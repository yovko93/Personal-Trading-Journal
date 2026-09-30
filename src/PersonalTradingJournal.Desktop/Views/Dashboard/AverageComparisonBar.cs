using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

public sealed class AverageComparisonBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(AverageWinLossPresentation), typeof(AverageComparisonBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WinBrushProperty = BrushProperty(nameof(WinBrush));
    public static readonly DependencyProperty LossBrushProperty = BrushProperty(nameof(LossBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));
    public AverageWinLossPresentation? Value { get => (AverageWinLossPresentation?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush WinBrush { get => (Brush)GetValue(WinBrushProperty); set => SetValue(WinBrushProperty, value); }
    public Brush LossBrush { get => (Brush)GetValue(LossBrushProperty); set => SetValue(LossBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(AverageComparisonBar), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    protected override AutomationPeer OnCreateAutomationPeer() => new ComparisonPeer(this);
    private sealed class ComparisonPeer(AverageComparisonBar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.Value?.Description ?? "Average comparison unavailable";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Value is not { HasComparison: true } value)
        {
            dc.DrawRectangle(NeutralBrush, null, new(0, 0, ActualWidth, ActualHeight));
            return;
        }
        double width = (double)value.WinShare * ActualWidth;
        dc.DrawRectangle(WinBrush, null, new(0, 0, width, ActualHeight));
        dc.DrawRectangle(LossBrush, null, new(width, 0, ActualWidth - width, ActualHeight));
    }
}
