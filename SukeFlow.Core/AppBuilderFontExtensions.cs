using Avalonia;
using Avalonia.Media;

namespace SukeFlow.Core;

/// <summary>
/// 字体配置：内嵌 Noto Sans SC（中文）+ Inter（拉丁），保证 WASM 等无系统中文字体的平台正常显示中文。
/// </summary>
public static class AppBuilderFontExtensions
{
    /// <summary>内嵌中文字体资源 URI（Noto Sans SC，OFL 许可，见 Assets/Fonts/LICENSE-OFL.txt）。</summary>
    public const string ChineseFontUri =
        "avares://SukeFlow.Core/Assets/Fonts/NotoSansSC-Regular.otf#Noto Sans SC";

    /// <summary>Inter 字体资源 URI（由 Avalonia.Fonts.Inter 提供）。</summary>
    public const string LatinFontUri =
        "avares://Avalonia.Fonts.Inter/Assets#Inter";

    /// <summary>
    /// 配置 SukeFlow 字体：默认拉丁字形用 Inter，中文通过字体回退使用内嵌 Noto Sans SC。
    /// </summary>
    public static AppBuilder UseSukeFlowFonts(this AppBuilder builder)
    {
        var chinese = new FontFamily(ChineseFontUri);
        return builder
            .WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = LatinFontUri,
                FontFallbacks =
                [
                    new FontFallback { FontFamily = chinese },
                ],
            });
    }
}
