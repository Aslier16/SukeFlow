using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using SukeFlow.Browser;
using SukeFlow.Core;
using SukeFlow.Core.Services;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        // WASM 使用 localStorage 持久化课表
        AppStorage.Timetable = new LocalStorageTimetableStorage();

        return BuildAvaloniaApp()
            .UseSukeFlowFonts()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
