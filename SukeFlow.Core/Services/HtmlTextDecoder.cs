using System;
using System.Text;

namespace SukeFlow.Core.Services;

/// <summary>
/// 课表 HTML 文件字节 → 文本的解码器。
/// 优先 UTF-8（含 BOM），其次尝试 GB18030（教务系统旧页面可能是 GBK），最后退化为宽松 UTF-8。
/// </summary>
public static class HtmlTextDecoder
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>解码文件字节；任何情况下都不抛异常（失败返回宽松解码结果）。</summary>
    public static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        // UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // 不是合法 UTF-8，继续尝试其它编码
        }

        try
        {
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            // 该平台没有代码页支持（如浏览器/WASM），退化为宽松 UTF-8
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
