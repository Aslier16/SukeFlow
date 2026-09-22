using SukeFlow.Core.Models;
using SukeFlow.Core.Services;
using Xunit;

namespace SukeFlow.Tests;

/// <summary>
/// 解析器的细节行为测试，使用与正方教务 <c>kblist_table</c> 一致的最小 HTML 片段
/// （详情全部在一个 <c>&lt;p&gt;</c> 内，以图标 span 分隔、<c>键：值</c> 形式罗列）。
/// </summary>
public class ZfsoftTimetableParserTests
{
    private static readonly ZfsoftTimetableParser Parser = new();

    // ------------------------------------------------------------ 测试夹具

    /// <summary>构造最小可解析页面：表头行 + 星期单元格 + 指定行。</summary>
    private static string Page(int day, string rowsHtml) => $"""
        <html><body>
        <table id="kblist_table">
        <tbody id="xq_{day}">
        <tr><td colspan="9"><div class="timetable_title"><h6 class="pull-left">2026-2027学年第1学期</h6>陈思远的课表<h6 class="pull-right">　学号：20260000001</h6></div><div><span class="pull-left">★-讲课☆-实验□-实践■-实践周〇-课外◆-上机</span><span class="pull-right"><font color="red" size="3"><i>红色斜体为待筛选</i></font></span></div></td></tr>
        <tr><td id="xq_rowspan_{day}">一</td></tr>
        {rowsHtml}
        </tbody>
        </table>
        </body></html>
        """;

    /// <summary>节次单元格（<c>id="jc_天-起-止"</c>，真实页面里课程内容在它的下一个单元格）。</summary>
    private static string SlotCell(int day, int start, int end, int rowspan = 1) =>
        $"""<td id="jc_{day}-{start}-{end}" rowspan="{rowspan}"><span class="festival">{start}-{end}</span></td>""";

    /// <summary>课程内容单元格（没有节次 id，节次由同行的节次单元格携带）。</summary>
    private static string ContentCell(string content) => $"""<td>{content}</td>""";

    /// <summary>由节次单元格 + 内容单元格拼一行。</summary>
    private static string Row(int day, int start, int end, string content, int rowspan = 1) =>
        $"<tr>{SlotCell(day, start, end, rowspan)}{ContentCell(content)}</tr>";

    /// <summary>单个课程块，字段写法与教务页面一致（含半角冒号、键后空格、值尾随空格）。</summary>
    private static string CourseDiv(
        string title,
        string weeks = "2-16周",
        string teacher = "陈老师",
        string campus = "示例校区",
        string location = "实2-B-101",
        string teachingClass = "(2026-2027-1)-10000001-20000001-1",
        string assessment = "考查",
        string color = "blue",
        string? jxbId = null)
    {
        var titleHtml = jxbId is null
            ? $"""<span class="title"><font color="{color}">{title}</font></span>"""
            : $"""<u class="title showJxbtkjl" data-jxb_id="{jxbId}" style="cursor: pointer;"><font color="{color}">{title}</font></u>""";

        var details = $"""
            <p><font color="blue"><span class="glyphicon glyphicon-calendar"></span> 周数：{weeks}</font><font color="blue"><span class="glyphicon glyphicon-tower"></span> 校区:{campus}<span class="glyphicon glyphicon-map-marker"></span> 上课地点：{location} </font><font color="blue"><span class="glyphicon glyphicon-user"></span> 教师 ：{teacher}</font><font color="blue"><span class="glyphicon glyphicon-home"></span> 教学班：{teachingClass}</font><font color="blue"><span class="glyphicon glyphicon-home"></span> 教学班组成：24软件工程</font><font color="blue"><span class="glyphicon glyphicon-tower"></span> 考核方式：{assessment}</font><font color="blue"><span class="glyphicon glyphicon-tower"></span> 周学时：16</font><font color="blue"><span class="glyphicon glyphicon-tower"></span> 总学时：32</font><font color="blue"><span class="glyphicon glyphicon-tower"></span> 学分：2.0</font></p>
            """;

        return $"""<div class="timetable_con text-left">{titleHtml}{details}</div>""";
    }

    // ------------------------------------------------------------ 用例

    /// <summary>半角冒号、键后空格、值尾随空格 / 全角空格都能正确切分与 Trim。</summary>
    [Fact]
    public void Details_Tolerate_ColonVariants_And_Whitespace()
    {
        var html = Page(1, Row(1, 1, 2, CourseDiv("高等数学", teacher: "陈\u3000\u3000老师", location: "实2-B-101 ")));

        var course = Assert.Single(Parser.Parse(html).Courses);

        Assert.Equal("高等数学", course.Name);
        Assert.Equal("示例校区", course.Campus);        // 半角冒号 校区:
        Assert.Equal("实2-B-101", course.Location);     // 值尾随空格
        Assert.Equal("陈 老师", course.Teacher);        // 键后空格 + 全角空格折叠
        Assert.Equal(SelectionState.Selected, course.Selection);
    }

    /// <summary>同一节次单元格内的多个 <c>timetable_con</c>（单双周交叠）都要解析。</summary>
    [Fact]
    public void Multiple_Courses_In_Same_Cell_Are_All_Parsed()
    {
        var content = CourseDiv("课程甲", weeks: "2-16周") + CourseDiv("课程乙", weeks: "3-15周(单)");
        var html = Page(2, Row(2, 3, 4, content));

        var timetable = Parser.Parse(html);

        Assert.Equal(2, timetable.Courses.Count);
        var first = timetable.Courses.Single(c => c.Name == "课程甲").Sessions.Single();
        var second = timetable.Courses.Single(c => c.Name == "课程乙").Sessions.Single();
        Assert.Equal((2, 3, 4), (first.DayOfWeek, first.StartPeriod, first.EndPeriod));
        Assert.Equal((2, 3, 4), (second.DayOfWeek, second.StartPeriod, second.EndPeriod));
        Assert.Equal(WeekParity.All, first.Weeks.Parity);
        Assert.Equal(WeekParity.Odd, second.Weeks.Parity);
    }

