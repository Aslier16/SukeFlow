using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
namespace SukeFlow.Core.Models;

/// <summary>
/// 一个课程格子中的一条待绘制内容：课程 + 时段 + 是否属于当前显示周。
/// </summary>
/// <param name="Course">课程。</param>
/// <param name="Session">时段。</param>
/// <param name="IsCurrentWeek">当前显示周是否上课（否则应淡化绘制并标注「非本周」）。</param>
public readonly record struct CellEntry(Course Course, CourseSession Session, bool IsCurrentWeek);

/// <summary>
/// 整张课程表：学期/学生信息 + 课程集合 + 格子查询。
/// </summary>
public sealed class Timetable
{
    /// <summary>学期名称，如 <c>2026-2027学年第1学期</c>。</summary>
    public string? SemesterName { get; set; }

    /// <summary>学生姓名。</summary>
    public string? StudentName { get; set; }

    /// <summary>学号。</summary>
    public string? StudentId { get; set; }

    /// <summary>全部课程。</summary>
    public List<Course> Courses { get; set; } = [];

    /// <summary>
    /// 查询某周、某天、某节次上的所有课程内容。
    /// 返回结果中「当周上课」的排在前、非当周的排在后，便于按顺序绘制（先淡化、后正常）。
    /// </summary>
    public IReadOnlyList<CellEntry> GetCellEntries(int week, int dayOfWeek, int period)
    {
        List<CellEntry>? current = null;
        List<CellEntry>? other = null;

        foreach (var course in Courses)
        {
            foreach (var session in course.Sessions)
            {
                if (session.DayOfWeek != dayOfWeek || !session.CoversPeriod(period))
                {
                    continue;
                }

                var entry = new CellEntry(course, session, session.OccursOn(week));
                var list = entry.IsCurrentWeek
                    ? current ??= []
                    : other ??= [];
                list.Add(entry);
            }
        }

        if (current is null)
        {
            return (IReadOnlyList<CellEntry>?)other ?? [];
        }

        if (other is not null)
        {
            current.AddRange(other);
        }

        return current;
    }

    /// <summary>是否存在任何课程内容（用于空表判断）。</summary>
    [JsonIgnore]
    public bool IsEmpty => Courses.Count == 0 || Courses.All(c => c.Sessions.Count == 0);
}
