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
        var pen = new Pen(Foreground, 1);
        dc.DrawLine(pen, new(left, zero), new(left + width, zero));
        Text(dc, max.ToString("G4", CultureInfo.CurrentCulture), new(0, top));
        Text(dc, min.ToString("G4", CultureInfo.CurrentCulture), new(0, top + height - 12));
        DateOnly start = StartDate ?? points[0].Date;
        double dateSpan = Math.Max(1, points[^1].Date.DayNumber - start.DayNumber + 1);
        Text(dc, start.ToString("yyyy-MM-dd"), new(left, ActualHeight - 20));
        if (ActualWidth > 350) Text(dc, points[^1].Date.ToString("yyyy-MM-dd"), new(ActualWidth - 92, ActualHeight - 20));
        Point? previous = IsCumulative ? new Point(left, zero) : null;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].Value is not { } value) { previous = null; continue; }
            double x = left + width * (points[i].Date.DayNumber - start.DayNumber + 1) / dateSpan;
            double y = Y((double)value);
            if (IsCumulative)
            {
                if (previous is { } prior) dc.DrawLine(new(Foreground, 2), prior, new(x, y));
                dc.DrawEllipse(Foreground, null, new(x, y), 2, 2);
                previous = new(x, y);
            }
            else
            {
                double bar = Math.Max(.5, Math.Min(30, width / dateSpan * .65));
                dc.DrawRectangle(Foreground, null, new(x - bar / 2, Math.Min(y, zero), bar, Math.Max(1, Math.Abs(zero - y))));
            }
        }
    }
    private void Text(DrawingContext dc, string value, Point origin) => dc.DrawText(new FormattedText(value,
        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Foreground,
        VisualTreeHelper.GetDpi(this).PixelsPerDip), origin);
}
