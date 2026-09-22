using System.Text;
using SukeFlow.Core.Services;
using Xunit;

namespace SukeFlow.Tests;

/// <summary>课表 HTML 文件字节解码（UTF-8 / BOM / 非 UTF-8 兜底）。</summary>
public class HtmlTextDecoderTests
{
    /// <summary>UTF-8 内容（无 BOM）原样还原，示例课表字节可完整解析。</summary>
    [Fact]
    public void Decodes_Utf8_Without_Bom()
    {
        var original = SampleTimetable.ReadHtml();
        var bytes = Encoding.UTF8.GetBytes(original);

        Assert.Equal(original, HtmlTextDecoder.Decode(bytes));
    }

    /// <summary>带 UTF-8 BOM 时去掉 BOM，不影响解析。</summary>
    [Fact]
    public void Strips_Utf8_Bom()
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes("<html>中文</html>");

        var text = HtmlTextDecoder.Decode(bytes);

        Assert.Equal("<html>中文</html>", text);
        Assert.DoesNotContain('\uFEFF', text);
    }

    /// <summary>空输入返回空字符串。</summary>
    [Fact]
    public void Empty_Input_Returns_Empty_String()
    {
        Assert.Equal(string.Empty, HtmlTextDecoder.Decode([]));
    }

    /// <summary>非 UTF-8 字节不抛异常（优先 GB18030，平台不支持时退化为宽松 UTF-8）。</summary>
    [Fact]
    public void NonUtf8_Bytes_Do_Not_Throw()
    {
        // GBK 编码的「课程表」
        byte[] gbk = [0xBF, 0xCE, 0xB3, 0xCC, 0xB1, 0xED];

        var text = HtmlTextDecoder.Decode(gbk);

        Assert.False(string.IsNullOrEmpty(text));
    }

    /// <summary>解码后的示例课表仍能被解析器解析出 8 门课（端到端：文件 → 文本 → 解析）。</summary>
    [Fact]
    public void Decoded_Sample_Is_Parsable()
    {
        var bytes = Encoding.UTF8.GetBytes(SampleTimetable.ReadHtml());

        var timetable = new ZfsoftTimetableParser().Parse(HtmlTextDecoder.Decode(bytes));

        Assert.Equal(8, timetable.Courses.Count);
    }
}
