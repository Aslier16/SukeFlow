using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SukeFlow.Core.Models;

namespace SukeFlow.Core.Services;

/// <summary>
/// 方正教务系统「个人课表查询」页面解析器（<c>kblist_table</c> 结构）。
/// 手写解析、零第三方依赖，兼容 WASM/AOT。
/// </summary>
public sealed partial class ZfsoftTimetableParser
{
    /// <summary>类型标记字符集合：★讲课 ☆实验 □实践 ■实践周 〇课外 ◆上机。</summary>
    private const string TypeMarkChars = "★☆□■〇◆";

    /// <summary>解析 HTML，得到课程表数据。</summary>
    public Timetable Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var table = KbListTableRegex.Match(html);
        var scope = table.Success ? table.Groups["body"].Value : html;

        var timetable = new Timetable();
        ParseHeader(scope, timetable);

        var courses = new Dictionary<string, Course>(StringComparer.Ordinal);
        foreach (Match tbody in TbodyRegex.Matches(scope))
        {
            if (!int.TryParse(tbody.Groups["day"].Value, out var dayOfWeek) || dayOfWeek is < 1 or > 7)
            {
                continue;
            }

            ParseDay(tbody.Groups["body"].Value, dayOfWeek, courses);
        }

        timetable.Courses.AddRange(courses.Values);
        return timetable;
    }

    // ---------------------------------------------------------------- 表头

    private void ParseHeader(string scope, Timetable timetable)
    {
        var title = TimetableTitleRegex.Match(scope);
        if (!title.Success)
        {
            return;
        }

        var body = title.Groups["body"].Value;
        var remainder = body;

        foreach (Match h6 in H6Regex.Matches(body))
        {
            var text = CleanText(h6.Groups["body"].Value);
            var attrs = h6.Groups["attrs"].Value;

            if (attrs.Contains("pull-left", StringComparison.OrdinalIgnoreCase))
            {
                timetable.SemesterName = text;
            }
            else if (attrs.Contains("pull-right", StringComparison.OrdinalIgnoreCase))
            {
                var digits = DigitsRegex.Match(text);
                if (digits.Success)
                {
                    timetable.StudentId = digits.Value;
                }
            }

            remainder = remainder.Replace(h6.Value, string.Empty, StringComparison.Ordinal);
        }

        var name = CleanText(remainder);
        if (name.EndsWith("的课表", StringComparison.Ordinal))
        {
            name = name[..^3];
        }

        if (name.Length > 0)
        {
            timetable.StudentName = name;
        }
    }

    // ---------------------------------------------------------------- 按天解析

    private void ParseDay(string body, int dayOfWeek, Dictionary<string, Course> courses)
    {
        (int Start, int End)? carrySlot = null;
        var carryRowsLeft = 0;

        foreach (Match row in RowRegex.Matches(body))
        {
            (int Start, int End)? ownSlot = null;
            var ownSpan = 0;
            var courseCells = new List<string>();

            foreach (Match cell in TdRegex.Matches(row.Groups["body"].Value))
            {
                var attrs = cell.Groups["attrs"].Value;
                var inner = cell.Groups["body"].Value;

                var slot = SlotIdRegex.Match(attrs);
                if (slot.Success)
                {
                    ownSlot = (int.Parse(slot.Groups["s"].Value, CultureInfo.InvariantCulture),
                               int.Parse(slot.Groups["e"].Value, CultureInfo.InvariantCulture));
                    var span = RowSpanRegex.Match(attrs);
                    ownSpan = span.Success ? int.Parse(span.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
                }
                else if (WeekdayIdRegex.IsMatch(attrs))
                {
                    // 星期单元格，跳过
                }
                else if (CourseDivRegex.IsMatch(inner))
                {
                    courseCells.Add(inner);
                }
            }

            var slotInUse = ownSlot ?? (carryRowsLeft > 0 ? carrySlot : null);
            if (slotInUse is { } used && courseCells.Count > 0)
            {
                foreach (var cellHtml in courseCells)
                {
                    foreach (Match div in CourseDivRegex.Matches(cellHtml))
                    {
                        ParseCourseDiv(div.Groups["body"].Value, dayOfWeek, used.Start, used.End, courses);
                    }
                }
            }

            if (ownSlot is not null)
            {
                carrySlot = ownSlot;
                carryRowsLeft = Math.Max(ownSpan - 1, 0);
            }
            else if (carryRowsLeft > 0)
            {
                carryRowsLeft--;
            }
        }
    }

    // ---------------------------------------------------------------- 单个课程

    private void ParseCourseDiv(string divHtml, int dayOfWeek, int startPeriod, int endPeriod,
        Dictionary<string, Course> courses)
    {
        var title = TitleRegex.Match(divHtml);
        if (!title.Success)
        {
            return;
        }

        var selection = ParseSelection(title.Groups["body"].Value);
        var name = CleanText(title.Groups["body"].Value);
        var isAdjusted = StripAdjustPrefix(ref name);
        var typeMarks = ExtractTypeMarks(ref name);
        if (name.Length == 0)
        {
            return;
        }

        var jxbId = JxbIdRegex.Match(title.Groups["attrs"].Value) is { Success: true } jxb
            ? jxb.Groups["id"].Value
            : null;

        var details = ParseDetails(divHtml);
        var teacher = details.GetValueOrDefault("教师");
        var location = details.GetValueOrDefault("上课地点");

        var weeks = WeekPattern.TryParse(details.GetValueOrDefault("周数"), out var parsed)
            ? parsed
            : WeekPattern.EveryWeek(SemesterInfo.Default.WeekCount);

        var key = Course.GroupKey(name, teacher, location);
        if (!courses.TryGetValue(key, out var course))
        {
            course = new Course { Name = name };
            courses[key] = course;
        }

        course.Teacher ??= teacher;
        course.Location ??= location;
        course.Campus ??= details.GetValueOrDefault("校区");
        course.ClassComposition ??= details.GetValueOrDefault("教学班组成");
        course.AssessmentMethod ??= details.GetValueOrDefault("考核方式");
        course.HoursComposition ??= details.GetValueOrDefault("课程学时组成");
        course.WeeklyHours ??= ParseNumber(details.GetValueOrDefault("周学时"));
        course.TotalHours ??= ParseNumber(details.GetValueOrDefault("总学时"));
        course.Credits ??= ParseNumber(details.GetValueOrDefault("学分"));
        course.Selection = MergeSelection(course.Selection, selection);

        var session = new CourseSession
        {
            DayOfWeek = dayOfWeek,
            StartPeriod = startPeriod,
            EndPeriod = endPeriod,
            Weeks = weeks,
            TypeMark = typeMarks.Length > 0 ? typeMarks : null,
            IsAdjusted = isAdjusted,
            JxbId = jxbId,
            TeachingClass = details.GetValueOrDefault("教学班"),
        };

        if (!ContainsSession(course, session))
        {
            course.Sessions.Add(session);
        }
    }

    private static bool ContainsSession(Course course, CourseSession session)
    {
        foreach (var existing in course.Sessions)
        {
            if (existing.DayOfWeek == session.DayOfWeek &&
                existing.StartPeriod == session.StartPeriod &&
                existing.EndPeriod == session.EndPeriod &&
                existing.Weeks.StartWeek == session.Weeks.StartWeek &&
                existing.Weeks.EndWeek == session.Weeks.EndWeek &&
                existing.Weeks.Parity == session.Weeks.Parity &&
                string.Equals(existing.TypeMark, session.TypeMark, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- 详情解析

    /// <summary>解析课程详情段落：<c>图标 + 键：值</c> 序列。</summary>
    private static Dictionary<string, string> ParseDetails(string divHtml)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var paragraph = ParagraphRegex.Match(divHtml);
        if (!paragraph.Success)
        {
            return result;
        }

        // 图标 span 作为分隔符，避免「校区」与「上课地点」粘连
        var html = IconSpanRegex.Replace(paragraph.Groups["body"].Value, "\u0001");
        html = TagRegex.Replace(html, " ");
        var text = WebUtility.HtmlDecode(html);

        foreach (var raw in text.Split('\u0001'))
        {
            var item = CollapseWhitespace(raw);
            if (item.Length == 0)
            {
                continue;
            }

            var index = item.IndexOfAny(['：', ':']);
            if (index <= 0 || index == item.Length - 1)
            {
                continue;
            }

            var key = item[..index].Trim();
            var value = item[(index + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static SelectionState ParseSelection(string titleHtml)
    {
        var match = ColorRegex.Match(titleHtml);
        if (!match.Success)
        {
            return SelectionState.Unknown;
        }

        return match.Groups["color"].Value.ToLowerInvariant() switch
        {
            "red" => SelectionState.Pending,
            "blue" => SelectionState.Selected,
            _ => SelectionState.Unknown,
        };
    }

    private static SelectionState MergeSelection(SelectionState current, SelectionState incoming)
    {
        if (current is SelectionState.Selected or SelectionState.Pending)
        {
            return current;
        }

        return incoming;
    }

    private static bool StripAdjustPrefix(ref string name)
    {
        var match = AdjustPrefixRegex.Match(name);
        if (!match.Success)
        {
            return false;
        }

        name = name[match.Length..].Trim();
        return match.Groups["mark"].Value.Contains('调');
    }

    private static string ExtractTypeMarks(ref string name)
    {
        var marks = new StringBuilder();
        var builder = new StringBuilder(name.Length);

        foreach (var ch in name)
        {
            if (TypeMarkChars.Contains(ch))
            {
                if (marks.ToString().IndexOf(ch) < 0)
                {
                    marks.Append(ch);
                }
            }
            else
            {
                builder.Append(ch);
            }
        }

        name = builder.ToString().Trim();
        return marks.ToString();
    }

    private static double? ParseNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = NumberRegex.Match(text);
        return match.Success &&
               double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    // ---------------------------------------------------------------- 文本工具

    private static string CleanText(string html)
    {
        var text = TagRegex.Replace(html, string.Empty);
        return CollapseWhitespace(WebUtility.HtmlDecode(text));
    }

    private static string CollapseWhitespace(string text)
    {
        var replaced = text.Replace('\u00a0', ' ').Replace('\u3000', ' ').Replace('\t', ' ').Replace("\r", " ").Replace("\n", " ");
        return WhitespaceRegex.Replace(replaced, " ").Trim();
    }

    // ---------------------------------------------------------------- 正则

    [GeneratedRegex("""<table[^>]*\bid\s*=\s*["']?kblist_table["']?[^>]*>(?<body>.*?)</table>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex KbListTableRegex { get; }

    [GeneratedRegex("""<div[^>]*\bclass\s*=\s*["'][^"']*\btimetable_title\b[^"']*["'][^>]*>(?<body>.*?)</div>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TimetableTitleRegex { get; }

    [GeneratedRegex("""<h6(?<attrs>[^>]*)>(?<body>.*?)</h6>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex H6Regex { get; }

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex { get; }

    [GeneratedRegex("""<tbody[^>]*\bid\s*=\s*["']?xq_(?<day>\d+)["']?[^>]*>(?<body>.*?)</tbody>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TbodyRegex { get; }

    [GeneratedRegex(@"<tr[^>]*>(?<body>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RowRegex { get; }

    [GeneratedRegex(@"<td(?<attrs>[^>]*)>(?<body>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TdRegex { get; }

    [GeneratedRegex("""\bid\s*=\s*["']?jc_(?<day>\d+)-(?<s>\d+)-(?<e>\d+)["']?""", RegexOptions.IgnoreCase)]
    private static partial Regex SlotIdRegex { get; }

    [GeneratedRegex("""\bid\s*=\s*["']?xq_rowspan_(?<day>\d+)["']?""", RegexOptions.IgnoreCase)]
    private static partial Regex WeekdayIdRegex { get; }

    [GeneratedRegex("""\browspan\s*=\s*["']?(?<n>\d+)["']?""", RegexOptions.IgnoreCase)]
    private static partial Regex RowSpanRegex { get; }

    [GeneratedRegex("""<div[^>]*\bclass\s*=\s*["'][^"']*\btimetable_con\b[^"']*["'][^>]*>(?<body>.*?)</div>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CourseDivRegex { get; }

    [GeneratedRegex("""<(?<tag>u|span)(?<attrs>[^>]*\bclass\s*=\s*["'][^"']*\btitle\b[^"']*["'][^>]*)>(?<body>.*?)</\k<tag>>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex { get; }

    [GeneratedRegex("""\bdata-jxb_id\s*=\s*["'](?<id>[^"']*)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex JxbIdRegex { get; }

    [GeneratedRegex(@"<p[^>]*>(?<body>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphRegex { get; }

    [GeneratedRegex("""<span[^>]*\bclass\s*=\s*["'][^"']*glyphicon[^"']*["'][^>]*>\s*</span>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex IconSpanRegex { get; }

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex { get; }

    [GeneratedRegex("""\bcolor\s*(?:=|:)\s*["']?\s*(?<color>[a-zA-Z#0-9]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ColorRegex { get; }

    [GeneratedRegex(@"^【(?<mark>[^】]*)】")]
    private static partial Regex AdjustPrefixRegex { get; }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)")]
    private static partial Regex NumberRegex { get; }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex { get; }
}
