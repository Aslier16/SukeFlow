using System.Text.Json.Serialization;

namespace SukeFlow.Core.Models;

/// <summary>
/// 一次排课：某星期、某节次区间、某周次模式。同一门课可有多个 <see cref="CourseSession"/>。
/// </summary>
public sealed class CourseSession
{
    /// <summary>星期，1=周一 … 7=周日。</summary>
    public required int DayOfWeek { get; init; }

    /// <summary>起始节次（含），1 基。</summary>
    public required int StartPeriod { get; init; }

    /// <summary>结束节次（含）。</summary>
    public required int EndPeriod { get; init; }

    /// <summary>周次模式。</summary>
    public required WeekPattern Weeks { get; init; }

    /// <summary>课程类型标记：★讲课 ☆实验 □实践 ■实践周 〇课外 ◆上机。</summary>
    public string? TypeMark { get; init; }

    /// <summary>是否为调课（名称前缀【调】）。</summary>
    public bool IsAdjusted { get; init; }

    /// <summary>教学班 ID（<c>data-jxb_id</c>）。</summary>
    public string? JxbId { get; init; }

    /// <summary>教学班名称。</summary>
    public string? TeachingClass { get; init; }

    /// <summary>节数。</summary>
    [JsonIgnore]
    public int PeriodCount => EndPeriod - StartPeriod + 1;

    /// <summary>该节次是否落在本时段内。</summary>
    public bool CoversPeriod(int period) => period >= StartPeriod && period <= EndPeriod;

    /// <summary>本时段在指定周是否上课。</summary>
    public bool OccursOn(int week) => Weeks.Contains(week);

    /// <summary>与另一时段是否在同一天且节次重叠（不考虑周次）。</summary>
    public bool Overlaps(CourseSession other) =>
        DayOfWeek == other.DayOfWeek &&
        StartPeriod <= other.EndPeriod &&
        other.StartPeriod <= EndPeriod;

    /// <summary>节次文本，如 <c>第1-2节</c>。</summary>
    [JsonIgnore]
    public string PeriodText => StartPeriod == EndPeriod
        ? $"第{StartPeriod}节"
        : $"第{StartPeriod}-{EndPeriod}节";

    /// <summary>星期文本，如 <c>周二</c>。</summary>
    [JsonIgnore]
    public string DayText => DayTextOf(DayOfWeek);

    /// <summary>取星期文本（1=周一 … 7=周日）。</summary>
    public static string DayTextOf(int dayOfWeek) => dayOfWeek switch
    {
        1 => "周一",
        2 => "周二",
        3 => "周三",
        4 => "周四",
        5 => "周五",
        6 => "周六",
        7 => "周日",
        _ => $"周{dayOfWeek}",
    };

    /// <summary>详情卡片中的单行摘要，如 <c>周二 第1-2节 · 2-16周 · ★ · 调课</c>。</summary>
    [JsonIgnore]
    public string DisplaySummary
    {
        get
        {
            var text = $"{DayText} {PeriodText} · {Weeks.DisplayText}";
            if (!string.IsNullOrEmpty(TypeMark))
            {
                text += $" · {TypeMark}";
            }

            if (IsAdjusted)
            {
                text += " · 调课";
            }

            return text;
        }
    }
}
