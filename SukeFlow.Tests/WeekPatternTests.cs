using SukeFlow.Core.Models;
using Xunit;

namespace SukeFlow.Tests;

/// <summary>周次模式（<c>2-16周</c> / <c>3-15周(单)</c> / <c>17周</c> 等）解析与判断。</summary>
public class WeekPatternTests
{
    [Theory]
    [InlineData("2-16周", 2, 16, WeekParity.All)]
    [InlineData("3-15周(单)", 3, 15, WeekParity.Odd)]
    [InlineData("2-16周（双）", 2, 16, WeekParity.Even)]
    [InlineData("17周", 17, 17, WeekParity.All)]
    [InlineData("第 3-15 周(单)", 3, 15, WeekParity.Odd)]
    [InlineData("17周次", 17, 17, WeekParity.All)]
    [InlineData("16-2周", 2, 16, WeekParity.All)]
    public void TryParse_Recognizes_Formats(string text, int start, int end, WeekParity parity)
    {
        Assert.True(WeekPattern.TryParse(text, out var pattern));
        Assert.Equal(start, pattern.StartWeek);
        Assert.Equal(end, pattern.EndWeek);
        Assert.Equal(parity, pattern.Parity);
        Assert.Equal(text.Trim(), pattern.RawText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("周数待定")]
    [InlineData("每周")]
    [InlineData(null)]
    public void TryParse_Rejects_Invalid_Text(string? text)
    {
        Assert.False(WeekPattern.TryParse(text, out _));
    }

    [Fact]
    public void Contains_Respects_Parity()
    {
        Assert.True(WeekPattern.TryParse("3-15周(单)", out var odd));
        Assert.True(odd.Contains(3));
        Assert.True(odd.Contains(15));
        Assert.False(odd.Contains(4));
        Assert.False(odd.Contains(2));

        Assert.True(WeekPattern.TryParse("2-16周(双)", out var even));
        Assert.True(even.Contains(2));
        Assert.False(even.Contains(3));
        Assert.False(even.Contains(17));
    }

    [Fact]
    public void DisplayText_Uses_Chinese_Parentheses()
    {
        Assert.True(WeekPattern.TryParse("2-16周", out var range));
        Assert.Equal("2-16周", range.DisplayText);

        Assert.True(WeekPattern.TryParse("17周", out var single));
        Assert.Equal("17周", single.DisplayText);

        Assert.True(WeekPattern.TryParse("2-16周(双)", out var even));
        Assert.Equal("2-16周（双）", even.DisplayText);
    }

    [Fact]
    public void EveryWeek_Covers_Whole_Semester()
    {
        var all = WeekPattern.EveryWeek(20);
        Assert.Equal((1, 20), (all.StartWeek, all.EndWeek));
        Assert.True(all.Contains(1));
        Assert.True(all.Contains(20));
        Assert.False(all.Contains(21));
    }
}
