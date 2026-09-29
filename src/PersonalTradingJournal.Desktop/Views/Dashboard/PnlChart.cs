using System.Globalization;
using System.Windows;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

/// <summary>Responsive vector plot. Exact accessible values are in the adjacent expandable table.</summary>
public sealed class PnlChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points),
        typeof(IReadOnlyList<DashboardChartRow>), typeof(PnlChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsCumulativeProperty = DependencyProperty.Register(nameof(IsCumulative),
        typeof(bool), typeof(PnlChart), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground),
        typeof(Brush), typeof(PnlChart), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(nameof(StartDate),
        typeof(DateOnly?), typeof(PnlChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PositiveBrushProperty = BrushProperty(nameof(PositiveBrush));
    public static readonly DependencyProperty NegativeBrushProperty = BrushProperty(nameof(NegativeBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));
    public Brush PositiveBrush { get => (Brush)GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public Brush NegativeBrush { get => (Brush)GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name,
        typeof(Brush), typeof(PnlChart), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public DateOnly? StartDate { get => (DateOnly?)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public IReadOnlyList<DashboardChartRow>? Points { get => (IReadOnlyList<DashboardChartRow>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public bool IsCumulative { get => (bool)GetValue(IsCumulativeProperty); set => SetValue(IsCumulativeProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Points is not { Count: > 0 } points || ActualWidth < 120 || ActualHeight < 80) return;
        // Doubles are rendering coordinates only; authoritative decimal values are never changed.
        double[] amounts = points.Where(p => p.Value.HasValue).Select(p => (double)p.Value!.Value).Append(0d).ToArray();
        double min = amounts.Min(), max = amounts.Max();
        if (max == min) { min -= 1; max += 1; }
        const double left = 82, top = 20;
        double width = Math.Max(1, ActualWidth - left - 12), height = ActualHeight - 56;
        double Y(double value) => top + (max - value) / (max - min) * height;
        double zero = Y(0);
        var pen = new Pen(NeutralBrush, 1);
        dc.DrawLine(pen, new(left, zero), new(left + width, zero));
        Text(dc, max.ToString("G4", CultureInfo.CurrentCulture), new(0, top));
        Text(dc, min.ToString("G4", CultureInfo.CurrentCulture), new(0, top + height - 12));
        DateOnly start = StartDate ?? points[0].Date;
        double dateSpan = Math.Max(1, points[^1].Date.DayNumber - start.DayNumber + 1);
        Text(dc, start.ToString("yyyy-MM-dd"), new(left, ActualHeight - 20));
        if (ActualWidth > 350) Text(dc, points[^1].Date.ToString("yyyy-MM-dd"), new(ActualWidth - 92, ActualHeight - 20));
        Point? previous = IsCumulative ? new Point(left, zero) : null;
        decimal previousValue = 0m;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].Value is not { } value) { previous = null; continue; }
            double x = left + width * (points[i].Date.DayNumber - start.DayNumber + 1) / dateSpan;
            double y = Y((double)value);
            if (IsCumulative)
            {
                if (previous is { } prior)
                    foreach (LineSegment segment in SplitSegment(prior, previousValue, new(x, y), value, zero))
                        dc.DrawLine(new(OutcomeBrush(segment.Sign), 2), segment.Start, segment.End);
                dc.DrawEllipse(OutcomeBrush(Math.Sign(value)), null, new(x, y), 2, 2);
                previous = new(x, y);
                previousValue = value;
            }
            else
            {
                double bar = Math.Max(.5, Math.Min(30, width / dateSpan * .65));
                dc.DrawRectangle(OutcomeBrush(Math.Sign(value)), null, new(x - bar / 2, Math.Min(y, zero), bar, Math.Max(1, Math.Abs(zero - y))));
            }
        }
    }
    private Brush OutcomeBrush(int sign) => sign > 0 ? PositiveBrush : sign < 0 ? NegativeBrush : NeutralBrush;

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
    private void Text(DrawingContext dc, string value, Point origin) => dc.DrawText(new FormattedText(value,
        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Foreground,
        VisualTreeHelper.GetDpi(this).PixelsPerDip), origin);
}
