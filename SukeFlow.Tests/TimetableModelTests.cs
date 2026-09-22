using System;
using System.Linq;
using SukeFlow.Core.Models;
using Xunit;

namespace SukeFlow.Tests;

/// <summary>课表模型行为：格子查询顺序、时段区间、周次与节次时间。</summary>
public class TimetableModelTests
{
    private static Timetable BuildSample()
    {
        Timetable timetable = new() { SemesterName = "2026-2027学年第1学期" };

        timetable.Courses.Add(new Course
        {
            Name = "每周课",
            Sessions =
            {
                new CourseSession { DayOfWeek = 3, StartPeriod = 1, EndPeriod = 2, Weeks = WeekPattern.EveryWeek(20) },
            },
        });

        timetable.Courses.Add(new Course
        {
            Name = "单周课",
            Sessions =
            {
                new CourseSession
                {
                    DayOfWeek = 3,
                    StartPeriod = 1,
                    EndPeriod = 2,
                    Weeks = WeekPattern.TryParse("3-15周(单)", out var odd) ? odd : WeekPattern.EveryWeek(20),
                },
            },
        });

        timetable.Courses.Add(new Course
        {
            Name = "双周课",
            Sessions =
            {
                new CourseSession
                {
                    DayOfWeek = 3,
                    StartPeriod = 1,
                    EndPeriod = 2,
                    Weeks = WeekPattern.TryParse("2-16周(双)", out var even) ? even : WeekPattern.EveryWeek(20),
                },
            },
        });

        return timetable;
    }

    /// <summary>当周上课的排在前面，非当周的排在后面（供控件先淡化后正常绘制）。</summary>
    [Fact]
    public void GetCellEntries_Puts_CurrentWeek_First()
    {
        var timetable = BuildSample();

        var week4 = timetable.GetCellEntries(4, 3, 1);   // 双周
        Assert.Equal(3, week4.Count);
        Assert.Equal(["每周课", "双周课"], week4.Where(e => e.IsCurrentWeek).Select(e => e.Course.Name).ToArray());
        Assert.Equal(["单周课"], week4.Where(e => !e.IsCurrentWeek).Select(e => e.Course.Name).ToArray());
        Assert.Equal(week4.Count(e => e.IsCurrentWeek), week4.TakeWhile(e => e.IsCurrentWeek).Count());

        var week3 = timetable.GetCellEntries(3, 3, 2);   // 单周
        Assert.Equal(["每周课", "单周课"], week3.Where(e => e.IsCurrentWeek).Select(e => e.Course.Name).ToArray());
        Assert.Equal(["双周课"], week3.Where(e => !e.IsCurrentWeek).Select(e => e.Course.Name).ToArray());
    }

    /// <summary>格子查询按节次命中；无课格子返回空集合。</summary>
    [Fact]
    public void GetCellEntries_Matches_Period_And_Day()
    {
        var timetable = BuildSample();

        Assert.Equal(3, timetable.GetCellEntries(4, 3, 2).Count);
        Assert.Empty(timetable.GetCellEntries(4, 3, 3));
        Assert.Empty(timetable.GetCellEntries(4, 4, 1));
    }

    /// <summary>IsEmpty 判定：无课程或无任何时段都视为空表。</summary>
    [Fact]
    public void IsEmpty_Reflects_Content()
    {
        Assert.True(new Timetable().IsEmpty);
        Assert.True(new Timetable { Courses = { new Course { Name = "空课" } } }.IsEmpty);
        Assert.False(BuildSample().IsEmpty);
    }

    /// <summary>时段区间、重叠、文本与摘要。</summary>
    [Fact]
    public void Session_Helpers_Behave()
    {
        Assert.True(WeekPattern.TryParse("2-16周(双)", out var weeks));
        var session = new CourseSession
        {
            DayOfWeek = 4,
            StartPeriod = 3,
            EndPeriod = 4,
            Weeks = weeks,
            TypeMark = "★",
            IsAdjusted = true,
        };

        Assert.Equal(2, session.PeriodCount);
        Assert.True(session.CoversPeriod(3));
        Assert.True(session.CoversPeriod(4));
        Assert.False(session.CoversPeriod(5));
        Assert.False(session.OccursOn(3));
        Assert.True(session.OccursOn(4));
        Assert.Equal("第3-4节", session.PeriodText);
        Assert.Equal("周四", session.DayText);
        Assert.Equal("周四 第3-4节 · 2-16周（双） · ★ · 调课", session.DisplaySummary);

        var other = new CourseSession { DayOfWeek = 4, StartPeriod = 4, EndPeriod = 5, Weeks = weeks };
        var otherDay = new CourseSession { DayOfWeek = 5, StartPeriod = 4, EndPeriod = 5, Weeks = weeks };
        Assert.True(session.Overlaps(other));
        Assert.False(session.Overlaps(otherDay));

        Assert.Equal("周六", CourseSession.DayTextOf(6));
        Assert.Equal("周日", CourseSession.DayTextOf(7));
        Assert.Equal("周8", CourseSession.DayTextOf(8));
    }

    /// <summary>学期信息：第 1 周从 2026-09-07 起算，周次循环归一。</summary>
    [Fact]
    public void SemesterInfo_Computes_Weeks_And_Dates()
    {
        var semester = SemesterInfo.Default;

        Assert.Equal(new DateOnly(2026, 9, 7), semester.StartDate);
        Assert.Equal(20, semester.WeekCount);

        Assert.Equal(1, semester.GetWeek(new DateOnly(2026, 9, 7)));
        Assert.Equal(1, semester.GetWeek(new DateOnly(2026, 9, 13)));
        Assert.Equal(2, semester.GetWeek(new DateOnly(2026, 9, 14)));
        Assert.Equal(0, semester.GetWeek(new DateOnly(2026, 9, 6)));

        Assert.Equal(new DateOnly(2026, 9, 16), semester.GetDate(2, 3));
        Assert.Equal((new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20)), semester.GetWeekRange(2));

        Assert.Equal(20, semester.Normalize(0));
        Assert.Equal(20, semester.Normalize(20));
        Assert.Equal(1, semester.Normalize(21));
        Assert.Equal(2, semester.Normalize(-18));
    }

    /// <summary>默认作息：22 节，07:30 起，每节 40 分钟、间隔 5 分钟。</summary>
    [Fact]
    public void Default_PeriodSchedule_Matches_Spec()
    {
        var schedule = PeriodSchedule.Default;

        Assert.Equal(22, schedule.Count);
        Assert.Equal((new TimeOnly(7, 30), new TimeOnly(8, 10)), (schedule[1]!.Value.Start, schedule[1]!.Value.End));
        Assert.Equal((new TimeOnly(16, 30), new TimeOnly(17, 10)), (schedule[13]!.Value.Start, schedule[13]!.Value.End));
        Assert.Equal((new TimeOnly(23, 15), new TimeOnly(23, 55)), (schedule[22]!.Value.Start, schedule[22]!.Value.End));
        Assert.Equal("07:30-08:10", schedule[1]!.Value.ToString());
        Assert.Null(schedule[0]);
        Assert.Null(schedule[23]);
    }
}
