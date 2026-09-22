using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SukeFlow.Core.Models;
using SukeFlow.Core.Services;

namespace SukeFlow.Core.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        Timetable = SampleTimetable.Parse();

        var today = DateOnly.FromDateTime(DateTime.Today);
        CurrentWeek = Math.Clamp(Semester.GetWeek(today), 1, Semester.WeekCount);
        DisplayWeek = CurrentWeek;
    }

    /// <summary>课程表数据（导入 / 载入示例会替换）。</summary>
    [ObservableProperty]
    public partial Timetable Timetable { get; set; }

    /// <summary>学期信息（测试用默认值）。</summary>
    public SemesterInfo Semester { get; } = SemesterInfo.Default;

    /// <summary>节次时间表（默认 22 节）。</summary>
    public PeriodSchedule PeriodSchedule { get; } = PeriodSchedule.Default;

    /// <summary>实际当前周。</summary>
    public int CurrentWeek { get; }

    /// <summary>显示周次（与控件双向绑定）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeekLabel))]
    [NotifyPropertyChangedFor(nameof(WeekDateRange))]
    public partial int DisplayWeek { get; set; }

    /// <summary>是否显示课程详情底部卡片。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailSheetOffset))]
    [NotifyPropertyChangedFor(nameof(DetailOverlayOpacity))]
    public partial bool IsDetailOpen { get; set; }

    /// <summary>是否显示导入面板。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportSheetOffset))]
    [NotifyPropertyChangedFor(nameof(ImportOverlayOpacity))]
    public partial bool IsImportOpen { get; set; }

    /// <summary>详情卡片纵向位移（0 = 完全展开，>0 = 收在屏幕外），供滑入/滑出动画使用。</summary>
    public double DetailSheetOffset => IsDetailOpen ? 0 : SheetHiddenOffset;

    /// <summary>导入面板纵向位移。</summary>
    public double ImportSheetOffset => IsImportOpen ? 0 : SheetHiddenOffset;

    /// <summary>详情遮罩不透明度（0 = 隐藏，1 = 完全显示；由过渡动画插值）。</summary>
    public double DetailOverlayOpacity => IsDetailOpen ? 1 : 0;

    /// <summary>导入遮罩不透明度。</summary>
    public double ImportOverlayOpacity => IsImportOpen ? 1 : 0;

    /// <summary>导入面板中的 HTML 文本。</summary>
    [ObservableProperty]
    public partial string ImportHtml { get; set; } = string.Empty;

    /// <summary>导入 / 存储状态提示。</summary>
    [ObservableProperty]
    public partial string ImportStatus { get; set; } = string.Empty;

    /// <summary>当前数据来源说明。</summary>
    [ObservableProperty]
    public partial string DataSourceText { get; set; } = "示例数据";

    /// <summary>详情卡片中的课程。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailTitle))]
    [NotifyPropertyChangedFor(nameof(DetailSubtitle))]
    [NotifyPropertyChangedFor(nameof(DetailSessions))]
    [NotifyPropertyChangedFor(nameof(DetailMeta))]
    public partial Course? SelectedCourse { get; set; }

    /// <summary>顶栏周次文本。</summary>
    public string WeekLabel => DisplayWeek == CurrentWeek
        ? $"第 {DisplayWeek} 周"
        : $"第 {DisplayWeek} 周（非本周）";

    /// <summary>顶栏日期范围文本。</summary>
    public string WeekDateRange
    {
        get
        {
            var (start, end) = Semester.GetWeekRange(DisplayWeek);
            return start.Month == end.Month
                ? $"{start.Year}/{start.Month}/{start.Day} - {end.Day}"
                : $"{start.Year}/{start.Month}/{start.Day} - {end.Month}/{end.Day}";
        }
    }

    /// <summary>详情标题。</summary>
    public string DetailTitle => SelectedCourse?.Name ?? string.Empty;

    /// <summary>详情副标题（教师 / 地点）。</summary>
    public string DetailSubtitle
    {
        get
        {
            if (SelectedCourse is not { } course)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(course.Teacher))
            {
                parts.Add(course.Teacher);
            }

            if (!string.IsNullOrEmpty(course.Location))
            {
                parts.Add(course.Location);
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>详情的全部时段（按星期/节次排序）。</summary>
    public IReadOnlyList<CourseSession> DetailSessions => SelectedCourse is { } course
        ? course.Sessions.OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartPeriod).ToList()
        : [];

    /// <summary>详情的其他信息行。</summary>
    public string DetailMeta
    {
        get
        {
            if (SelectedCourse is not { } course)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (course.Campus is { Length: > 0 } campus)
            {
                parts.Add($"校区：{campus}");
            }

            if (course.WeeklyHours is { } weekly)
            {
                parts.Add($"周学时：{Format(weekly)}");
            }

            if (course.TotalHours is { } total)
            {
                parts.Add($"总学时：{Format(total)}");
            }

            if (course.Credits is { } credits)
            {
                parts.Add($"学分：{Format(credits)}");
            }

            if (course.AssessmentMethod is { Length: > 0 } assessment)
            {
                parts.Add($"考核方式：{assessment}");
            }

            if (course.ClassComposition is { Length: > 0 } composition)
            {
                parts.Add($"教学班组成：{composition}");
            }

            return string.Join(Environment.NewLine, parts);
        }
    }

    /// <summary>启动时载入本地已保存的课表（没有则保留示例数据）。</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var json = await AppStorage.Timetable.LoadAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(json) &&
                TimetableSerializer.Deserialize(json) is { } saved &&
                !saved.IsEmpty)
            {
                Timetable = saved;
                DataSourceText = "本地已保存的课表";
                ImportStatus = $"已载入本地课表：{saved.Courses.Count} 门课程";
            }
        }
        catch (Exception ex)
        {
            ImportStatus = $"读取本地数据失败：{ex.Message}";
        }
    }

    /// <summary>选中课程（由控件点击事件调用）。</summary>
    public void ShowCourse(Course course)
    {
        IsImportOpen = false;
        SelectedCourse = course;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void PreviousWeek() => DisplayWeek = Semester.Normalize(DisplayWeek - 1);

    [RelayCommand]
    private void NextWeek() => DisplayWeek = Semester.Normalize(DisplayWeek + 1);

    [RelayCommand]
    private void GoToCurrentWeek() => DisplayWeek = CurrentWeek;

    [RelayCommand]
    public void CloseDetail()
    {
        IsDetailOpen = false;
        SelectedCourse = null;
    }

    /// <summary>打开导入面板。</summary>
    [RelayCommand]
    private void OpenImport()
    {
        IsDetailOpen = false;
        SelectedCourse = null;
        IsImportOpen = true;
    }

    /// <summary>关闭导入面板。</summary>
    [RelayCommand]
    private void CloseImport() => IsImportOpen = false;

    /// <summary>解析粘贴的正方教务 HTML 并保存。</summary>
    [RelayCommand]
    private async Task ImportFromHtmlAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportHtml))
        {
            ImportStatus = "请先粘贴「个人课表查询」页面的 HTML 源码";
            return;
        }

        try
        {
            var parsed = new ZfsoftTimetableParser().Parse(ImportHtml);
            if (parsed.IsEmpty)
            {
                ImportStatus = "未解析到课程，请确认粘贴的是教务系统「个人课表查询」页面源码";
                return;
            }

            Timetable = parsed;
            SelectedCourse = null;
            IsDetailOpen = false;
            DataSourceText = "本地已保存的课表";

            await AppStorage.Timetable.SaveAsync(TimetableSerializer.Serialize(parsed)).ConfigureAwait(true);

            var sessions = parsed.Courses.Sum(c => c.Sessions.Count);
            ImportStatus = $"解析成功：{parsed.Courses.Count} 门课程 / {sessions} 个时段，已保存到本地";
            IsImportOpen = false;
        }
        catch (Exception ex)
        {
            ImportStatus = $"解析失败：{ex.Message}";
        }
    }

    /// <summary>载入内置示例课表并保存。</summary>
    [RelayCommand]
    private async Task LoadSampleAsync()
    {
        var sample = SampleTimetable.Parse();
        Timetable = sample;
        SelectedCourse = null;
        IsDetailOpen = false;
        DataSourceText = "示例数据";

        try
        {
            await AppStorage.Timetable.SaveAsync(TimetableSerializer.Serialize(sample)).ConfigureAwait(true);
            ImportStatus = $"已载入示例课表（{sample.Courses.Count} 门课程）并保存到本地";
            IsImportOpen = false;
        }
        catch (Exception ex)
        {
            ImportStatus = $"示例课表已载入，但保存失败：{ex.Message}";
        }
    }

    /// <summary>清除本地保存的数据并恢复示例课表。</summary>
    [RelayCommand]
    private async Task ClearSavedAsync()
    {
        Timetable = SampleTimetable.Parse();
        SelectedCourse = null;
        IsDetailOpen = false;
        DataSourceText = "示例数据";

        try
        {
            await AppStorage.Timetable.ClearAsync().ConfigureAwait(true);
            ImportStatus = "已清除本地数据，恢复为示例课表";
        }
        catch (Exception ex)
        {
            ImportStatus = $"清除本地数据失败：{ex.Message}";
        }
    }

    /// <summary>底部面板收起时的停靠位移（需大于面板最大高度）。</summary>
    private const double SheetHiddenOffset = 720;

    private static string Format(double value) =>
        value == Math.Floor(value)
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);

}
