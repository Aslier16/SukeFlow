using System;
using System.Collections.Generic;
using System.Linq;

namespace SukeFlow.Core.Models;

/// <summary>一节课的时间信息。</summary>
/// <param name="Index">节次序号（1 基）。</param>
/// <param name="Start">开始时间。</param>
/// <param name="End">结束时间。</param>
public readonly record struct PeriodTime(int Index, TimeOnly Start, TimeOnly End)
{
    /// <summary>开始时间文本，如 <c>07:30</c>。</summary>
    public string StartText => Start.ToString(@"HH\:mm");

    /// <summary>结束时间文本，如 <c>08:10</c>。</summary>
    public string EndText => End.ToString(@"HH\:mm");

    public override string ToString() => $"{StartText}-{EndText}";
}

/// <summary>
/// 节次时间表（每天有哪些节、每节的起止时间），可整体替换为学校作息。
/// </summary>
public sealed class PeriodSchedule
{
    private readonly PeriodTime[] _periods;

    private PeriodSchedule(PeriodTime[] periods)
    {
        _periods = periods;
    }

    /// <summary>节次数量。</summary>
    public int Count => _periods.Length;

    /// <summary>按 1 基节次取时间；越界返回 <see langword="null"/>。</summary>
    public PeriodTime? this[int period] =>
        period >= 1 && period <= _periods.Length ? _periods[period - 1] : null;

    /// <summary>全部节次。</summary>
    public IReadOnlyList<PeriodTime> Periods => _periods;

    /// <summary>
    /// 默认作息：22 节，每节 40 分钟、节间 5 分钟，第 1 节 07:30 开始。
    /// 第 n 节开始时间 = 07:30 + (n−1)×45 分钟。
    /// </summary>
    public static PeriodSchedule Default { get; } = Create(
        count: 22,
        firstStart: new TimeOnly(7, 30),
        duration: TimeSpan.FromMinutes(40),
        gap: TimeSpan.FromMinutes(5));

    /// <summary>按规则生成节次时间表。</summary>
    public static PeriodSchedule Create(int count, TimeOnly firstStart, TimeSpan duration, TimeSpan gap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var step = duration + gap;
        var periods = new PeriodTime[count];
        var start = firstStart.ToTimeSpan();
        for (var i = 0; i < count; i++)
        {
            var end = start + duration;
            periods[i] = new PeriodTime(i + 1, TimeOnly.FromTimeSpan(start), TimeOnly.FromTimeSpan(end));
            start += step;
        }

        return new PeriodSchedule(periods);
    }

    /// <summary>用显式的起止时间构造（长度即节数）。</summary>
    public static PeriodSchedule FromTimes(IEnumerable<(TimeOnly Start, TimeOnly End)> times)
    {
        var list = times.ToList();
        ArgumentOutOfRangeException.ThrowIfZero(list.Count);
        var periods = new PeriodTime[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            periods[i] = new PeriodTime(i + 1, list[i].Start, list[i].End);
        }

        return new PeriodSchedule(periods);
    }
}
