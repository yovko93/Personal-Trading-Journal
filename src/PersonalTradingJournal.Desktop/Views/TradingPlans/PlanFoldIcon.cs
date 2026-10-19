using System.Windows;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

/// <summary>Decorative hinged strips: one render-only progress clock, never a layout/content animation.</summary>
public sealed class PlanFoldIcon : FrameworkElement
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(PlanFoldIcon), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(PlanFoldIcon), new FrameworkPropertyMetadata(Brushes.Teal, FrameworkPropertyMetadataOptions.AffectsRender));
    internal static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(PlanFoldIcon), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    internal double Progress => (double)GetValue(ProgressProperty);
    internal bool? Animate { get; set; }
    internal AnimationClock? Clock { get; private set; }
    public PlanFoldIcon()
    {
        IsHitTestVisible = false; Focusable = false;
        Loaded += (_, _) => { SystemParameters.StaticPropertyChanged += OnPreferences; SetState(false); };
        Unloaded += (_, _) => { SystemParameters.StaticPropertyChanged -= OnPreferences; SetState(false); };
    }
    private void OnPreferences(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation) && !SystemParameters.ClientAreaAnimation) SetState(false); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PlanFoldIcon)d).SetState(true);
    internal void SetState(bool allowAnimation)
    {
        double from = Progress, to = IsExpanded ? 1 : 0;
        Clock?.Controller?.Remove(); Clock = null;
        ApplyAnimationClock(ProgressProperty, null);
        SetValue(ProgressProperty, to);
        if (allowAnimation && IsLoaded && (Animate ?? SystemParameters.ClientAreaAnimation) && from != to)
        {
            Clock = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(200))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop }.CreateClock();
            ApplyAnimationClock(ProgressProperty, Clock, HandoffBehavior.SnapshotAndReplace);
            // A newly attached clock has no current value until its first tick. Establish the
            // captured starting pose synchronously so a rapid reversal cannot flash the base value.
            Clock.Controller!.SeekAlignedToLastTick(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
        }
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double p = Math.Clamp(Progress, 0, 1);
        dc.PushTransform(new TranslateTransform((ActualWidth - 20) / 2, (ActualHeight - 20) / 2));
        // Top/bottom strips hinge at their centres. Opposite depth folds foreshorten each half;
        // a small offset rear face makes the turn legible without a blur/shadow effect.
        foreach (int direction in new[] { -1, 1 })
        {
            double fold = Math.Sin(Math.PI * p);
            dc.PushOpacity(.28 * fold);
            dc.PushTransform(new TranslateTransform(0, 1.1 * fold));
            dc.DrawGeometry(Foreground, null, Strip(p, direction));
            dc.Pop(); dc.Pop();
            dc.DrawGeometry(Foreground, null, Strip(p, direction));
        }
        // The middle strip folds edge-on into the hinge, rather than crossfading away.
        double span = 18 * (1 - p), thickness = 2 * (1 - p);
        if (span > 0 && thickness > 0) dc.DrawRectangle(Foreground, null, new Rect(10 - span / 2, 10 - thickness / 2, span, thickness));
        dc.Pop();
    }
    internal static Geometry Strip(double progress, int direction)
    {
        double p = Math.Clamp(progress, 0, 1), angle = direction * Math.PI / 4 * p;
        double fold = Math.Sin(Math.PI * p) * 1.15;
        double cy = 10 + direction * 6 * (1 - p);
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            // Project the two hinged halves with perspective; the centre seam stays joined.
            Point Project(double x, double y)
            {
                double z = Math.Abs(x) * Math.Sin(fold) * direction;
                double scale = 40 / (40 + z);
                double fx = x * Math.Cos(fold);
                return new Point(10 + (fx * Math.Cos(angle) - y * Math.Sin(angle)) * scale,
                    cy + (fx * Math.Sin(angle) + y * Math.Cos(angle)) * scale);
            }
            c.BeginFigure(Project(-9, -1), true, true);
            foreach (var point in new[] { Project(0, -1), Project(9, -1), Project(9, 1), Project(0, 1), Project(-9, 1) }) c.LineTo(point, true, false);
        }
        geometry.Freeze(); return geometry;
    }
}
