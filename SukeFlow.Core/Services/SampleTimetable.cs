using System;
using System.IO;
using System.Text;
using SukeFlow.Core.Models;

namespace SukeFlow.Core.Services;

/// <summary>
/// 内置示例课表（<c>Assets/个人课表查询.html</c> 嵌入资源），用于开发期显示与验证。
/// </summary>
public static class SampleTimetable
{
    /// <summary>嵌入资源名。</summary>
    public const string ResourceName = "SukeFlow.Core.Assets.SampleTimetable.html";

    /// <summary>读取示例 HTML 文本。</summary>
    public static string ReadHtml()
    {
        using var stream = typeof(SampleTimetable).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"未找到嵌入资源：{ResourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>解析示例 HTML。</summary>
    public static Timetable Parse() => new ZfsoftTimetableParser().Parse(ReadHtml());
}
