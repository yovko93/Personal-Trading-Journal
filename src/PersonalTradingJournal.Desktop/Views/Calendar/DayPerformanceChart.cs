using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Dashboard;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

/// <summary>Elapsed-UTC-time step plot: changes only at actual closed Trade timestamps.</summary>
public sealed class DayPerformanceChart : UserControl
{
    private const double PlotTop = 16;
    private const double FillOpacity = .08;
    private readonly Canvas _axis = new();
    private readonly Canvas _plot = new() { Background = Brushes.Transparent };
    private readonly ScrollViewer _scroller = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private bool _rebuilding;
    private readonly List<Border> _pointTargets = [];
    private Border? _hoveredTarget, _focusedTarget, _displayedTarget;
    private Line? _activeGuide;
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(CalendarDayPerformance), typeof(DayPerformanceChart), new PropertyMetadata(null, Changed));
    public CalendarDayPerformance? Series { get => (CalendarDayPerformance?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public static readonly DependencyProperty PositiveBrushProperty = BrushProperty(nameof(PositiveBrush));
    public static readonly DependencyProperty NegativeBrushProperty = BrushProperty(nameof(NegativeBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));
    public Brush PositiveBrush { get => (Brush)GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public Brush NegativeBrush { get => (Brush)GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(DayPerformanceChart), new PropertyMetadata(Brushes.Gray, Changed));
    private static void Changed(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        var chart = (DayPerformanceChart)owner;
        if (e.Property == SeriesProperty)
        {
            chart._hoveredTarget = chart._focusedTarget = null;
            chart.UpdateActivePoint();
            chart._scroller.ScrollToHorizontalOffset(0);
        }
        chart.Rebuild();
    }

    public DayPerformanceChart()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        _scroller.Content = _plot;
        Grid.SetColumn(_scroller, 1);
        NestedTableWheelRouting.SetForwardVerticalWheel(_scroller, true);
        grid.Children.Add(_axis);
        grid.Children.Add(_scroller);
        Content = grid;
        _plot.MouseLeave += (_, _) => { _hoveredTarget = null; UpdateActivePoint(); };
        Unloaded += (_, _) =>
        {
            CloseTooltips();
            _hoveredTarget = _focusedTarget = null;
            UpdateActivePoint();
        };
        SizeChanged += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        if (_rebuilding || ActualWidth < 120 || ActualHeight < 100) return;
        _rebuilding = true;
        try
        {
            var hoveredPoint = _hoveredTarget?.Tag as CalendarDayPerformancePoint;
            var focusedPoint = _focusedTarget is not null && ReferenceEquals(Keyboard.FocusedElement, _focusedTarget)
                ? _focusedTarget.Tag as CalendarDayPerformancePoint : null;
            CloseTooltips();
            _hoveredTarget = _focusedTarget = _displayedTarget = null;
            _activeGuide = null;
            _pointTargets.Clear();
            _axis.Children.Clear();
            _plot.Children.Clear();
            var points = Series?.Points ?? [];
            _axis.Height = _plot.Height = ActualHeight;
            if (points.Count == 0)
            {
                _axis.Width = 0;
                _plot.Width = Math.Max(1, ActualWidth);
                Text(_plot, "No closed Trades to chart.", 8, 16);
                return;
            }
            string currency = Series!.Currency;
            var scale = ChartValueScale.For(points.Where(p => p.Value.HasValue).Select(p => (double)p.Value!.Value).Append(0).ToArray());
            int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(scale.Step)), 0, 8);
            string[] labels = Enumerable.Range(0, scale.TickCount).Select(i => ChartValueScale.TickText(scale.Minimum + i * scale.Step, currency, decimals)).ToArray();
            double axisWidth = Math.Max(96, labels.Select(label => { var text = new TextBlock { Text = label, FontSize = 11 }; text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); return text.DesiredSize.Width + 36; }).Max());
            _axis.Width = axisWidth;
            double width = Math.Max(Math.Max(360, points.Count * 40), ActualWidth - axisWidth - 20);
            _plot.Width = width + 20;
            double bottom = ActualHeight - 52;
            double Y(decimal amount) => 16 + (scale.Maximum - (double)amount) / (scale.Maximum - scale.Minimum) * (bottom - 16);
            double span = (points[^1].ClosedAtUtc - points[0].ClosedAtUtc).Ticks;
            double X(CalendarDayPerformancePoint point) => span == 0 ? width / 2 : 16 + (point.ClosedAtUtc - points[0].ClosedAtUtc).Ticks / span * (width - 32);
            for (int i = 0; i < labels.Length; i++)
            {
                double tick = scale.Minimum + i * scale.Step;
                double y = 16 + (scale.Maximum - tick) / (scale.Maximum - scale.Minimum) * (bottom - 16);
                Text(_axis, labels[i], 24, y - 6);
                Line(_plot, new(0, y), new(width, y), NeutralBrush, Math.Abs(tick) < scale.Step / 1000 ? "ZeroBaseline" : "Gridline");
            }
            Line(_plot, new(0, bottom), new(width, bottom), NeutralBrush, "TimeAxis");
            var profitLabel = new TextBlock { Text = "Profit", FontSize = 11, LayoutTransform = new RotateTransform(-90) };
            profitLabel.SetResourceReference(TextBlock.ForegroundProperty, "PtjTextSecondaryBrush");
            AutomationProperties.SetName(profitLabel, $"Profit in {currency}");
            Canvas.SetLeft(profitLabel, 0);
            Canvas.SetTop(profitLabel, (PlotTop + bottom) / 2 - 16);
            _axis.Children.Add(profitLabel);
            Text(_plot, "Time", width / 2 - 12, bottom + 28);
            CalendarDayPerformancePoint? previous = null;
            double lastLabelEnd = double.NegativeInfinity;
            foreach (var point in points)
            {
                double x = X(point), y = point.Value is { } value ? Y(value) : Y(0);
                if (point.Value is { } current && previous?.Value is { } prior)
                {
                    // Hold the preceding realized amount until this closure; never interpolate profit.
                    AddStepFill(X(previous), x, Y(prior), Y(0), prior);
                    Line(_plot, new(X(previous), Y(prior)), new(x, Y(prior)), Outcome(prior), "ClosureStep");
                    foreach (var segment in PnlChart.SplitSegment(new(x, Y(prior)), prior, new(x, y), current, Y(0)))
                        Line(_plot, segment.Start, segment.End, segment.Sign > 0 ? PositiveBrush : segment.Sign < 0 ? NegativeBrush : NeutralBrush, "ClosureStep");
                }
                AddPointTarget(point, x, y, bottom);
                var label = new TextBlock { Text = point.TimeText, FontSize = 11 };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double labelLeft = Math.Clamp(x - label.DesiredSize.Width / 2, 0, Math.Max(0, width - label.DesiredSize.Width));
                if (labelLeft >= lastLabelEnd + 8)
                {
                    Text(_plot, point.TimeText, labelLeft, bottom + 8);
                    lastLabelEnd = labelLeft + label.DesiredSize.Width;
                }
                previous = point.Value.HasValue ? point : null;
            }
            if (points.Any(p => p.Value is null)) Text(_plot, "Unavailable cumulative values: incomplete economics.", 8, 0);
            AddHoverRegions(width, bottom);
            _activeGuide = new Line { Y1 = PlotTop, Y2 = bottom, Stroke = NeutralBrush, StrokeThickness = 1,
                Opacity = .65, StrokeDashArray = new DoubleCollection { 3, 3 }, Tag = "ActivePointGuide",
                IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            Panel.SetZIndex(_activeGuide, 1);
            _plot.Children.Add(_activeGuide);
            _hoveredTarget = FindPoint(hoveredPoint);
            _focusedTarget = FindPoint(focusedPoint);
            if (_focusedTarget is { } replacement && !replacement.Focus()) _focusedTarget = null;
            UpdateActivePoint();
        }
        finally { _rebuilding = false; }
    }

    private Border? FindPoint(CalendarDayPerformancePoint? point) => point is null ? null
        : _pointTargets.FirstOrDefault(target => Equals(target.Tag, point));

    private void CloseTooltips()
    {
        foreach (Border target in _plot.Children.OfType<Border>())
            if (target.ToolTip is ToolTip tip) tip.IsOpen = false;
    }

    private void AddPointTarget(CalendarDayPerformancePoint point, double x, double y, double bottom)
    {
        double top = Math.Clamp(y - 12, PlotTop, bottom - 24);
        var content = new Canvas();
        var target = new Border { Width = 24, Height = 24, Background = Brushes.Transparent,
            Focusable = true, Tag = point, Child = content };
        if (point.Value is { } amount)
        {
            var marker = new Ellipse { Width = 6, Height = 6, Fill = Outcome(amount), IsHitTestVisible = false };
            Canvas.SetLeft(marker, 9); Canvas.SetTop(marker, y - top - 3); content.Children.Add(marker);
        }
        else Text(content, "—", 7, y - top - 7);
        Canvas.SetLeft(target, x - 12); Canvas.SetTop(target, top);
        KeyboardNavigation.SetIsTabStop(target, true);
        AutomationProperties.SetName(target, point.Description);
        AutomationProperties.SetHelpText(target, point.Description);
        ToolTip tip = Tooltip(point, target);
        target.ToolTip = tip;
        var follower = new ChartTooltipFollower(target, tip);
        target.MouseEnter += (_, _) => { _hoveredTarget = target; UpdateActivePoint(); };
        target.MouseLeave += (_, _) => LeavePoint(target);
        target.GotKeyboardFocus += (_, _) =>
        {
            target.BringIntoView();
            target.BorderThickness = new Thickness(1);
            target.SetResourceReference(Border.BorderBrushProperty, "PtjAccentBrush");
            follower.AnchorToKeyboard();
            _focusedTarget = target;
            UpdateActivePoint();
        };
        target.LostKeyboardFocus += (_, _) =>
        {
            target.BorderThickness = new Thickness(0);
            follower.ReleaseKeyboard();
            if (ReferenceEquals(_focusedTarget, target)) _focusedTarget = null;
            UpdateActivePoint();
        };
        target.Unloaded += (_, _) => tip.IsOpen = false;
        Panel.SetZIndex(target, 2);
        _plot.Children.Add(target); _pointTargets.Add(target);
    }

    private void AddHoverRegions(double width, double bottom)
    {
        double[] centers = _pointTargets.Select(target => Canvas.GetLeft(target) + target.Width / 2).ToArray();
        for (int i = 0; i < _pointTargets.Count; i++)
        {
            Border target = _pointTargets[i];
            // Actual intraday intervals vary: cover the plot without gaps and meet at midpoints.
            var range = PnlChart.GetHoverRange(centers, i, width, double.PositiveInfinity);
            var region = new Border { Width = range.Right - range.Left, Height = bottom - PlotTop,
                Background = Brushes.Transparent, Focusable = false, Tag = "DayPerformanceHoverRegion", DataContext = target };
            KeyboardNavigation.SetIsTabStop(region, false);
            Canvas.SetLeft(region, range.Left); Canvas.SetTop(region, PlotTop);
            ToolTip tip = Tooltip((CalendarDayPerformancePoint)target.Tag, region);
            region.ToolTip = tip;
            _ = new ChartTooltipFollower(region, tip);
            region.MouseEnter += (_, _) => { _hoveredTarget = target; UpdateActivePoint(); };
            region.MouseLeave += (_, _) => LeavePoint(target);
            region.MouseLeftButtonDown += (_, _) => target.Focus();
            region.Unloaded += (_, _) => tip.IsOpen = false;
            Panel.SetZIndex(region, 3);
            _plot.Children.Add(region);
        }
    }

    private void LeavePoint(Border target)
    {
        if (ReferenceEquals(_hoveredTarget, target)) _hoveredTarget = null;
        UpdateActivePoint();
    }

    private void UpdateActivePoint()
    {
        Border? target = _hoveredTarget ?? _focusedTarget;
        if (target?.Tag is not CalendarDayPerformancePoint { Value: not null }) target = null;
        if (!ReferenceEquals(target, _displayedTarget))
        {
            Highlight(_displayedTarget, false); Highlight(target, true); _displayedTarget = target;
        }
        if (_activeGuide is null) return;
        _activeGuide.Visibility = target is null ? Visibility.Collapsed : Visibility.Visible;
        if (target is not null) _activeGuide.X1 = _activeGuide.X2 = Canvas.GetLeft(target) + target.Width / 2;
    }

    private static void Highlight(Border? target, bool active)
    {
        if (target?.Child is not Canvas content || content.Children.OfType<Ellipse>().FirstOrDefault() is not { } marker) return;
        double x = Canvas.GetLeft(marker) + marker.Width / 2, y = Canvas.GetTop(marker) + marker.Height / 2;
        marker.Width = marker.Height = active ? 9 : 6;
        Canvas.SetLeft(marker, x - marker.Width / 2); Canvas.SetTop(marker, y - marker.Height / 2);
        marker.StrokeThickness = active ? 1 : 0;
        if (active) marker.SetResourceReference(Shape.StrokeProperty, "PtjAccentBrush");
        else marker.ClearValue(Shape.StrokeProperty);
    }

    private static ToolTip Tooltip(CalendarDayPerformancePoint point, UIElement target)
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding());
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(FrameworkElement.MaxWidthProperty, 280d);
        var tip = new ToolTip { Content = point.Description, ContentTemplate = new DataTemplate { VisualTree = text },
            PlacementTarget = target, Padding = new Thickness(10, 6, 10, 6) };
        tip.SetResourceReference(Control.BackgroundProperty, "PtjSurfaceElevatedBrush");
        tip.SetResourceReference(Control.ForegroundProperty, "PtjTextPrimaryBrush");
        return tip;
    }

    private void AddStepFill(double left, double right, double y, double zero, decimal prior)
    {
        if (prior == 0 || right <= left) return;
        var fill = new Rectangle { Width = right - left, Height = Math.Abs(y - zero), Fill = Outcome(prior),
            Opacity = FillOpacity, IsHitTestVisible = false, Tag = "ClosureFill" };
        Canvas.SetLeft(fill, left); Canvas.SetTop(fill, Math.Min(y, zero));
        Panel.SetZIndex(fill, -1); _plot.Children.Add(fill);
    }

    private Brush Outcome(decimal value) => value > 0 ? PositiveBrush : value < 0 ? NegativeBrush : NeutralBrush;
    private static void Text(Canvas canvas, string text, double x, double y)
    {
        var label = new TextBlock { Text = text, FontSize = 11, IsHitTestVisible = false };
        label.SetResourceReference(TextBlock.ForegroundProperty, "PtjTextSecondaryBrush");
        Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
    }
    private static void Line(Canvas canvas, Point start, Point end, Brush brush, string tag) => canvas.Children.Add(new Line
        { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = brush,
            StrokeThickness = tag == "ClosureStep" ? 1.25 : 1, Opacity = tag == "Gridline" ? .25 : 1,
            Tag = tag, IsHitTestVisible = false });
}