    /// <summary>rowspan 续行没有节次 id 时，沿用上方单元格的节次。</summary>
    [Fact]
    public void Rowspan_Carries_Slot_To_Following_Rows()
    {
        var rows = $"""<tr>{SlotCell(1, 1, 2, rowspan: 2)}{ContentCell(CourseDiv("课程甲"))}</tr><tr>{ContentCell(CourseDiv("课程乙"))}</tr>""";

        var timetable = Parser.Parse(Page(1, rows));

        Assert.Equal(2, timetable.Courses.Count);
        Assert.All(timetable.Courses, c =>
        {
            var session = c.Sessions.Single();
            Assert.Equal(1, session.DayOfWeek);
            Assert.Equal((1, 2), (session.StartPeriod, session.EndPeriod));
        });
    }

    /// <summary>表尾「其它课程」行没有节次 id，不应产生任何课程。</summary>
    [Fact]
    public void OtherCourses_Row_Produces_No_Course()
    {
        var rows = Row(1, 1, 2, CourseDiv("正常课程"))
                   + """<tr><td colspan="9" style="text-align:left;"><div class="timetable_title"><span class="red">其它课程：</span><span>形势与政策（五）★王鹤(共4周)/1-4周/无;</span></div></td></tr>""";

        var timetable = Parser.Parse(Page(1, rows));

        var course = Assert.Single(timetable.Courses);
        Assert.Equal("正常课程", course.Name);
    }

    /// <summary>字体颜色决定选课状态：蓝色已选上、红色待筛选。</summary>
    [Theory]
    [InlineData("blue", SelectionState.Selected)]
    [InlineData("red", SelectionState.Pending)]
    [InlineData(null, SelectionState.Unknown)]
    public void Selection_State_Comes_From_Title_Color(string? color, SelectionState expected)
    {
        var div = color is null
            ? """<div class="timetable_con text-left"><span class="title">无颜色课程</span><p><font> 周数：2-16周</font></p></div>"""
            : CourseDiv("有颜色课程", color: color);
        var html = Page(1, Row(1, 1, 2, div));

        Assert.Equal(expected, Parser.Parse(html).Courses.Single().Selection);
    }

    /// <summary>【调】前缀与多个类型标记都被剥离，且保留顺序。</summary>
    [Fact]
    public void AdjustPrefix_And_Multiple_TypeMarks_Are_Split_From_Name()
    {
        var html = Page(1, Row(1, 1, 2, CourseDiv("【调】Web前端开发◆★", jxbId: "5584FEF3A98AEDD4E065000000000001")));

        var course = Assert.Single(Parser.Parse(html).Courses);
        var session = course.Sessions.Single();

        Assert.Equal("Web前端开发", course.Name);
        Assert.Equal("◆★", session.TypeMark);
        Assert.True(session.IsAdjusted);
        Assert.Equal("5584FEF3A98AEDD4E065000000000001", session.JxbId);
    }

    /// <summary>缺少 / 空周数字段时退化为整学期（1-20 周）。</summary>
    [Fact]
    public void Missing_Weeks_Falls_Back_To_Full_Semester()
    {
        var html = Page(1, Row(1, 1, 2, CourseDiv("无周数课程", weeks: string.Empty)));

        var session = Parser.Parse(html).Courses.Single().Sessions.Single();

        Assert.Equal((1, 20), (session.Weeks.StartWeek, session.Weeks.EndWeek));
    }

    /// <summary>同一天、同节次、同周次、同标记的重复块只保留一个时段。</summary>
    [Fact]
    public void Identical_Sessions_Are_Deduplicated()
    {
        var rows = Row(1, 1, 2, CourseDiv("课程甲")) + Row(1, 1, 2, CourseDiv("课程甲"));

        var course = Assert.Single(Parser.Parse(Page(1, rows)).Courses);

        Assert.Single(course.Sessions);
    }

    /// <summary>同名但不同教师视为不同课程（颜色/详情互不干扰）。</summary>
    [Fact]
    public void SameName_DifferentTeacher_Are_Different_Courses()
    {
        var content = CourseDiv("大学英语", teacher: "甲老师") + CourseDiv("大学英语", teacher: "乙老师");
        var html = Page(3, Row(3, 5, 6, content));

        var timetable = Parser.Parse(html);

        Assert.Equal(2, timetable.Courses.Count);
        Assert.Equal(["甲老师", "乙老师"], timetable.Courses.Select(c => c.Teacher).OrderBy(t => t).ToArray());
    }

    /// <summary>空 HTML / 非课表页面不抛异常，返回空表。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("<html><body>没有课表</body></html>")]
    [InlineData("随便一段文字")]
    public void NonTimetable_Input_Returns_Empty_Timetable(string html)
    {
        var timetable = Parser.Parse(html);

        Assert.Empty(timetable.Courses);
        Assert.True(timetable.IsEmpty);
    }

    /// <summary>缺少 <c>#kblist_table</c> 时整体退化解析（不因缺表而失败）。</summary>
    [Fact]
    public void Missing_Table_Id_Still_Parses_Body()
    {
        var html = $"""
            <html><body><table><tbody id="xq_4"><tr>{SlotCell(4, 1, 2)}{ContentCell(CourseDiv("降级课程"))}</tr></tbody></table></body></html>
            """;

        var course = Assert.Single(Parser.Parse(html).Courses);
        Assert.Equal(4, course.Sessions.Single().DayOfWeek);
    }
}
