using Avalonia;
using System;
using System.IO;
using SukeFlow.Core;
using SukeFlow.Core.Services;

namespace SukeFlow.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 课表默认存到应用目录下的 data/，保持用户目录清洁；
        // 若应用目录不可写（如安装到只读位置），降级到用户目录。
        AppStorage.Timetable = new FileTimetableStorage(
            Path.Combine(AppContext.BaseDirectory, "data", "timetable.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SukeFlow",
                "timetable.json"));

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .UseSukeFlowFonts()
            .LogToTrace();
}
