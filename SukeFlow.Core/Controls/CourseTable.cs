using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using SukeFlow.Core.Models;

namespace SukeFlow.Core.Controls;

/// <summary>
/// 自绘课程表控件：日期表头 + 节次时间列 + 课程卡片网格。
/// 支持纵向滚动、左右滑动切换周次（相邻周内容跟随滑动、松手后带过渡动画）、点击课程卡片。
/// 视觉规格见 AGENTS.md §7（参考 <c>Assets/CourseTableRef.png</c>）。
/// </summary>
public class CourseTable : Control
{
    // ---------------------------------------------------------------- 布局常量

    private const double TimeColumnWidth = 36;
    private const double HeaderHeight = 70;
    private const double RowHeight = 90;
    private const double CardGap = 4;
    private const double SplitGap = 3;
    private const double CardRadius = 6;
    private const double CardPadding = 6;

    private const double NameFontSize = 17;
    private const double DetailFontSize = 13;
    private const double BadgeFontSize = 10;
    private const double HeaderWeekdayFontSize = 11;
    private const double HeaderDateFontSize = 13.5;
    private const double TimeFontSize = 11;
    private const double MonthFontSize = 11;

    private const double DragAxisThreshold = 8;

    /// <summary>松手后判定切换周次的位移比例（相对控件宽度）。</summary>
    private const double SwipeCommitFraction = 0.22;

    private const double SwipeCommitDurationMs = 220;
    private const double SwipeCancelDurationMs = 170;
    private const double WeekSlideDurationMs = 240;
    private const double AnimationFrameMs = 16;

    private static readonly Typeface NameTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface BodyTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface DateTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    private static readonly IBrush WhiteBrush = new ImmutableSolidColorBrush(Colors.White);

    private static readonly Geometry LocationIcon = StreamGeometry.Parse(
        "M5,10.5 L2.3,6.2 A3.1,3.1 0 1 1 7.7,6.2 Z");

    private static readonly Geometry PersonIcon = StreamGeometry.Parse(
        "M5,1.2 A2.7,2.7 0 1 1 4.99,1.2 Z M0.6,10.6 A4.4,4.4 0 0 1 9.4,10.6 Z");

    // ---------------------------------------------------------------- 状态

    /// <summary>按周次缓存的页面内容（滑动时同时需要相邻两周）。</summary>
    private readonly Dictionary<int, List<DrawItem>> _pageCache = [];

    private bool _cacheDirty = true;
    private bool _suppressWeekAnimation;
    private double _offsetY;

    /// <summary>横向页面位移：0 = 当前周居中；负值 = 向左拖动（露出下一周）。</summary>
    private double _weekOffset;

    private CourseSession? _selectedSession;

    private bool _pressed;
    private bool _axisLocked;
    private bool _horizontalDrag;
    private Point _pressPoint;
    private double _offsetAtPress;

    // 滑动 / 换页动画
    private DispatcherTimer? _animationTimer;
    private double _animFrom;
    private double _animTo;
    private double _animDurationMs;
    private double _animElapsedMs;
    private int _animCommitDirection;

    // ---------------------------------------------------------------- 属性

    /// <summary>课程表数据。</summary>
    public static readonly StyledProperty<Timetable?> TimetableProperty =
        AvaloniaProperty.Register<CourseTable, Timetable?>(nameof(Timetable));

    /// <summary>学期信息（用于日期换算与周次循环）。</summary>
    public static readonly StyledProperty<SemesterInfo?> SemesterProperty =
        AvaloniaProperty.Register<CourseTable, SemesterInfo?>(nameof(Semester));

    /// <summary>节次时间表。</summary>
    public static readonly StyledProperty<PeriodSchedule?> PeriodScheduleProperty =
        AvaloniaProperty.Register<CourseTable, PeriodSchedule?>(nameof(PeriodSchedule));

