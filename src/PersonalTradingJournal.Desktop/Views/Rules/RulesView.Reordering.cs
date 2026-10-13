using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Desktop.Diagnostics;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RulesView
{
    private const string RuleDragFormat = "PTJ.RuleOrder";
    private Point _dragStart;
    private Border? _dragRow;
    private int _edgeDirection;
    private long? _lastEdgeScroll;
    internal TimeProvider DragClock { get; set; } = TimeProvider.System;

    internal static bool CanStartRowDrag(DependencyObject? source, Border row)
    {
        // Walk the original hit, not just the event sender. Preserve selection and editing
        // inside interactive descendants, including any future selectable text control.
        for (var current = source; current is not null; current = DragParent(current))
        {
            if (ReferenceEquals(current, row)) return true;
            if (current is ButtonBase or TextBoxBase or PasswordBox or Selector or ScrollBar or Thumb or Hyperlink or Expander ||
                current is FrameworkElement { Name: "InlineEditorHost" }) return false;
        }
        return false;
    }
    private static DependencyObject? DragParent(DependencyObject value) => value is Visual
        ? VisualTreeHelper.GetParent(value) : value is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(value);

    private void OnRowDown(object sender, MouseButtonEventArgs e)
    {
        _dragRow = null;
        if (sender is not Border row || DataContext is not RulesViewModel { IsBusy: false } ||
            !CanStartRowDrag(e.OriginalSource as DependencyObject, row)) return;
        _dragRow = row; _dragStart = e.GetPosition(this);
        row.Focus(); // Do not handle the event or capture the mouse; ordinary controls retain input.
        Mouse.UpdateCursor();
    }
    private void OnRowMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { _dragRow = null; return; }
        if (!ReferenceEquals(sender, _dragRow) || _dragRow.DataContext is not RuleRow row ||
            DataContext is not RulesViewModel { IsBusy: false }) return;
        Point now = e.GetPosition(this);
        if (Math.Abs(now.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var source = _dragRow; _dragRow = null;
        DragDiagnostics?.Reset();
        try
        {
            if (BeginRowLift(source, e.GetPosition(DragOverlay)))
                DragDrop.DoDragDrop(source, new DataObject(RuleDragFormat, row.Id), DragDropEffects.Move);
        }
        finally { EndRowDrag(); DragDiagnostics?.Report(); }
    }
    private void OnRowUp(object sender, MouseButtonEventArgs e) => EndRowDrag();
    private void OnRowUnloaded(object sender, RoutedEventArgs e) { DragDiagnostics?.Count(RuleDragCounter.RowsUnloaded); EndRowDrag(); }
    private void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        // WPF can announce Drop before routing the Drop event. Keep its prospective
        // position until that event consumes it; cancellation clears it immediately.
        if (e.EscapePressed || e.Action == DragAction.Cancel) EndRowDrag();
    }
    internal void EndRowDrag()
    {
        _dragRow = null;
        _lastEdgeScroll = null; _edgeDirection = 0;
        HideRowLift();
        _liftSource = null;
        ClearVisualOrder();
        LiftedRuleImage.Source = null;
        LiftTranslation.X = 0; LiftTranslation.Y = 0;
    }
    private bool TryDrop(DragEventArgs e, out Guid source)
    {
        source = e.Data.GetData(RuleDragFormat) is Guid id ? id : Guid.Empty;
        Guid candidate = source;
        return _liftSource?.DataContext is RuleRow captured && captured.Id == candidate &&
            DataContext is RulesViewModel { IsBusy: false } vm && vm.Rules.Any(r => r.Id == candidate);
    }
    private void OnRuleDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (!TryDrop(e, out _) || !new Rect(DragOverlay.RenderSize).Contains(e.GetPosition(DragOverlay))) { HideRowLift(); return; }
        e.Effects = DragDropEffects.Move;
        UpdateRowDrag(e.GetPosition(DragOverlay), e.GetPosition(RulesScroller).Y);
    }
    internal void UpdateRowDrag(Point pointer, double y)
    {
        DragDiagnostics?.Count(RuleDragCounter.PointerMoves);
        MoveRowLift(pointer); UpdateProspectiveOrder(pointer);
        int direction = y < 36 ? -1 : y > RulesScroller.ActualHeight - 36 ? 1 : 0;
        if (direction == 0) { _lastEdgeScroll = null; _edgeDirection = 0; return; }
        long now = DragClock.GetTimestamp();
        // Logical-pixel scrolling at at most 20 steps/s, not one jump per pointer event.
        // No catch-up burst after a busy dispatcher and no timer surviving a cancelled drag.
        if (direction == _edgeDirection && _lastEdgeScroll is long last &&
            DragClock.GetElapsedTime(last, now) < TimeSpan.FromMilliseconds(50)) return;
        _lastEdgeScroll = now; _edgeDirection = direction;
        DragDiagnostics?.Count(RuleDragCounter.ScrollRequests);
        RulesScroller.ScrollToVerticalOffset(RulesScroller.VerticalOffset + direction * 12);
    }
    private void OnRuleDragLeave(object sender, DragEventArgs e) => LeaveRowDrag(e.GetPosition(DragOverlay));
    internal void LeaveRowDrag(Point pointer)
    {
        if (!new Rect(0, 0, DragOverlay.ActualWidth, DragOverlay.ActualHeight).Contains(pointer))
        {
            HideRowLift(); _lastEdgeScroll = null; _edgeDirection = 0;
        }
        // Routed child/row leave events inside the viewport must not reset the gesture.
    }
    private async void OnRuleDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!TryDrop(e, out _) || !new Rect(DragOverlay.RenderSize).Contains(e.GetPosition(DragOverlay))) { EndRowDrag(); return; }
        UpdateProspectiveOrder(e.GetPosition(DragOverlay));
        await DropProspectiveOrderAsync();
    }
    internal async Task DropRuleAsync(Guid source, Guid target, bool after)
    {
        EndRowDrag();
        if (DataContext is not RulesViewModel vm) return;
        int index = vm.Rules.ToList().FindIndex(r => r.Id == target);
        if (index < 0 || source == target) return;
        Guid? before = after ? index + 1 < vm.Rules.Count ? vm.Rules[index + 1].Id : null : target;
        await CommitDropAsync(source, before);
    }
    private async Task CommitDropAsync(Guid source, Guid? before)
    {
        try
        {
            if (DataContext is not RulesViewModel vm) return;
            await vm.MoveBeforeAsync(source, before);
            FocusRow(source);
        }
        finally { EndRowDrag(); DragDiagnostics?.Report(RuleDragPhase.DropCompleted); }
    }
    private async void OnRowKey(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers != ModifierKeys.Alt || key is not (Key.Up or Key.Down) ||
            sender is not Border { DataContext: RuleRow row } surface ||
            !CanStartRowDrag(e.OriginalSource as DependencyObject, surface) ||
            DataContext is not RulesViewModel vm) return;
        e.Handled = true;
        var command = key == Key.Up ? vm.MoveUpCommand : vm.MoveDownCommand;
        if (command.CanExecute(row)) await command.ExecuteAsync(row);
        FocusRow(row.Id);
    }
    private void FocusRow(Guid id)
    {
        UpdateLayout();
        var row = Descendants(RuleRows).OfType<Border>().FirstOrDefault(b => b.Name == "RuleRowSurface" && b.DataContext is RuleRow r && r.Id == id);
        row?.BringIntoView(); row?.Focus();
    }
}
