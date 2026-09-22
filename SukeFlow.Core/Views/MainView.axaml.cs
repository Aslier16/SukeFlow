using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SukeFlow.Core.Controls;
using SukeFlow.Core.Services;
using SukeFlow.Core.ViewModels;

namespace SukeFlow.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Table.CourseTapped += OnCourseTapped;
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 启动时载入本地已保存的课表（无数据时保持空表）
        if (DataContext is MainViewModel viewModel)
        {
            _ = viewModel.InitializeAsync();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 详情关闭时取消卡片选中高亮（面板滑出动画由 XAML 过渡完成）
        if (e.PropertyName == nameof(MainViewModel.IsDetailOpen) &&
            DataContext is MainViewModel { IsDetailOpen: false })
        {
            Table.ClearSelection();
        }
    }

    private void OnCourseTapped(object? sender, CourseTappedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ShowCourse(e.Course);
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CloseDetail();
        }
    }

    private void OnImportScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CloseImportCommand.Execute(null);
        }
    }

    // ------------------------------------------------------------ 导入：文件选择

    /// <summary>
    /// 直接选择 HTML 文件导入（手机端推荐：输入框粘贴超长 HTML 会受输入控件长度限制）。
    /// 三端均由 Avalonia StorageProvider 支持（桌面 → 文件对话框，Android → SAF，WASM → file input）。
    /// </summary>
    private async void OnPickHtmlFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanOpen)
        {
            viewModel.SetImportStatus("当前平台不支持文件选择，请改用粘贴 HTML 源码");
            return;
        }

        try
        {
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择课表 HTML 文件",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("网页 / 文本")
                    {
                        Patterns = ["*.html", "*.htm", "*.txt"],
                        MimeTypes = ["text/html", "text/plain"],
                    },
                    FilePickerFileTypes.All,
                ],
            });

            if (files.Count == 0)
            {
                return;
            }

            var file = files[0];
            viewModel.SetImportStatus($"已选择 {file.Name}，正在解析…");

            await using var stream = await file.OpenReadAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            await viewModel.ImportFromTextAsync(HtmlTextDecoder.Decode(buffer.ToArray()), $"文件「{file.Name}」");
        }
        catch (Exception ex)
        {
            viewModel.SetImportStatus($"读取文件失败：{ex.Message}");
        }
    }

    // ------------------------------------------------------------ 输入框上下文菜单

    private void OnCutClick(object? sender, RoutedEventArgs e) => ImportHtmlBox.Cut();

    private void OnCopyClick(object? sender, RoutedEventArgs e) => ImportHtmlBox.Copy();

    private void OnPasteClick(object? sender, RoutedEventArgs e) => ImportHtmlBox.Paste();

    private void OnSelectAllClick(object? sender, RoutedEventArgs e) => ImportHtmlBox.SelectAll();
}
