using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

/// <summary>Cumulative realized P&L with dated, accessible points and a presentation-only period origin.</summary>
public sealed class PnlChart : UserControl
{
    private const double SlotWidth = 88;
    private const double PlotTop = 16;
    // Space for the origin's second-line "Start" label plus the horizontal scrollbar.
    private const double DateAxisHeight = 52;
    private readonly ColumnDefinition _axisColumn = new();
    private readonly Canvas _valueAxis = new() { Background = Brushes.Transparent };
    private readonly Canvas _plot = new() { Background = Brushes.Transparent };
    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        CanContentScroll = false,
    };
    private readonly List<Border> _pointTargets = [];
    private bool _rebuilding;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points),
        typeof(IReadOnlyList<DashboardChartRow>), typeof(PnlChart), new PropertyMetadata(null, OnScopeChanged));
    public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(nameof(StartDate),
        typeof(DateOnly?), typeof(PnlChart), new PropertyMetadata(null, OnScopeChanged));
    public static readonly DependencyProperty PositiveBrushProperty = BrushProperty(nameof(PositiveBrush));
    public static readonly DependencyProperty NegativeBrushProperty = BrushProperty(nameof(NegativeBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));

    public IReadOnlyList<DashboardChartRow>? Points { get => (IReadOnlyList<DashboardChartRow>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public DateOnly? StartDate { get => (DateOnly?)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public Brush PositiveBrush { get => (Brush)GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public Brush NegativeBrush { get => (Brush)GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }

    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name,
        typeof(Brush), typeof(PnlChart), new PropertyMetadata(Brushes.Gray, (owner, _) => ((PnlChart)owner).Rebuild()));

    private static void OnScopeChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        var chart = (PnlChart)owner;
        chart._scroll.ScrollToHorizontalOffset(0);
        chart.Rebuild();
    }

    public PnlChart()
    {
        var layout = new Grid();
        layout.ColumnDefinitions.Add(_axisColumn);
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_scroll, 1);
        _scroll.Content = _plot;
        NestedTableWheelRouting.SetForwardVerticalWheel(_scroll, true);
        layout.Children.Add(_valueAxis);
        layout.Children.Add(_scroll);
        Content = layout;
        SizeChanged += (_, _) => Rebuild();
        _scroll.SizeChanged += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        if (_rebuilding || ActualHeight < 80 || ActualWidth < 120) return;
        _rebuilding = true;
        try
        {
            foreach (Border target in _pointTargets)
                if (target.ToolTip is ToolTip tip) tip.IsOpen = false;
            _pointTargets.Clear();
            _valueAxis.Children.Clear();
            _plot.Children.Clear();
            IReadOnlyList<DashboardChartRow> points = Points ?? [];
            double plotBottom = ActualHeight - DateAxisHeight;
            _valueAxis.Height = _plot.Height = ActualHeight;
            if (points.Count == 0)
            {
                _axisColumn.Width = new GridLength(0);
                _plot.Width = Math.Max(1, ActualWidth);
                AddText(_plot, "No cumulative P&L values in this scope.", 8, PlotTop, "Empty cumulative chart");
                return;
            }

            // Doubles are drawing coordinates only; exact decimal amounts remain on the targets.
            double[] values = points.Where(row => row.Value.HasValue).Select(row => (double)row.Value!.Value).Append(0d).ToArray();
            ChartValueScale scale = ChartValueScale.For(values);
            string currency = points[0].Currency;
            int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(scale.Step)), 0, 8);
            double[] ticks = Enumerable.Range(0, scale.TickCount).Select(i => scale.Minimum + i * scale.Step).ToArray();
            string[] labels = ticks.Select(value => ChartValueScale.TickText(value, currency, decimals)).ToArray();
            double axisWidth = Math.Max(72, labels.Select(TextWidth).Max() + 14);
            _axisColumn.Width = new GridLength(axisWidth);
            _valueAxis.Width = axisWidth;
            double plotWidth = Math.Max(Math.Max(1, ActualWidth - axisWidth), (points.Count + 1) * SlotWidth);
            _plot.Width = plotWidth;
            double slot = plotWidth / (points.Count + 1);
            double Y(double amount) => PlotTop + (scale.Maximum - amount) / (scale.Maximum - scale.Minimum) * (plotBottom - PlotTop);
            double zero = Y(0);

            AddLine(_valueAxis, new(axisWidth - 1, PlotTop), new(axisWidth - 1, plotBottom), NeutralBrush, 1, "ValueAxis");
            AddLine(_plot, new(0, plotBottom), new(plotWidth, plotBottom), NeutralBrush, 1, "DateAxis");
            for (int i = 0; i < ticks.Length; i++)
            {
                double y = Y(ticks[i]);
                TextBlock label = AddText(_valueAxis, labels[i], 0, 0, "ValueTick");
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, axisWidth - label.DesiredSize.Width - 8);
                Canvas.SetTop(label, Math.Clamp(y - label.DesiredSize.Height / 2, 0, plotBottom - label.DesiredSize.Height));
                bool isZero = Math.Abs(ticks[i]) < scale.Step / 1000;
                AddLine(_plot, new(0, y), new(plotWidth, y), NeutralBrush, isZero ? 2 : 1,
                    isZero ? "ZeroBaseline" : "ValueGridline", isZero ? 1 : .3);
            }

            var origin = new DashboardChartRow(StartDate ?? points[0].Date, 0m, false, currency, "Period start before selected closed Trades", 0);
            Point? previous = new Point(slot / 2, zero);
            decimal previousValue = 0m;
            AddDateLabel(origin.Date, slot / 2, plotBottom, true);
            AddPointTarget(origin, slot / 2, zero, plotBottom, true);
            for (int i = 0; i < points.Count; i++)
            {
                DashboardChartRow row = points[i];
                double x = (i + 1.5) * slot;
                double y = row.Value is { } amount ? Y((double)amount) : zero;
                AddDateLabel(row.Date, x, plotBottom, false);
                if (row.Value is { } value)
                {
                    if (previous is { } prior)
                        foreach (LineSegment segment in SplitSegment(prior, previousValue, new(x, y), value, zero))
                            AddLine(_plot, segment.Start, segment.End, OutcomeBrush(segment.Sign), 2, "CumulativeSegment");
                    previous = new(x, y);
                    previousValue = value;
                }
                else previous = null;
                AddPointTarget(row, x, y, plotBottom, false);
            }
            if (points.All(row => row.Value is null))
                AddText(_plot, "Cumulative P&L values unavailable.", 8, PlotTop, "Unavailable cumulative chart");
        }
        finally { _rebuilding = false; }
    }

    private void AddDateLabel(DateOnly date, double center, double plotBottom, bool origin)
    {
        string text = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        TextBlock label = AddText(_plot, origin ? text + "\nStart" : text, 0, plotBottom + 5, "DateTick");
        label.TextAlignment = TextAlignment.Center;
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, center - label.DesiredSize.Width / 2);
        AutomationProperties.SetName(label, $"{(origin ? "Period start" : "New York closure date")} {text}");
    }

    private void AddPointTarget(DashboardChartRow row, double x, double y, double plotBottom, bool origin)
    {
        double top = Math.Clamp(y - 12, PlotTop, plotBottom - 24);
        var target = new Border { Width = 40, Height = 24, Background = Brushes.Transparent, Focusable = true,
            Tag = row, Name = origin ? "PeriodOrigin" : "CumulativePoint", Child = new Canvas() };
        KeyboardNavigation.SetIsTabStop(target, true);
        Canvas.SetLeft(target, x - 20);
        Canvas.SetTop(target, top);
        string details = TooltipText(row, origin);
        AutomationProperties.SetName(target, details);
        AutomationProperties.SetHelpText(target, origin ? "Period start before selected closed Trades; not an account balance." : row.Description);
        var tip = new ToolTip { Content = details, PlacementTarget = target, Padding = new Thickness(10, 6, 10, 6) };
        tip.SetResourceReference(Control.BackgroundProperty, "PtjSurfaceElevatedBrush");
        tip.SetResourceReference(Control.ForegroundProperty, "PtjTextPrimaryBrush");
        target.ToolTip = tip;
        ToolTipService.SetInitialShowDelay(target, 100);
        ToolTipService.SetBetweenShowDelay(target, 2000);
        target.GotKeyboardFocus += (_, _) =>
        {
            target.BringIntoView();
            target.BorderThickness = new Thickness(1);
            target.SetResourceReference(Border.BorderBrushProperty, "PtjAccentBrush");
            tip.IsOpen = true;
        };
        target.LostKeyboardFocus += (_, _) => { target.BorderThickness = new Thickness(0); tip.IsOpen = false; };
        if (row.Value is { } value)
        {
            var marker = new Ellipse { Width = 6, Height = 6, Fill = OutcomeBrush(Math.Sign(value)), IsHitTestVisible = false };
            Canvas.SetLeft(marker, 17);
            Canvas.SetTop(marker, y - top - 3);
            ((Canvas)target.Child).Children.Add(marker);
        }
        _plot.Children.Add(target);
        _pointTargets.Add(target);
    }

    internal static string TooltipText(DashboardChartRow row, bool origin = false)
    {
        string amount = row.Value is { } value
            ? $"{(value > 0m ? "+" : "")}{value.ToString("0.00##########################", CultureInfo.CurrentCulture)} {row.Currency}".TrimEnd()
            : "Unavailable";
        return $"{(origin ? "Period start" : "Cumulative Realized P&L")}\nDate: {row.Date:yyyy-MM-dd}\nCumulative P&L: {amount}";
    }

    private Brush OutcomeBrush(int sign) => sign > 0 ? PositiveBrush : sign < 0 ? NegativeBrush : NeutralBrush;

    private TextBlock AddText(Canvas canvas, string text, double x, double y, string tag)
    {
        var label = new TextBlock { Text = text, FontSize = 11, Tag = tag, IsHitTestVisible = false };
        label.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        canvas.Children.Add(label);
        return label;
    }

    private static void AddLine(Canvas canvas, Point start, Point end, Brush brush, double thickness, string tag, double opacity = 1)
        => canvas.Children.Add(new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = brush,
            StrokeThickness = thickness, Opacity = opacity, Tag = tag, IsHitTestVisible = false });

    private static double TextWidth(string text)
    {
        var label = new TextBlock { Text = text, FontSize = 11 };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return label.DesiredSize.Width;
    }

    internal readonly record struct LineSegment(Point Start, Point End, int Sign);

    // Coordinates only: no decimal economics are rounded or rewritten. A crossing uses both
    // endpoint magnitudes, not their signs alone. Double magnitudes avoid decimal overflow.
    internal static IReadOnlyList<LineSegment> SplitSegment(Point start, decimal startValue, Point end, decimal endValue, double zeroY)
    {
        int first = Math.Sign(startValue), last = Math.Sign(endValue);
        if (first != 0 && last != 0 && first != last)
        {
            double magnitude = Math.Abs((double)startValue);
            double fraction = magnitude / (magnitude + Math.Abs((double)endValue));
            Point crossing = new(start.X + (end.X - start.X) * fraction, zeroY);
            return [new(start, crossing, first), new(crossing, end, last)];
        }
        return [new(start, end, first != 0 ? first : last)];
    }
}
