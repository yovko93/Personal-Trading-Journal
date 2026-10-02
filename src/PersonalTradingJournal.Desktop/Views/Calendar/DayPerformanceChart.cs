using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly Canvas _axis = new();
    private readonly Canvas _plot = new() { Background = Brushes.Transparent };
    private readonly ScrollViewer _scroller = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private bool _rebuilding;
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(CalendarDayPerformance), typeof(DayPerformanceChart), new PropertyMetadata(null, Changed));
    public CalendarDayPerformance? Series { get => (CalendarDayPerformance?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public static readonly DependencyProperty PositiveBrushProperty = BrushProperty(nameof(PositiveBrush));
    public static readonly DependencyProperty NegativeBrushProperty = BrushProperty(nameof(NegativeBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));
    public Brush PositiveBrush { get => (Brush)GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public Brush NegativeBrush { get => (Brush)GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(DayPerformanceChart), new PropertyMetadata(Brushes.Gray, Changed));
    private static void Changed(DependencyObject owner, DependencyPropertyChangedEventArgs e) => ((DayPerformanceChart)owner).Rebuild();

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
        SizeChanged += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        if (_rebuilding || ActualWidth < 120 || ActualHeight < 100) return;
        _rebuilding = true;
        try
        {
            foreach (Border old in _plot.Children.OfType<Border>())
                if (old.ToolTip is ToolTip tip) tip.IsOpen = false;
            _axis.Children.Clear();
            _plot.Children.Clear();
            var points = Series?.Points ?? [];
            _axis.Height = _plot.Height = ActualHeight;
            if (points.Count == 0)
            {
                _axis.Width = 0;
                Text(_plot, "No closed Trades to chart.", 8, 16);
                return;
            }
            string currency = Series!.Currency;
            var scale = ChartValueScale.For(points.Where(p => p.Value.HasValue).Select(p => (double)p.Value!.Value).Append(0).ToArray());
            int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(scale.Step)), 0, 8);
            string[] labels = Enumerable.Range(0, scale.TickCount).Select(i => ChartValueScale.TickText(scale.Minimum + i * scale.Step, currency, decimals)).ToArray();
            double axisWidth = Math.Max(72, labels.Select(label => { var text = new TextBlock { Text = label, FontSize = 11 }; text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); return text.DesiredSize.Width + 12; }).Max());
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
                Text(_axis, labels[i], 0, y - 6);
                Line(_plot, new(0, y), new(width, y), NeutralBrush, Math.Abs(tick) < scale.Step / 1000 ? "ZeroBaseline" : "Gridline");
            }
            Line(_plot, new(0, bottom), new(width, bottom), NeutralBrush, "TimeAxis");
            CalendarDayPerformancePoint? previous = null;
            double lastLabel = double.NegativeInfinity;
            foreach (var point in points)
            {
                double x = X(point), y = point.Value is { } value ? Y(value) : Y(0);
                if (point.Value is { } current && previous?.Value is { } prior)
                {
                    // Hold the preceding realized amount until this closure; never interpolate profit.
                    Line(_plot, new(X(previous), Y(prior)), new(x, Y(prior)), Outcome(prior), "ClosureStep");
                    foreach (var segment in PnlChart.SplitSegment(new(x, Y(prior)), prior, new(x, y), current, Y(0)))
                        Line(_plot, segment.Start, segment.End, segment.Sign > 0 ? PositiveBrush : segment.Sign < 0 ? NegativeBrush : NeutralBrush, "ClosureStep");
                }
                var target = new Border { Width = 24, Height = 24, Background = Brushes.Transparent, Focusable = true, Tag = point,
                    Child = point.Value is { } amount ? new Ellipse { Width = 7, Height = 7, Fill = Outcome(amount) }
                        : new TextBlock { Text = "—", Foreground = NeutralBrush, HorizontalAlignment = HorizontalAlignment.Center } };
                Canvas.SetLeft(target, x - 12);
                Canvas.SetTop(target, y - 12);
                AutomationProperties.SetName(target, point.Description);
                var tip = new ToolTip { Content = point.Description, Placement = PlacementMode.Bottom, PlacementTarget = target };
                target.ToolTip = tip;
                var follower = new ChartTooltipFollower(target, tip);
                target.GotKeyboardFocus += (_, _) =>
                {
                    target.BorderThickness = new Thickness(1);
                    target.SetResourceReference(Border.BorderBrushProperty, "PtjAccentBrush");
                    follower.AnchorToKeyboard();
                };
                target.LostKeyboardFocus += (_, _) => { target.BorderThickness = new Thickness(0); follower.ReleaseKeyboard(); };
                target.Unloaded += (_, _) => tip.IsOpen = false;
                _plot.Children.Add(target);
                if (x - lastLabel >= 130)
                {
                    Text(_plot, point.TimeText, Math.Max(0, Math.Min(width - 115, x - 50)), bottom + 8);
                    lastLabel = x;
                }
                previous = point.Value.HasValue ? point : null;
            }
            if (points.Any(p => p.Value is null)) Text(_plot, "Unavailable cumulative values: incomplete economics.", 8, 0);
        }
        finally { _rebuilding = false; }
    }

    private Brush Outcome(decimal value) => value > 0 ? PositiveBrush : value < 0 ? NegativeBrush : NeutralBrush;
    private static void Text(Canvas canvas, string text, double x, double y)
    {
        var label = new TextBlock { Text = text, FontSize = 11, IsHitTestVisible = false };
        label.SetResourceReference(TextBlock.ForegroundProperty, "PtjTextSecondaryBrush");
        Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
    }
    private static void Line(Canvas canvas, Point start, Point end, Brush brush, string tag) => canvas.Children.Add(new Line
        { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = brush, StrokeThickness = tag == "ClosureStep" ? 2 : 1, Tag = tag, IsHitTestVisible = false });
}
