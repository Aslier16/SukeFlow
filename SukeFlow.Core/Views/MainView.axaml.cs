using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using SukeFlow.Core.Controls;
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

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 启动时载入本地已保存的课表
        if (DataContext is MainViewModel viewModel)
        {
            _ = viewModel.InitializeAsync();
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
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
}
