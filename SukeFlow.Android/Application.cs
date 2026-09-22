using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using SukeFlow.Core;
using SukeFlow.Core.Services;

namespace SukeFlow.Android;

[Application]
public class Application : AvaloniaAndroidApplication<App>
{
    protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Android：课表存到应用私有目录（卸载应用时随数据一起清除）
        AppStorage.Timetable = new FileTimetableStorage(
            System.IO.Path.Combine(FilesDir!.AbsolutePath, "timetable.json"));

        return base.CustomizeAppBuilder(builder)
            .UseSukeFlowFonts();
    }
}
