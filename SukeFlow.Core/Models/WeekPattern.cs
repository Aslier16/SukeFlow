using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SukeFlow.Core.Models;

/// <summary>
/// 周次模式：起始周 ~ 结束周（可含单/双周限制）。
/// 对应教务 HTML 中的 <c>周数：2-16周</c> / <c>3-15周(单)</c> / <c>17周</c> 等写法。
/// </summary>
public sealed partial class WeekPattern
{
    /// <summary>起始周（含）。</summary>
    public required int StartWeek { get; init; }

    /// <summary>结束周（含）。</summary>
    public required int EndWeek { get; init; }

    /// <summary>单双周限制。</summary>
    public WeekParity Parity { get; init; } = WeekParity.All;

    /// <summary>原始文本（保留用于详情展示）。</summary>
    public string? RawText { get; init; }

    /// <summary>指定的周是否落在该模式内。</summary>
    public bool Contains(int week)
    {
        if (week < StartWeek || week > EndWeek)
        {
            return false;
        }

        return Parity switch
        {
            WeekParity.Odd => week % 2 == 1,
            WeekParity.Even => week % 2 == 0,
            _ => true,
        };
    }

    /// <summary>展示文本，如 <c>2-16周</c>、<c>3-15周（单）</c>。</summary>
    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var range = StartWeek == EndWeek ? $"{StartWeek}周" : $"{StartWeek}-{EndWeek}周";
            return Parity switch
            {
                WeekParity.Odd => range + "（单）",
                WeekParity.Even => range + "（双）",
                _ => range,
            };
        }
    }

    /// <summary>全学期每周（用于缺省情况）。</summary>
    public static WeekPattern EveryWeek(int weekCount) => new()
    {
        StartWeek = 1,
        EndWeek = weekCount,
    };

    [GeneratedRegex(@"^\s*(?:第)?\s*(\d+)\s*(?:[-–—~至]\s*(\d+))?\s*周(?:次)?\s*[（(]?\s*(单|双)?\s*[）)]?\s*$")]
    private static partial Regex PatternRegex { get; }

    /// <summary>解析形如 <c>2-16周</c>、<c>3-15周(单)</c>、<c>17周</c> 的周数文本。</summary>
    public static bool TryParse(string? text, out WeekPattern pattern)
    {
        pattern = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = PatternRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        var start = int.Parse(match.Groups[1].Value);
        var end = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : start;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        pattern = new WeekPattern
        {
            StartWeek = start,
            EndWeek = end,
            Parity = match.Groups[3].Value switch
            {
                "单" => WeekParity.Odd,
                "双" => WeekParity.Even,
                _ => WeekParity.All,
            },
            RawText = text.Trim(),
        };
        return true;
    }
}
