using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RulesView
{
    private const string RuleDragFormat = "PTJ.RuleOrder";
    private Point _dragStart;
    private Border? _dragRow;

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
        try
        {
            if (BeginRowLift(source, e.GetPosition(DragOverlay)))
                DragDrop.DoDragDrop(source, new DataObject(RuleDragFormat, row.Id), DragDropEffects.Move);
        }
        finally { EndRowDrag(); }
    }
    private void OnRowUp(object sender, MouseButtonEventArgs e) => EndRowDrag();
    private void OnRowUnloaded(object sender, RoutedEventArgs e) => EndRowDrag();
    private void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed || e.Action != DragAction.Continue) EndRowDrag();
    }
    internal void EndRowDrag()
    {
        _dragRow = null;
        HideRowLift();
        _liftSource = null;
        LiftedRuleImage.Source = null;
    }
    private void ClearDropHighlights()
    {
        foreach (var highlight in Descendants(RuleRows).OfType<Border>().Where(b => b.Name == "DropHighlight"))
            highlight.Visibility = Visibility.Collapsed;
    }
    private bool TryDrop(DragEventArgs e, out Guid source)
    {
        source = e.Data.GetData(RuleDragFormat) is Guid id ? id : Guid.Empty;
        Guid candidate = source;
        return DataContext is RulesViewModel { IsBusy: false } vm && vm.Rules.Any(r => r.Id == candidate);
    }
    private void OnRuleDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true; ClearDropHighlights(); e.Effects = DragDropEffects.None;
        if (!TryDrop(e, out _) || sender is not Border { Child: Grid grid }) { HideRowLift(); return; }
        e.Effects = DragDropEffects.Move;
        MoveRowLift(e.GetPosition(DragOverlay));
        ShowDropHighlight(grid);
        double y = e.GetPosition(RulesScroller).Y;
        if (y < 36) RulesScroller.LineUp();
        else if (y > RulesScroller.ActualHeight - 36) RulesScroller.LineDown();
    }
    internal static void ShowDropHighlight(Grid grid) =>
        Descendants(grid).OfType<Border>().First(b => b.Name == "DropHighlight").Visibility = Visibility.Visible;
    private void OnRuleDragLeave(object sender, DragEventArgs e) => HideRowLift();
    private async void OnRuleDrop(object sender, DragEventArgs e)
    {
        e.Handled = true; EndRowDrag();
        if (!TryDrop(e, out Guid source) || sender is not Border { Child: Grid { DataContext: RuleRow target } grid }) return;
        await DropRuleAsync(source, target.Id, e.GetPosition(grid).Y >= grid.RowDefinitions[0].ActualHeight / 2);
    }
    internal async Task DropRuleAsync(Guid source, Guid target, bool after)
    {
        EndRowDrag();
        try
        {
            if (DataContext is not RulesViewModel vm) return;
            int index = vm.Rules.ToList().FindIndex(r => r.Id == target);
            if (index < 0 || source == target) return;
            Guid? before = after ? index + 1 < vm.Rules.Count ? vm.Rules[index + 1].Id : null : target;
            await vm.MoveBeforeAsync(source, before);
            FocusRow(source);
        }
        finally { EndRowDrag(); }
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
