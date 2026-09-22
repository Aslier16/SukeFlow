using System.Linq;
using SukeFlow.Core.Models;
using SukeFlow.Core.Services;
using Xunit;

namespace SukeFlow.Tests;

/// <summary>
/// 内置示例课表（<c>Assets/个人课表查询.html</c>，已脱敏）的端到端解析回归测试。
/// 同时充当隐私回归：断言文件中只有虚构的姓名/学号。
/// </summary>
public class SampleTimetableTests
{
    private static readonly Timetable Sample = SampleTimetable.Parse();

    /// <summary>表头（学期 / 姓名 / 学号）能正确解析，且为脱敏后的虚构数据。</summary>
    [Fact]
    public void Header_IsParsed_AsAnonymizedStudent()
    {
        Assert.Equal("2026-2027学年第1学期", Sample.SemesterName);
        Assert.Equal("陈思远", Sample.StudentName);
        Assert.Equal("20260000001", Sample.StudentId);
    }

    /// <summary>课程 / 时段数量稳定（改动解析器或示例文件时能立刻发现回归）。</summary>
    [Fact]
    public void Sample_Has_8_Courses_And_16_Sessions()
    {
        Assert.Equal(8, Sample.Courses.Count);
        Assert.Equal(16, Sample.Courses.Sum(c => c.Sessions.Count));
    }

    /// <summary>表尾「其它课程」（无固定时间）不进入模型。</summary>
    [Fact]
    public void OtherCoursesSection_IsIgnored()
    {
        Assert.DoesNotContain(Sample.Courses, c => c.Name.Contains("形势与政策"));
    }

    /// <summary>所有时段都落在 7 天 × 22 节范围内。</summary>
    [Fact]
    public void AllSessions_Fit_Weekday_And_Period_Range()
    {
        foreach (var session in Sample.Courses.SelectMany(c => c.Sessions))
        {
            Assert.InRange(session.DayOfWeek, 1, 7);
            Assert.InRange(session.StartPeriod, 1, 22);
            Assert.InRange(session.EndPeriod, session.StartPeriod, 22);
        }
    }

    /// <summary>【调】前缀与类型标记从课程名中剥离，周次单双周正确，其它字段一并解析。</summary>
    [Fact]
    public void AdjustedPrefix_TypeMarks_And_DetailFields_AreParsed()
    {
        var java = Sample.Courses.Single(c => c.Name == "Web后端开发（JAVA）");

        var lecture = java.Sessions.Single(s => s.TypeMark == "★");
        Assert.True(lecture.IsAdjusted);
        Assert.Equal(WeekParity.All, lecture.Weeks.Parity);
        Assert.Equal((2, 1, 2, 2, 16), (lecture.DayOfWeek, lecture.StartPeriod, lecture.EndPeriod, lecture.Weeks.StartWeek, lecture.Weeks.EndWeek));

        var lab = java.Sessions.Single(s => s.TypeMark == "◆");
        Assert.Equal(WeekParity.Odd, lab.Weeks.Parity);
        Assert.Equal((2, 7, 8), (lab.DayOfWeek, lab.StartPeriod, lab.EndPeriod));
        Assert.Equal("3-15周（单）", lab.Weeks.DisplayText);

        Assert.Equal("林依辰", java.Teacher);
        Assert.Equal("实2-B-304", java.Location);
        Assert.Equal("滨海校区", java.Campus);
        Assert.Equal("24软件工程", java.ClassComposition);
        Assert.Equal("考查", java.AssessmentMethod);
        Assert.Equal("讲课:32,上机:16", java.HoursComposition);
        Assert.Equal(2, java.WeeklyHours.GetValueOrDefault());
        Assert.Equal(32, java.TotalHours.GetValueOrDefault());
        Assert.Equal(2.5, java.Credits.GetValueOrDefault());
        Assert.Equal(SelectionState.Selected, java.Selection);
        Assert.NotEmpty(java.Sessions[0].JxbId!);
    }

    /// <summary>实践周整段课（■，1-8 节，周一到周四各一条）。</summary>
    [Fact]
    public void PracticeWeekCourse_Covers_Full_Day_Block()
    {
        var practice = Sample.Courses.Single(c => c.Name == "嵌入式技术课程设计");

        Assert.Equal(4, practice.Sessions.Count);
        Assert.All(practice.Sessions, s =>
        {
            Assert.Equal("■", s.TypeMark);
            Assert.Equal(1, s.StartPeriod);
            Assert.Equal(8, s.EndPeriod);
            Assert.Equal(17, s.Weeks.StartWeek);
            Assert.False(s.IsAdjusted);
        });
        Assert.Equal([1, 2, 3, 4], practice.Sessions.Select(s => s.DayOfWeek).OrderBy(d => d).ToArray());
    }

    /// <summary>同名课程的不同时段聚合为一门课；教学班 ID 保留在教学班名称中。</summary>
    [Fact]
    public void SameCourse_Sessions_AreGrouped()
    {
        var network = Sample.Courses.Single(c => c.Name == "计算机网络");
        Assert.Equal(2, network.Sessions.Count);
        Assert.All(network.Sessions, s => Assert.StartsWith("(2026-2027-1)-10830733-", s.TeachingClass!));
    }
}