    /// <summary>当前显示的周次（手势切换时回写）。</summary>
    public static readonly StyledProperty<int> DisplayWeekProperty =
        AvaloniaProperty.Register<CourseTable, int>(nameof(DisplayWeek), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>实际当前周次（用于「今天」高亮），-1 表示未知。</summary>
    public static readonly StyledProperty<int> CurrentWeekProperty =
        AvaloniaProperty.Register<CourseTable, int>(nameof(CurrentWeek), -1);

    public Timetable? Timetable
    {
        get => GetValue(TimetableProperty);
        set => SetValue(TimetableProperty, value);
    }

    public SemesterInfo? Semester
    {
        get => GetValue(SemesterProperty);
        set => SetValue(SemesterProperty, value);
    }

    public PeriodSchedule? PeriodSchedule
    {
        get => GetValue(PeriodScheduleProperty);
        set => SetValue(PeriodScheduleProperty, value);
    }

    public int DisplayWeek
    {
        get => GetValue(DisplayWeekProperty);
        set => SetValue(DisplayWeekProperty, value);
    }

    public int CurrentWeek
    {
        get => GetValue(CurrentWeekProperty);
        set => SetValue(CurrentWeekProperty, value);
    }

    /// <summary>点击课程卡片时触发。</summary>
    public event EventHandler<CourseTappedEventArgs>? CourseTapped;

    static CourseTable()
    {
        AffectsRender<CourseTable>(
            TimetableProperty,
            SemesterProperty,
            PeriodScheduleProperty,
            DisplayWeekProperty,
            CurrentWeekProperty);
    }

    public CourseTable()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TimetableProperty || change.Property == PeriodScheduleProperty)
        {
            _cacheDirty = true;
            _pageCache.Clear();
        }
        else if (change.Property == DisplayWeekProperty)
        {
            _selectedSession = null;
            if (!_suppressWeekAnimation)
            {
                // 外部（按钮 / 方向键）切换周次时，让相邻周内容平滑滑入
                AnimateWeekChange(change.GetOldValue<int>(), change.GetNewValue<int>());
            }
        }
    }

    /// <summary>清除卡片选中高亮。</summary>
    public void ClearSelection()
    {
        if (_selectedSession is null)
        {
            return;
        }

        _selectedSession = null;
        InvalidateVisual();
    }

    // ---------------------------------------------------------------- 手势与输入

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed && e.Pointer.Type != PointerType.Touch)
        {
            return;
        }

        // 正在滑入时按下：停止动画，从当前位置继续拖动
        _animationTimer?.Stop();
        _animCommitDirection = 0;

        Focus();
        _pressed = true;
        _axisLocked = false;
        _horizontalDrag = false;
        _pressPoint = point.Position;
        _offsetAtPress = _offsetY;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var dx = position.X - _pressPoint.X;
        var dy = position.Y - _pressPoint.Y;

        if (!_axisLocked)
        {
            if (Math.Abs(dx) < DragAxisThreshold && Math.Abs(dy) < DragAxisThreshold)
            {
                return;
            }

            _axisLocked = true;
            _horizontalDrag = Math.Abs(dx) > Math.Abs(dy);
        }

        if (_horizontalDrag)
        {
            // 1:1 跟手滑动（相邻周内容一起移动），最多一页
            _weekOffset = Math.Clamp(dx, -Bounds.Width, Bounds.Width);
        }
        else
        {
            _offsetY = ClampOffset(_offsetAtPress - dy);
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }

        _pressed = false;

        if (_horizontalDrag && Bounds.Width > 0)
        {
            var width = Bounds.Width;
            var offset = _weekOffset;

            if (Math.Abs(offset) >= width * SwipeCommitFraction)
            {
                // 提交切换：滑完整页，动画结束后替换周次（画面无跳变）
                var direction = offset < 0 ? 1 : -1;
                StartAnimation(-direction * width, SwipeCommitDurationMs, direction);
                return;
            }

            if (Math.Abs(offset) > 0.5)
            {
                StartAnimation(0, SwipeCancelDurationMs, 0);
                return;
            }
        }

        if (!_axisLocked)
        {
            HandleTap(e.GetPosition(this));
        }

        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        _weekOffset = 0;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var delta = e.Delta.Y * 60;
        var next = ClampOffset(_offsetY - delta);
        if (Math.Abs(next - _offsetY) > 0.01)
        {
            _offsetY = next;
            InvalidateVisual();
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                SwitchWeek(-1);
                e.Handled = true;
                break;
            case Key.Right:
                SwitchWeek(1);
                e.Handled = true;
                break;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    private void SwitchWeek(int delta)
    {
        var semester = Semester;
        var week = DisplayWeek + delta;
        if (semester is { WeekCount: > 0 })
        {
            week = semester.Normalize(week);
        }

        SetCurrentValue(DisplayWeekProperty, week);
        InvalidateVisual();
    }

    private void HandleTap(Point position)
    {
        var hit = HitTestEntry(position);
        _selectedSession = hit?.Session;
        InvalidateVisual();

        if (hit is { } entry)
        {
            CourseTapped?.Invoke(this, new CourseTappedEventArgs(entry.Course, entry.Session));
        }
    }

    private CellEntry? HitTestEntry(Point position)
    {
        var schedule = PeriodSchedule;
        if (Timetable is null || schedule is null || Bounds.Width <= TimeColumnWidth)
        {
            return null;
        }

        // 滑动 / 动画过程中不响应点击
        if (Math.Abs(_weekOffset) > 0.5)
        {
            return null;
        }

        var bodyY = position.Y - HeaderHeight + _offsetY;
        if (bodyY < 0)
        {
            return null;
        }

        var period = (int)(bodyY / RowHeight) + 1;
        if (period < 1 || period > schedule.Count)
        {
            return null;
        }

        var dayWidth = (Bounds.Width - TimeColumnWidth) / 7.0;
        var x = position.X - TimeColumnWidth;
        if (x < 0 || dayWidth <= 0)
        {
            return null;
        }

        var day = (int)(x / dayWidth) + 1;
        if (day is < 1 or > 7)
        {
            return null;
        }

        var chosen = ChooseEntries(DisplayWeek, day, period);
        if (chosen is not { Count: > 0 })
        {
            return null;
        }

        if (chosen.Count == 1)
        {
            return chosen[0];
        }

        var index = (int)(x % dayWidth / dayWidth * chosen.Count);
        return chosen[Math.Clamp(index, 0, chosen.Count - 1)];
    }

    // ---------------------------------------------------------------- 换页动画

    /// <summary>周次变化（按钮/方向键）时的滑入动画。</summary>
    private void AnimateWeekChange(int oldWeek, int newWeek)
    {
        var delta = newWeek - oldWeek;
        if (Semester is { WeekCount: > 0 } semester)
        {
            var count = semester.WeekCount;
            if (delta == 1 - count)
            {
                delta = 1;
            }
            else if (delta == count - 1)
            {
                delta = -1;
            }
        }

        TrimPageCache();

        if (delta is 1 or -1 && Bounds.Width > 0)
        {
            // 起始时把旧的一周摆在中间，然后滑到新的一周
            _weekOffset = delta * Bounds.Width;
            StartAnimation(0, WeekSlideDurationMs, 0);
        }
        else
        {
            _weekOffset = 0;
            InvalidateVisual();
        }
    }

    private void StartAnimation(double to, double durationMs, int commitDirection)
    {
        _animFrom = _weekOffset;
        _animTo = to;
        _animDurationMs = durationMs;
        _animElapsedMs = 0;
        _animCommitDirection = commitDirection;

        _animationTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(AnimationFrameMs), DispatcherPriority.Render, OnAnimationTick);
        _animationTimer.Start();
        InvalidateVisual();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _animElapsedMs += AnimationFrameMs;
        var progress = _animDurationMs <= 0 ? 1 : Math.Clamp(_animElapsedMs / _animDurationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3); // ease-out cubic

        _weekOffset = _animFrom + (_animTo - _animFrom) * eased;

        if (progress >= 1)
        {
            _animationTimer?.Stop();
            _weekOffset = 0;

            if (_animCommitDirection != 0)
            {
                var direction = _animCommitDirection;
                _animCommitDirection = 0;
                var target = Semester is { WeekCount: > 0 } semester
                    ? semester.Normalize(DisplayWeek + direction)
                    : DisplayWeek + direction;

                _suppressWeekAnimation = true;
                SetCurrentValue(DisplayWeekProperty, target);
                _suppressWeekAnimation = false;
                TrimPageCache();
            }
        }

        InvalidateVisual();
    }

    // ---------------------------------------------------------------- 数据组织

    /// <summary>
    /// 一个格子内应显示的课程：有当周课程时只显示当周课程，否则显示全部（先不管冲突处理）。
    /// </summary>
    private List<CellEntry>? ChooseEntries(int week, int day, int period)
    {
        if (Timetable is null)
        {
            return null;
        }

        var all = Timetable.GetCellEntries(week, day, period);
        if (all.Count == 0)
        {
            return null;
        }

        var currentCount = 0;
        while (currentCount < all.Count && all[currentCount].IsCurrentWeek)
        {
            currentCount++;
        }

        if (currentCount == 0)
        {
            return [.. all];
        }

        var result = new List<CellEntry>(currentCount);
        for (var i = 0; i < currentCount; i++)
        {
            result.Add(all[i]);
        }

        return result;
    }

    /// <summary>取某周的页面内容（带缓存）。</summary>
    private List<DrawItem> GetPageItems(int week)
    {
        if (_pageCache.TryGetValue(week, out var items))
        {
            return items;
        }

        items = BuildPage(week);
        _pageCache[week] = items;
        return items;
    }

    /// <summary>把同一列中归属相同的连续格子合并为一张卡片（纵向拆分为可见区段）。</summary>
    private List<DrawItem> BuildPage(int week)
    {
        var items = new List<DrawItem>();
        var schedule = PeriodSchedule;
        if (Timetable is null || schedule is null || schedule.Count == 0)
        {
            return items;
        }

        var count = schedule.Count;
        var cells = new List<CellEntry>?[count];

        for (var day = 1; day <= 7; day++)
        {
            for (var p = 0; p < count; p++)
            {
                cells[p] = ChooseEntries(week, day, p + 1);
            }

            var runStart = 0;
            for (var p = 1; p <= count; p++)
            {
                if (p < count && SameEntries(cells[runStart], cells[p]))
                {
                    continue;
                }

                if (cells[runStart] is { Count: > 0 } entries)
                {
                    items.Add(new DrawItem
                    {
                        Day = day,
                        StartPeriod = runStart + 1,
                        EndPeriod = p,
                        Entries = entries,
                    });
                }

                runStart = p;
            }
        }

        return items;
    }

    /// <summary>只保留当前周与相邻周的缓存。</summary>
    private void TrimPageCache()
    {
        if (_pageCache.Count <= 3 || Semester is not { WeekCount: > 0 } semester)
        {
            return;
        }

        var previous = semester.Normalize(DisplayWeek - 1);
        var next = semester.Normalize(DisplayWeek + 1);
        foreach (var key in _pageCache.Keys.ToList())
        {
            if (key != DisplayWeek && key != previous && key != next)
            {
                _pageCache.Remove(key);
            }
        }
    }

    private static bool SameEntries(List<CellEntry>? a, List<CellEntry>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null || a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i].Course, b[i].Course) || !ReferenceEquals(a[i].Session, b[i].Session))
            {
                return false;
            }
        }

        return true;
    }

    private double ClampOffset(double value)
    {
        var schedule = PeriodSchedule;
        if (schedule is null)
        {
            return 0;
        }

        var bodyHeight = Math.Max(Bounds.Height - HeaderHeight, 0);
        var contentHeight = schedule.Count * RowHeight;
        return Math.Clamp(value, 0, Math.Max(contentHeight - bodyHeight, 0));
    }

    // ---------------------------------------------------------------- 绘制

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;

        // 透明底：确保整块区域可命中指针事件
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, height));

        if (width <= TimeColumnWidth || height <= HeaderHeight)
        {
            return;
        }

        if (_cacheDirty)
        {
            _pageCache.Clear();
            _cacheDirty = false;
        }

        _offsetY = ClampOffset(_offsetY);

        var dayWidth = (width - TimeColumnWidth) / 7.0;
        var semester = Semester;
        var current = DisplayWeek;
        var previous = semester is { WeekCount: > 0 } ? semester.Normalize(current - 1) : current - 1;
        var next = semester is { WeekCount: > 0 } ? semester.Normalize(current + 1) : current + 1;

        // 表头：月份固定，星期/日期随页滑动
        DrawMonth(context, current);
        DrawPageHeader(context, dayWidth, previous, -width + _weekOffset);
        DrawPageHeader(context, dayWidth, current, _weekOffset);
        DrawPageHeader(context, dayWidth, next, width + _weekOffset);

        // 节次时间列固定
        using (context.PushClip(new Rect(0, HeaderHeight, TimeColumnWidth, height - HeaderHeight)))
        using (context.PushTransform(Matrix.CreateTranslation(0, -_offsetY)))
        {
            DrawTimeColumn(context);
        }

        // 三个周次页面（当前 + 相邻）
        var bodyClip = new Rect(TimeColumnWidth, HeaderHeight, width - TimeColumnWidth, height - HeaderHeight);
        DrawPage(context, bodyClip, dayWidth, previous, -width + _weekOffset);
        DrawPage(context, bodyClip, dayWidth, current, _weekOffset);
        DrawPage(context, bodyClip, dayWidth, next, width + _weekOffset);

        if (Timetable is null || Timetable.IsEmpty)
        {
            DrawEmptyHint(context);
        }
    }

    private void DrawPage(DrawingContext context, Rect clip, double dayWidth, int week, double dx)
    {
        if (dx <= -clip.Width || dx >= clip.Width)
        {
            return;
        }

        using (context.PushClip(clip))
        using (context.PushTransform(Matrix.CreateTranslation(dx, -_offsetY)))
        {
            DrawCards(context, dayWidth, GetPageItems(week));
        }
    }

    private void DrawPageHeader(DrawingContext context, double dayWidth, int week, double dx)
    {
        var available = Bounds.Width - TimeColumnWidth;
        if (dx <= -available || dx >= available)
        {
            return;
        }

        using (context.PushClip(new Rect(TimeColumnWidth, 0, available, HeaderHeight)))
        using (context.PushTransform(Matrix.CreateTranslation(dx, 0)))
        {
            DrawHeaderDays(context, dayWidth, week);
        }
    }

    private void DrawMonth(DrawingContext context, int week)
    {
        var semester = Semester;
        var date = semester?.GetDate(week, 1) ?? new DateOnly(2026, 9, 7);

        var month = Layout($"{date.Month}月", MonthFontSize, BodyTypeface, CoursePalette.MutedText);
        month.Draw(context, new Point((TimeColumnWidth - month.Width) / 2, HeaderHeight / 2 - month.Height / 2));
    }

    private void DrawHeaderDays(DrawingContext context, double dayWidth, int week)
    {
        var semester = Semester;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayOfWeek = week == CurrentWeek ? (int)today.DayOfWeek : -1; // DateTime: 周日=0

        for (var day = 1; day <= 7; day++)
        {
            var x = TimeColumnWidth + (day - 1) * dayWidth;
            var isToday = todayOfWeek == 0 && day == 7 || todayOfWeek == day;

            var weekday = Layout(CourseSession.DayTextOf(day), HeaderWeekdayFontSize, BodyTypeface,
                isToday ? CoursePalette.Accent : CoursePalette.MutedText);
            weekday.Draw(context, new Point(x + (dayWidth - weekday.Width) / 2, 14));

            var dayNumber = semester is not null
                ? semester.GetDate(week, day).Day.ToString(CultureInfo.InvariantCulture)
                : "—";
            var number = Layout(dayNumber, HeaderDateFontSize, DateTypeface,
                isToday ? CoursePalette.Accent : CoursePalette.DarkText(CoursePalette.Accent));
            var numberY = 38.0;
            number.Draw(context, new Point(x + (dayWidth - number.Width) / 2, numberY));

            if (isToday)
            {
                var barWidth = 14.0;
                var bar = new Rect(x + (dayWidth - barWidth) / 2, numberY + number.Height + 3, barWidth, 2.5);
                context.DrawRectangle(new ImmutableSolidColorBrush(CoursePalette.Accent), null, bar, 1.25, 1.25);
            }
        }
    }

    private void DrawTimeColumn(DrawingContext context)
    {
        var schedule = PeriodSchedule;
        if (schedule is null)
        {
            return;
        }

        foreach (var period in schedule.Periods)
        {
            var y = HeaderHeight + (period.Index - 1) * RowHeight;
            if (y + RowHeight < _offsetY || y > _offsetY + Bounds.Height)
            {
                continue;
            }

            var start = Layout(period.StartText, TimeFontSize, BodyTypeface, CoursePalette.MutedText);
            var end = Layout(period.EndText, TimeFontSize, BodyTypeface, CoursePalette.MutedText);
            var top = y + RowHeight / 2 - (start.Height + end.Height) / 2 - 1;

            start.Draw(context, new Point((TimeColumnWidth - start.Width) / 2, top));
            end.Draw(context, new Point((TimeColumnWidth - end.Width) / 2, top + start.Height));
        }
    }

    private void DrawCards(DrawingContext context, double dayWidth, List<DrawItem> items)
    {
        var bodyHeight = Bounds.Height - HeaderHeight;
        var viewTop = _offsetY;
        var viewBottom = _offsetY + bodyHeight;

        foreach (var item in items)
        {
            var y = HeaderHeight + (item.StartPeriod - 1) * RowHeight + CardGap;
            var cardHeight = (item.EndPeriod - item.StartPeriod + 1) * RowHeight - CardGap * 2;
            if (y + cardHeight < viewTop || y > viewBottom)
            {
                continue;
            }

            var columnX = TimeColumnWidth + (item.Day - 1) * dayWidth;
            var cardWidth = dayWidth - CardGap * 2;
            if (cardWidth <= 4)
            {
                continue;
            }

            var count = item.Entries.Count;
            var slotWidth = count == 1 ? cardWidth : (cardWidth - SplitGap * (count - 1)) / count;

            for (var i = 0; i < count; i++)
            {
                var x = columnX + CardGap + i * (slotWidth + SplitGap);
                DrawCard(context, item.Entries[i], new Rect(x, y, slotWidth, cardHeight));
            }
        }
    }

    private void DrawCard(DrawingContext context, CellEntry entry, Rect rect)
    {
        if (rect.Width < 12 || rect.Height < 10)
        {
            return;
        }

        var baseColor = CoursePalette.For(entry.Course.ColorKey);
        var background = entry.IsCurrentWeek ? baseColor : CoursePalette.Fade(baseColor);
        var textColor = entry.IsCurrentWeek ? CoursePalette.DarkText(baseColor) : CoursePalette.FadedText(baseColor);
        var textBrush = new ImmutableSolidColorBrush(textColor);

        context.DrawRectangle(new ImmutableSolidColorBrush(background), null, rect, CardRadius, CardRadius);

        if (ReferenceEquals(entry.Session, _selectedSession))
        {
            var pen = new Pen(new ImmutableSolidColorBrush(CoursePalette.DarkText(baseColor)), 1.6);
            context.DrawRectangle(null, pen, rect, CardRadius, CardRadius);
        }

        var showBadge = !entry.IsCurrentWeek && rect.Height >= 96;
        var badgeHeight = showBadge ? 18.0 : 0;

        var showTeacher = rect.Height >= 112;
        var detailHeight = 0.0;
        if (!string.IsNullOrEmpty(entry.Course.Location))
        {
            detailHeight += 20;
        }

        if (showTeacher && !string.IsNullOrEmpty(entry.Course.Teacher))
        {
            detailHeight += 18;
        }

        var innerWidth = rect.Width - CardPadding * 2;
        if (innerWidth <= 8)
        {
            return;
        }

        var nameHeight = rect.Height - 10 - detailHeight - badgeHeight - CardPadding;
        if (nameHeight >= 14)
        {
            var name = Layout(entry.Course.Name, NameFontSize, NameTypeface, textColor,
                maxWidth: innerWidth, maxHeight: nameHeight,
                wrapping: TextWrapping.Wrap, trimming: TextTrimming.CharacterEllipsis);
            name.Draw(context, new Point(rect.X + CardPadding, rect.Y + 5));
        }

        var detailY = rect.Bottom - CardPadding - detailHeight - badgeHeight;

        if (!string.IsNullOrEmpty(entry.Course.Location))
        {
            DrawIconLine(context, LocationIcon, entry.Course.Location, rect.X + CardPadding, detailY, innerWidth, textBrush, textColor);
            detailY += 20;
        }

        if (showTeacher && !string.IsNullOrEmpty(entry.Course.Teacher))
        {
            DrawIconLine(context, PersonIcon, entry.Course.Teacher, rect.X + CardPadding, detailY, innerWidth, textBrush, textColor);
        }

        if (showBadge)
        {
            DrawBadge(context, "非本周", rect, textColor);
        }
    }

    private void DrawIconLine(DrawingContext context, Geometry icon, string text, double x, double y,
        double maxWidth, IBrush brush, Color color)
    {
        const double iconSize = 11;
        var iconY = y + 2;

        using (context.PushTransform(Matrix.CreateTranslation(x, iconY)))
        {
            context.DrawGeometry(brush, null, icon);
        }

        var formatted = Layout(text, DetailFontSize, BodyTypeface, color,
            maxWidth: Math.Max(maxWidth - iconSize - 3, 8), trimming: TextTrimming.CharacterEllipsis);
        formatted.Draw(context, new Point(x + iconSize + 3, y));
    }

    private static void DrawBadge(DrawingContext context, string text, Rect card, Color textColor)
    {
        var badge = Layout(text, BadgeFontSize, BodyTypeface, textColor,
            maxWidth: Math.Max(card.Width - 12, 8), trimming: TextTrimming.CharacterEllipsis);
        var height = 15.0;
        var width = Math.Min(badge.Width + 10, card.Width - 8);
        var pill = new Rect(card.X + (card.Width - width) / 2, card.Bottom - height - 4, width, height);
        var radius = height / 2;

        context.DrawRectangle(new ImmutableSolidColorBrush(Colors.White, 0.5), null, pill, radius, radius);
        badge.Draw(context, new Point(pill.X + (pill.Width - badge.Width) / 2, pill.Y + (pill.Height - badge.Height) / 2));
    }

    private void DrawEmptyHint(DrawingContext context)
    {
        var hint = Layout("暂无课程数据", 14, BodyTypeface, CoursePalette.MutedText);
        hint.Draw(context, new Point((Bounds.Width - hint.Width) / 2, HeaderHeight + 40));
    }

    private static TextLayout Layout(string text, double size, Typeface typeface, Color color,
        double maxWidth = double.PositiveInfinity,
        double maxHeight = double.PositiveInfinity,
        TextWrapping wrapping = TextWrapping.NoWrap,
        TextTrimming? trimming = null) =>
        new(text, typeface, size, new ImmutableSolidColorBrush(color), TextAlignment.Left, wrapping,
            trimming, null, FlowDirection.LeftToRight, maxWidth, maxHeight);

    /// <summary>一列中连续若干节的同组卡片。</summary>
    private sealed class DrawItem
    {
        public required int Day { get; init; }

        public required int StartPeriod { get; init; }

        public required int EndPeriod { get; init; }

        public required List<CellEntry> Entries { get; init; }
    }
}
