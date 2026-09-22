using System;
using Avalonia.Media;

namespace SukeFlow.Core.Controls;

/// <summary>
/// 课程卡片配色：按课程名哈希取固定颜色，并提供淡化色（非本周）与文字色。
/// 颜色取自参考图 <c>CourseTableRef.png</c> 的柔和粉彩色板。
/// </summary>
internal static class CoursePalette
{
    /// <summary>页面背景色（用于淡化混色）。</summary>
    public static readonly Color PageBackground = Color.Parse("#E5E7F5");

    /// <summary>强调色（今天高亮）。</summary>
    public static readonly Color Accent = Color.Parse("#4C6FE8");

    /// <summary>次要文字色（时间、星期等）。</summary>
    public static readonly Color MutedText = Color.Parse("#8A90A6");

    private static readonly Color[] Colors =
    [
        Color.Parse("#EC9E8B"), // 鲑红
        Color.Parse("#8EC2EF"), // 天蓝
        Color.Parse("#8AE6D6"), // 薄荷
        Color.Parse("#91B5F4"), // 长春花蓝
        Color.Parse("#E4A4B9"), // 粉
        Color.Parse("#BFABF4"), // 淡紫
        Color.Parse("#76A2CF"), // 钢蓝
        Color.Parse("#DFC9B3"), // 米色
        Color.Parse("#A9D9A2"), // 嫩绿
        Color.Parse("#E8C98A"), // 沙黄
        Color.Parse("#D9B8E8"), // 丁香紫
        Color.Parse("#8FD0D9"), // 青
    ];

    private static readonly Color TextTone = Color.Parse("#1F2937");

    /// <summary>取课程固定颜色。</summary>
    public static Color For(string key) => Colors[StableHash(key) % (uint)Colors.Length];

    /// <summary>非本周卡片的淡化底色。</summary>
    public static Color Fade(Color color) => Blend(Blend(color, Gray(color), 0.5), PageBackground, 0.3);

    /// <summary>正常卡片的文字色（卡片同色系深色）。</summary>
    public static Color DarkText(Color color) => Blend(color, TextTone, 0.68);

    /// <summary>淡化卡片的文字色。</summary>
    public static Color FadedText(Color color) => Blend(Fade(color), TextTone, 0.45);

    /// <summary>线性混色，<paramref name="t"/> 为 <paramref name="b"/> 的占比。</summary>
    public static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    private static Color Gray(Color color)
    {
        var luminance = (byte)Math.Round(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
        return Color.FromRgb(luminance, luminance, luminance);
    }

    /// <summary>FNV-1a：稳定哈希（string.GetHashCode 跨进程随机，不能用于配色）。</summary>
    private static uint StableHash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in text)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
