using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

/// <summary>A date-labeled, horizontally scrollable daily plot with a keyboard target for every source day.</summary>
public sealed class DailyPnlChart : UserControl
{
    private const double SlotWidth = 88;
    private const double PlotTop = 16;
    private const double DateAxisHeight = 38;
    private readonly Grid _layout = new();
    private readonly ColumnDefinition _axisColumn = new();
    private readonly Canvas _valueAxis = new();
    private readonly Canvas _plot = new();
    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        CanContentScroll = false,
    };
    private readonly List<Border> _barTargets = [];
    private bool _rebuilding;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points),
        typeof(IReadOnlyList<DashboardChartRow>), typeof(DailyPnlChart),
        new PropertyMetadata(null, (owner, _) =>
        {
            var chart = (DailyPnlChart)owner;
            chart._scroll.ScrollToHorizontalOffset(0);
            chart.Rebuild();
        }));
    public static readonly DependencyProperty PositiveBrushProperty = BrushProperty(nameof(PositiveBrush));
    public static readonly DependencyProperty NegativeBrushProperty = BrushProperty(nameof(NegativeBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));

    public IReadOnlyList<DashboardChartRow>? Points { get => (IReadOnlyList<DashboardChartRow>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public Brush PositiveBrush { get => (Brush)GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public Brush NegativeBrush { get => (Brush)GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name,
        typeof(Brush), typeof(DailyPnlChart), new PropertyMetadata(Brushes.Gray, (owner, _) => ((DailyPnlChart)owner).Rebuild()));

    public DailyPnlChart()
    {
        _layout.ColumnDefinitions.Add(_axisColumn);
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_scroll, 1);
        _scroll.Content = _plot;
        _layout.Children.Add(_valueAxis);
        _layout.Children.Add(_scroll);
        Content = _layout;
        SizeChanged += (_, _) => Rebuild();
        _scroll.SizeChanged += (_, _) => Rebuild();
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (e.Handled || e.Delta == 0 || (Keyboard.Modifiers & ModifierKeys.Shift) != 0) return;

        // The plot's horizontal ScrollViewer otherwise consumes an ordinary vertical wheel.
        // Let the parent ScrollViewer apply its native wheel distance and direction once.
        for (DependencyObject? parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is not ScrollViewer outer || outer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
                continue;
            bool canMove = e.Delta > 0 ? outer.VerticalOffset > 0 : outer.VerticalOffset < outer.ScrollableHeight;
            if (!canMove) return;

            e.Handled = true;
            outer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = outer,
            });
            return;
        }
    }

    private void Rebuild()
    {
        if (_rebuilding || ActualHeight < 80 || ActualWidth < 120) return;
        _rebuilding = true;
        try
        {
            foreach (Border target in _barTargets)
                if (target.ToolTip is ToolTip tip) tip.IsOpen = false;
            _barTargets.Clear();
            _valueAxis.Children.Clear();
            _plot.Children.Clear();
            IReadOnlyList<DashboardChartRow> points = Points ?? [];
            double height = ActualHeight, plotBottom = height - DateAxisHeight;
            _valueAxis.Height = height;
            _plot.Height = height;
            if (points.Count == 0)
            {
                _axisColumn.Width = new GridLength(0);
                _plot.Width = Math.Max(1, ActualWidth);
                AddText(_plot, "No daily P&L values in this scope.", 8, PlotTop, "Empty daily chart");
                return;
            }

            double[] values = points.Where(row => row.Value.HasValue).Select(row => (double)row.Value!.Value).ToArray();
            AxisScale? scale = values.Length == 0 ? null : AxisScale.For(values);
            string currency = points[0].Currency;
            int decimals = scale is { } available ? Math.Clamp((int)Math.Ceiling(-Math.Log10(available.Step)), 0, 8) : 2;
            var ticks = scale is { } s
                ? Enumerable.Range(0, s.TickCount).Select(i => s.Minimum + i * s.Step).ToArray()
                : [];
            string[] tickLabels = ticks.Select(value => TickText(value, currency, decimals)).ToArray();
            double axisWidth = Math.Max(72, tickLabels.Select(TextWidth).DefaultIfEmpty(0).Max() + 14);
            _axisColumn.Width = new GridLength(axisWidth);
            _valueAxis.Width = axisWidth;
            double viewport = Math.Max(1, ActualWidth - axisWidth);
            double plotWidth = Math.Max(viewport, points.Count * SlotWidth);
            _plot.Width = plotWidth;
            double slot = plotWidth / points.Count;

            AddLine(_valueAxis, axisWidth - 1, PlotTop, axisWidth - 1, plotBottom, NeutralBrush, 1, "ValueAxis");
            AddLine(_plot, 0, plotBottom, plotWidth, plotBottom, NeutralBrush, 1, "DateAxis");
            if (scale is { } range)
            {
                double Y(double amount) => PlotTop + (range.Maximum - amount) / (range.Maximum - range.Minimum) * (plotBottom - PlotTop);
                for (int i = 0; i < ticks.Length; i++)
                {
                    double y = Y(ticks[i]);
                    var label = AddText(_valueAxis, tickLabels[i], 0, 0, "ValueTick");
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(label, axisWidth - label.DesiredSize.Width - 8);
                    Canvas.SetTop(label, Math.Clamp(y - label.DesiredSize.Height / 2, 0, plotBottom - label.DesiredSize.Height));
                    AddLine(_plot, 0, y, plotWidth, y, NeutralBrush, Math.Abs(ticks[i]) < range.Step / 1000 ? 2 : 1,
                        Math.Abs(ticks[i]) < range.Step / 1000 ? "ZeroBaseline" : "ValueGridline",
                        Math.Abs(ticks[i]) < range.Step / 1000 ? 1 : .3);
                }

                for (int i = 0; i < points.Count; i++)
                {
                    DashboardChartRow row = points[i];
                    double x = (i + .5) * slot;
                    AddDateLabel(row, x, plotBottom);
                    double zero = Y(0);
                    double y = row.Value is { } value ? Y((double)value) : zero;
                    AddBarTarget(row, x, y, zero, plotBottom, slot);
                }
            }
            else
            {
                AddText(_plot, "Daily P&L values unavailable.", 8, PlotTop, "Unavailable daily chart");
                for (int i = 0; i < points.Count; i++)
                {
                    double x = (i + .5) * slot;
                    AddDateLabel(points[i], x, plotBottom);
                    AddBarTarget(points[i], x, (PlotTop + plotBottom) / 2, (PlotTop + plotBottom) / 2, plotBottom, slot);
                }
            }
        }
        finally { _rebuilding = false; }
    }

    private void AddDateLabel(DashboardChartRow row, double center, double plotBottom)
    {
        string date = row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var label = AddText(_plot, date, 0, plotBottom + 5, "DateTick");
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, center - label.DesiredSize.Width / 2);
        AutomationProperties.SetName(label, $"New York closure date {date}");
    }

    private void AddBarTarget(DashboardChartRow row, double center, double valueY, double zeroY, double plotBottom, double slot)
    {
        double barWidth = Math.Min(32, slot * .55);
        double barTop = row.Value == 0m ? Math.Clamp(zeroY - 2.5, PlotTop, plotBottom - 5) : Math.Min(valueY, zeroY);
        double barHeight = row.Value == 0m ? 5 : Math.Max(1, Math.Abs(valueY - zeroY));
        double targetTop = Math.Max(PlotTop, Math.Min(barTop - 8, plotBottom - 24));
        double targetBottom = Math.Min(plotBottom, Math.Max(barTop + barHeight + 8, targetTop + 24));
        double targetWidth = Math.Min(slot, Math.Max(40, barWidth + 8));
        var target = new Border
        {
            Width = targetWidth,
            Height = targetBottom - targetTop,
            Background = Brushes.Transparent,
            Focusable = true,
            Tag = row,
            Child = new Canvas(),
        };
        KeyboardNavigation.SetIsTabStop(target, true);
        Canvas.SetLeft(target, center - targetWidth / 2);
        Canvas.SetTop(target, targetTop);
        string details = TooltipText(row);
        AutomationProperties.SetName(target, details);
        AutomationProperties.SetHelpText(target, row.Description);
        var tip = new ToolTip { Content = details, PlacementTarget = target, Padding = new Thickness(10, 6, 10, 6) };
        tip.SetResourceReference(Control.BackgroundProperty, "PtjSurfaceElevatedBrush");
        tip.SetResourceReference(Control.ForegroundProperty, "PtjTextPrimaryBrush");
        target.ToolTip = tip;
        // WPF's normal initial delay is noticeable on a dense chart. The tooltip is already
        // built from this row, so hovering only needs to open the existing instance.
        ToolTipService.SetInitialShowDelay(target, 100);
        ToolTipService.SetBetweenShowDelay(target, 2000);
        target.GotKeyboardFocus += (_, _) => { target.BorderThickness = new Thickness(1); target.SetResourceReference(Border.BorderBrushProperty, "PtjAccentBrush"); tip.IsOpen = true; };
        target.LostKeyboardFocus += (_, _) => { target.BorderThickness = new Thickness(0); tip.IsOpen = false; };
        if (row.Value is { } value)
        {
            var bar = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                Fill = value > 0m ? PositiveBrush : value < 0m ? NegativeBrush : NeutralBrush,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(bar, (targetWidth - barWidth) / 2);
            Canvas.SetTop(bar, barTop - targetTop);
            ((Canvas)target.Child).Children.Add(bar);
        }
        _plot.Children.Add(target);
        _barTargets.Add(target);
    }

    internal static string TooltipText(DashboardChartRow row)
    {
        string amount = row.Value is { } value
            ? $"{(value > 0m ? "+" : "")}{value.ToString("0.00##########################", CultureInfo.CurrentCulture)} {row.Currency}".TrimEnd()
            : "Unavailable";
        return $"Day Profit\nDate: {row.Date:yyyy-MM-dd}\nTrades Count: {row.TradeCount.ToString(CultureInfo.CurrentCulture)}\nProfit: {amount}";
    }

    private TextBlock AddText(Canvas canvas, string text, double x, double y, string tag)
    {
        var label = new TextBlock { Text = text, FontSize = 11, Tag = tag, IsHitTestVisible = false };
        label.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        canvas.Children.Add(label);
        return label;
    }

    private static void AddLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush brush,
        double thickness, string tag, double opacity = 1)
    {
        canvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush,
            StrokeThickness = thickness, Opacity = opacity, Tag = tag, IsHitTestVisible = false,
        });
    }

    private static double TextWidth(string text)
    {
        var label = new TextBlock { Text = text, FontSize = 11 };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return label.DesiredSize.Width;
    }

    private static string TickText(double value, string currency, int decimals) =>
        $"{value.ToString($"N{decimals}", CultureInfo.CurrentCulture)} {currency}".TrimEnd();

    private readonly record struct AxisScale(double Minimum, double Maximum, double Step)
    {
        public int TickCount => Math.Min(12, (int)Math.Round((Maximum - Minimum) / Step) + 1);

        public static AxisScale For(double[] values)
        {
            double min = Math.Min(0, values.Min()), max = Math.Max(0, values.Max());
            if (min == max) return new(-1, 1, 1);
            double raw = (max - min) / 4;
            double power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double fraction = raw / power;
            double step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * power;
            double floor = Math.Floor(min / step) * step, ceiling = Math.Ceiling(max / step) * step;
            if (floor == ceiling) ceiling = floor + step;
            return new(floor, ceiling, step);
        }
    }
}
