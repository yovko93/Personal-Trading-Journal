using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PersonalTradingJournal.Desktop.Diagnostics;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RulesView
{
    private sealed record DragSlot(Border Row, Guid Id, double Top, double Height, Transform Original, TranslateTransform Shift)
    {
        public double From { get; set; }
        public double To { get; set; }
    }

    private readonly List<DragSlot> _dragSlots = [];
    private int _sourceIndex = -1;
    internal int ProspectiveIndex { get; private set; } = -1;
    internal bool? AnimateReordering { get; set; }
    internal AnimationClock? ShiftClock { get; private set; }
    private bool _resettingProgress;
    private static readonly DependencyProperty ShiftProgressProperty = DependencyProperty.Register(
        "ShiftProgress", typeof(double), typeof(RulesView), new PropertyMetadata(0d,
            (d, e) => ((RulesView)d).ApplyShiftProgress((double)e.NewValue)));

    private void CaptureVisualOrder(Border source)
    {
        // One traversal/capture per gesture. Coordinates are in the stationary ItemsControl,
        // not the transformed hit row, so scrolling and animated targets cannot oscillate.
        foreach (var row in Descendants(RuleRows).OfType<Border>().Where(r => r.Name == "RuleRowSurface"))
        {
            var transform = new TranslateTransform();
            _dragSlots.Add(new(row, ((RuleRow)row.DataContext).Id,
                row.TranslatePoint(new Point(), RuleRows).Y, row.ActualHeight + row.Margin.Top + row.Margin.Bottom,
                row.RenderTransform, transform));
            row.RenderTransform = transform;
        }
        _sourceIndex = _dragSlots.FindIndex(s => ReferenceEquals(s.Row, source));
        ProspectiveIndex = _sourceIndex;
    }

    internal void UpdateProspectiveOrder(Point overlayPoint)
    {
        if (_sourceIndex < 0) return;
        double y = DragOverlay.TranslatePoint(overlayPoint, RuleRows).Y;
        int insertion = _dragSlots.Where((_, i) => i != _sourceIndex).Count(s => y >= s.Top + s.Row.ActualHeight / 2);
        SetProspectiveIndex(insertion);
    }

    internal void SetProspectiveIndex(int insertion)
    {
        if (_sourceIndex < 0 || insertion == ProspectiveIndex) return;
        insertion = Math.Clamp(insertion, 0, _dragSlots.Count - 1);
        if (insertion == ProspectiveIndex) return;
        // Retarget from current visual positions, with ONE shared easing clock. All remaining
        // cards keep their relative order and interpolate together, including unequal heights.
        // No layout transform, collection move, binding, renumber, or new template is involved.
        foreach (var slot in _dragSlots) slot.From = slot.Shift.Y;
        StopShiftClock();
        var visualOrder = _dragSlots.Where((_, i) => i != _sourceIndex).ToList();
        visualOrder.Insert(insertion, _dragSlots[_sourceIndex]);
        double top = _dragSlots[0].Top;
        foreach (var slot in visualOrder) { slot.To = top - slot.Top; top += slot.Height; }
        ProspectiveIndex = insertion;
        DragDiagnostics?.Count(RuleDragCounter.TargetChanges);
        if (!(AnimateReordering ?? SystemParameters.ClientAreaAnimation)) { ApplyShiftProgress(1); return; }
        SetValue(ShiftProgressProperty, 0d);
        DragDiagnostics?.Count(RuleDragCounter.Animations);
        ShiftClock = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd
        }.CreateClock();
        ApplyAnimationClock(ShiftProgressProperty, ShiftClock);
    }

    private void ApplyShiftProgress(double progress)
    {
        if (_resettingProgress || _dragSlots.Count == 0) return;
        DragDiagnostics?.Count(RuleDragCounter.ShiftFrames);
        foreach (var slot in _dragSlots) slot.Shift.Y = slot.From + (slot.To - slot.From) * progress;
    }

    private void StopShiftClock()
    {
        _resettingProgress = true;
        BeginAnimation(ShiftProgressProperty, null);
        ShiftClock?.Controller?.Remove();
        ShiftClock = null;
        SetValue(ShiftProgressProperty, 0d);
        _resettingProgress = false;
    }

    private void RestoreVisualOrder()
    {
        StopShiftClock();
        foreach (var slot in _dragSlots) { slot.Shift.Y = 0; slot.From = slot.To = 0; }
        ProspectiveIndex = _sourceIndex;
    }

    private void ClearVisualOrder()
    {
        RestoreVisualOrder();
        foreach (var slot in _dragSlots) slot.Row.RenderTransform = slot.Original;
        _dragSlots.Clear();
        _sourceIndex = ProspectiveIndex = -1;
    }

    internal async Task DropProspectiveOrderAsync()
    {
        if (_sourceIndex < 0) { EndRowDrag(); return; }
        Guid source = _dragSlots[_sourceIndex].Id;
        var others = _dragSlots.Where((_, i) => i != _sourceIndex).ToArray();
        Guid? before = ProspectiveIndex < others.Length ? others[ProspectiveIndex].Id : null;
        await CommitDropAsync(source, before);
    }
}
