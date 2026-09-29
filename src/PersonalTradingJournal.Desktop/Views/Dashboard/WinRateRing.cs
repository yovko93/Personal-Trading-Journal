using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

/// <summary>Drawing-only proportions; unavailable coverage never becomes a zero win rate.</summary>
public sealed class WinRateRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(WinRatePresentation), typeof(WinRateRing), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WinBrushProperty = BrushProperty(nameof(WinBrush));
    public static readonly DependencyProperty LossBrushProperty = BrushProperty(nameof(LossBrush));
    public static readonly DependencyProperty NeutralBrushProperty = BrushProperty(nameof(NeutralBrush));
    public WinRatePresentation? Value { get => (WinRatePresentation?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush WinBrush { get => (Brush)GetValue(WinBrushProperty); set => SetValue(WinBrushProperty, value); }
    public Brush LossBrush { get => (Brush)GetValue(LossBrushProperty); set => SetValue(LossBrushProperty, value); }
    public Brush NeutralBrush { get => (Brush)GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(WinRateRing), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    protected override AutomationPeer OnCreateAutomationPeer() => new RingPeer(this);
    private sealed class RingPeer(WinRateRing owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => owner.Value?.Description ?? "Win rate unavailable";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        const double thickness = 12;
        double radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - thickness);
        Point center = new(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, new Pen(NeutralBrush, thickness), center, radius, radius);
        if (Value is not { IsAvailable: true } value) return;
        double start = -Math.PI / 2;
        foreach (var (share, brush) in new[] { (value.WinsShare, WinBrush), (value.LossesShare, LossBrush), (value.BreakEvenShare, NeutralBrush) })
        {
            double sweep = (double)share * Math.Tau;
            if (share == 1m) dc.DrawEllipse(null, new Pen(brush, thickness), center, radius, radius);
            else if (share > 0m)
            {
                var geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(At(start), false, false);
                    context.ArcTo(At(start + sweep), new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise, true, false);
                }
                dc.DrawGeometry(null, new Pen(brush, thickness), geometry);
            }
            start += sweep;
        }
        Point At(double angle) => new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
    }
}
