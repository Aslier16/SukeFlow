using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SukeFlow.Core.Models;

/// <summary>
/// 一门课程（同一门课的所有时段聚合在一起）。
/// </summary>
public sealed class Course
{
    /// <summary>课程名（已去掉【调】前缀与类型标记）。</summary>
    public required string Name { get; init; }

    /// <summary>教师。</summary>
    public string? Teacher { get; set; }

    /// <summary>上课地点。</summary>
    public string? Location { get; set; }

    /// <summary>校区。</summary>
    public string? Campus { get; set; }

    /// <summary>教学班组成。</summary>
    public string? ClassComposition { get; set; }

    /// <summary>考核方式。</summary>
    public string? AssessmentMethod { get; set; }

    /// <summary>课程学时组成。</summary>
    public string? HoursComposition { get; set; }

    /// <summary>周学时。</summary>
    public double? WeeklyHours { get; set; }

    /// <summary>总学时。</summary>
    public double? TotalHours { get; set; }

    /// <summary>学分。</summary>
    public double? Credits { get; set; }

    /// <summary>选课状态。</summary>
    public SelectionState Selection { get; set; } = SelectionState.Unknown;

    /// <summary>该课程的全部时段。</summary>
    public List<CourseSession> Sessions { get; set; } = [];

    /// <summary>颜色哈希键：同一门课（同名）在界面中使用相同颜色。</summary>
    [JsonIgnore]
    public string ColorKey => Name;

    /// <summary>聚合键：同名 + 同教师 + 同地点视为同一门课。</summary>
    public static string GroupKey(string name, string? teacher, string? location) =>
        $"{name}\u0001{teacher}\u0001{location}";
}
