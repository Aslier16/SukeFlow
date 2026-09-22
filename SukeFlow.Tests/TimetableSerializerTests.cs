using System.Linq;
using SukeFlow.Core.Services;
using Xunit;

namespace SukeFlow.Tests;

/// <summary>课表 JSON 持久化（源生成序列化）的往返一致性。</summary>
public class TimetableSerializerTests
{
    /// <summary>示例课表序列化 → 反序列化后内容一致（中文不转义、camelCase 属性名）。</summary>
    [Fact]
    public void RoundTrip_Preserves_Timetable()
    {
        var original = SampleTimetable.Parse();

        var json = TimetableSerializer.Serialize(original);
        var restored = TimetableSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Contains("semesterName", json);
        Assert.Contains("陈思远", json);              // 中文未转义
        Assert.DoesNotContain("\\u", json);

        Assert.Equal(original.SemesterName, restored!.SemesterName);
        Assert.Equal(original.StudentName, restored.StudentName);
        Assert.Equal(original.StudentId, restored.StudentId);
        Assert.Equal(original.Courses.Count, restored.Courses.Count);
        Assert.Equal(
            original.Courses.SelectMany(c => c.Sessions).Count(),
            restored.Courses.SelectMany(c => c.Sessions).Count());

        var javaOriginal = original.Courses.Single(c => c.Name == "Web后端开发（JAVA）");
        var javaRestored = restored.Courses.Single(c => c.Name == "Web后端开发（JAVA）");
        Assert.Equal(javaOriginal.Teacher, javaRestored.Teacher);
        Assert.Equal(javaOriginal.Location, javaRestored.Location);
        Assert.Equal(javaOriginal.Credits, javaRestored.Credits);

        var oddOriginal = javaOriginal.Sessions.Single(s => s.TypeMark == "◆");
        var oddRestored = javaRestored.Sessions.Single(s => s.TypeMark == "◆");
        Assert.Equal(oddOriginal.Weeks.Parity, oddRestored.Weeks.Parity);
        Assert.Equal(oddOriginal.Weeks.DisplayText, oddRestored.Weeks.DisplayText);
        Assert.True(oddRestored.IsAdjusted);
    }

    /// <summary>损坏 / 非课表 JSON 返回 null（调用方回退到示例课表）。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{ not json")]
    [InlineData("{\"courses\": 3}")]
    [InlineData("[]")]
    public void Deserialize_Invalid_Json_Returns_Null(string json)
    {
        Assert.Null(TimetableSerializer.Deserialize(json));
    }

    /// <summary>计算属性（ColorKey / IsEmpty / PeriodCount 等）不写入 JSON。</summary>
    [Fact]
    public void Computed_Properties_Are_Not_Serialized()
    {
        var json = TimetableSerializer.Serialize(SampleTimetable.Parse());

        Assert.DoesNotContain("colorKey", json);
        Assert.DoesNotContain("isEmpty", json);
        Assert.DoesNotContain("periodCount", json);
        Assert.DoesNotContain("displayText", json);
        Assert.DoesNotContain("displaySummary", json);
    }
}
