using System;

namespace SukeFlow.Core.Models;

/// <summary>
/// 学期信息：起始日期与总周数，用于「第 X 周」计算与日期换算。
/// 第 1 周的周一为 <see cref="StartDate"/>。
/// </summary>
public sealed class SemesterInfo
{
    /// <summary>第 1 周周一。</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>总周数。</summary>
    public int WeekCount { get; init; } = 20;

    /// <summary>测试用默认学期：2026-09-07 开学，20 周。</summary>
    public static SemesterInfo Default { get; } = new()
    {
        StartDate = new DateOnly(2026, 9, 7),
        WeekCount = 20,
    };

    /// <summary>指定日期属于第几周（可能为 0 或负数表示开学前，超过 <see cref="WeekCount"/> 表示学期后）。</summary>
    public int GetWeek(DateOnly date)
    {
        var days = date.DayNumber - StartDate.DayNumber;
        return (int)Math.Floor(days / 7.0) + 1;
    }

    /// <summary>取某周某天的日期（dayOfWeek：1=周一 … 7=周日）。</summary>
    public DateOnly GetDate(int week, int dayOfWeek)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dayOfWeek, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dayOfWeek, 7);
        return StartDate.AddDays((week - 1) * 7 + (dayOfWeek - 1));
    }

    /// <summary>取某周的日期范围（周一 ~ 周日）。</summary>
    public (DateOnly Start, DateOnly End) GetWeekRange(int week) => (GetDate(week, 1), GetDate(week, 7));

    /// <summary>把任意周次循环映射到 1 ~ <see cref="WeekCount"/>。</summary>
    public int Normalize(int week)
    {
        if (WeekCount <= 0)
        {
            return week;
        }

        var normalized = (week - 1) % WeekCount;
        if (normalized < 0)
        {
            normalized += WeekCount;
        }

        return normalized + 1;
    }
}
