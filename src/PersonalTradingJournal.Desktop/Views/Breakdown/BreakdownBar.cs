using System.Windows;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;

namespace PersonalTradingJournal.Desktop.Views.Breakdown;

/// <summary>Small native WPF geometry surface; its enclosing ListBoxItem provides selection and accessibility.</summary>
public sealed class BreakdownBar : FrameworkElement
{
    public static readonly DependencyProperty RowProperty = DependencyProperty.Register(nameof(Row), typeof(BreakdownChartRow), typeof(BreakdownBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public BreakdownChartRow? Row { get => (BreakdownChartRow?)GetValue(RowProperty); set => SetValue(RowProperty, value); }
    public static readonly DependencyProperty PositiveBrushProperty = BrushProperty(nameof(PositiveBrush));
    public static readonly DependencyProperty NegativeBrushProperty = BrushProperty(nameof(NegativeBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));
    public Brush PositiveBrush { get => (Brush)GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public Brush NegativeBrush { get => (Brush)GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(BreakdownBar), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    protected override void OnRender(DrawingContext dc)
    {
        if (Row is not { } row || ActualWidth < 4 || ActualHeight < 4) return;
        double width = ActualWidth - 4, zero = 2 + width * row.ZeroPosition;
        dc.DrawLine(new Pen(NeutralBrush, 1), new(zero, 0), new(zero, ActualHeight));
        if (row.ValuePosition is not { } value) return; // No bar/dot for missing evidence.
        if (row.Value == 0) { dc.DrawEllipse(null, new Pen(NeutralBrush, 1.5), new(zero, ActualHeight / 2), 2, 2); return; }
        double end = 2 + width * value;
        dc.DrawRectangle(row.Value < 0 ? NegativeBrush : PositiveBrush, null,
            new Rect(Math.Min(zero, end), 4, Math.Abs(end - zero), Math.Max(0, ActualHeight - 8)));
    }
}
