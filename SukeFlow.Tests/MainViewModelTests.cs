using System.Linq;
using System.Threading.Tasks;
using SukeFlow.Core.Services;
using SukeFlow.Core.ViewModels;
using Xunit;

namespace SukeFlow.Tests;

/// <summary>
/// 主视图模型：初始空表、导入（粘贴 / 文件文本）、清除数据、清空输入框。
/// 这些用例会替换 <see cref="AppStorage.Timetable"/>，故归入同一 collection 串行执行。
/// </summary>
[Collection("app-storage")]
public class MainViewModelTests
{
    /// <summary>内存存储替身。</summary>
    private sealed class MemoryStorage : ITimetableStorage
    {
        public string? Json { get; set; }

        public Task<string?> LoadAsync() => Task.FromResult(Json);

        public Task SaveAsync(string json)
        {
            Json = json;
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            Json = null;
            return Task.CompletedTask;
        }
    }

    private static (MainViewModel ViewModel, MemoryStorage Storage) Create(string? saved = null)
    {
        var storage = new MemoryStorage { Json = saved };
        AppStorage.Timetable = storage;
        return (new MainViewModel(), storage);
    }

    /// <summary>没有本地数据时初始为空课表（不预置示例课程）。</summary>
    [Fact]
    public async Task Starts_Empty_When_No_Saved_Data()
    {
        var (viewModel, _) = Create();

        Assert.True(viewModel.Timetable.IsEmpty);
        Assert.Equal("未导入课表", viewModel.DataSourceText);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.Timetable.IsEmpty);
        Assert.Equal("未导入课表", viewModel.DataSourceText);
    }

    /// <summary>有本地数据时启动载入。</summary>
    [Fact]
    public async Task Initialize_Loads_Saved_Timetable()
    {
        var json = TimetableSerializer.Serialize(SampleTimetable.Parse());
        var (viewModel, _) = Create(json);

        await viewModel.InitializeAsync();

        Assert.Equal(8, viewModel.Timetable.Courses.Count);
        Assert.Equal("本地已保存的课表", viewModel.DataSourceText);
        Assert.Contains("已载入本地课表", viewModel.ImportStatus);
    }

    /// <summary>从文本导入（模拟文件选择）：解析、替换、落盘、关闭面板。</summary>
    [Fact]
    public async Task ImportFromText_Parses_Saves_And_ClosesPanel()
    {
        var (viewModel, storage) = Create();
        viewModel.IsImportOpen = true;

        await viewModel.ImportFromTextAsync(SampleTimetable.ReadHtml(), "文件「课表.html」");

        Assert.Equal(8, viewModel.Timetable.Courses.Count);
        Assert.Equal(16, viewModel.Timetable.Courses.Sum(c => c.Sessions.Count));
        Assert.Equal("本地已保存的课表", viewModel.DataSourceText);
        Assert.Contains("解析成功", viewModel.ImportStatus);
        Assert.False(viewModel.IsImportOpen);

        Assert.NotNull(storage.Json);
        Assert.Equal(8, TimetableSerializer.Deserialize(storage.Json!)!.Courses.Count);
    }

    /// <summary>导入非法内容：保持空表、提示来自哪个来源、不写存储。</summary>
    [Fact]
    public async Task ImportFromText_Invalid_Html_Keeps_Empty_And_Reports_Source()
    {
        var (viewModel, storage) = Create();

        await viewModel.ImportFromTextAsync("<html><body>不是课表</body></html>", "文件「随手存的.html」");

        Assert.True(viewModel.Timetable.IsEmpty);
        Assert.Contains("文件「随手存的.html」", viewModel.ImportStatus);
        Assert.Null(storage.Json);
    }

    /// <summary>空文本导入给出提示。</summary>
    [Fact]
    public async Task ImportFromText_Empty_Text_Reports()
    {
        var (viewModel, _) = Create();

        await viewModel.ImportFromTextAsync("   ");

        Assert.True(viewModel.Timetable.IsEmpty);
        Assert.Contains("内容为空", viewModel.ImportStatus);
    }

    /// <summary>粘贴路径：ImportHtmlCommand 使用输入框内容。</summary>
    [Fact]
    public async Task ImportFromHtmlCommand_Uses_TextBox_Content()
    {
        var (viewModel, _) = Create();
        viewModel.ImportHtml = SampleTimetable.ReadHtml();

        await viewModel.ImportFromHtmlCommand.ExecuteAsync(null);

        Assert.Equal(8, viewModel.Timetable.Courses.Count);
        Assert.Equal(string.Empty, viewModel.ImportHtml);   // 成功后清空输入框
        Assert.Contains("解析成功", viewModel.ImportStatus);
    }

    /// <summary>清空输入框（上下文菜单「清空」）。</summary>
    [Fact]
    public void ClearImportHtml_Empties_Input()
    {
        var (viewModel, _) = Create();
        viewModel.ImportHtml = "<html>很长很长的源码</html>";
        viewModel.ImportStatus = "旧提示";

        viewModel.ClearImportHtmlCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.ImportHtml);
        Assert.Equal(string.Empty, viewModel.ImportStatus);
    }

    /// <summary>清除已保存数据 → 课表清空、存储删除。</summary>
    [Fact]
    public async Task ClearSaved_Empties_Timetable_And_Storage()
    {
        var (viewModel, storage) = Create();
        await viewModel.ImportFromTextAsync(SampleTimetable.ReadHtml());
        Assert.False(viewModel.Timetable.IsEmpty);

        await viewModel.ClearSavedCommand.ExecuteAsync(null);

        Assert.True(viewModel.Timetable.IsEmpty);
        Assert.Null(storage.Json);
        Assert.Equal("未导入课表", viewModel.DataSourceText);
        Assert.Contains("已清除", viewModel.ImportStatus);
    }

    /// <summary>周次切换在 1-20 周之间循环。</summary>
    [Fact]
    public void WeekNavigation_Wraps_Around()
    {
        var (viewModel, _) = Create();

        viewModel.DisplayWeek = 1;
        viewModel.PreviousWeekCommand.Execute(null);
        Assert.Equal(20, viewModel.DisplayWeek);

        viewModel.NextWeekCommand.Execute(null);
        Assert.Equal(1, viewModel.DisplayWeek);

        viewModel.DisplayWeek = 5;
        Assert.Equal("第 5 周（非本周）", viewModel.WeekLabel);

        viewModel.GoToCurrentWeekCommand.Execute(null);
        Assert.Equal(viewModel.CurrentWeek, viewModel.DisplayWeek);
    }
}
