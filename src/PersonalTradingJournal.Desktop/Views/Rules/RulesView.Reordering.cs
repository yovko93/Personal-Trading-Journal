using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RulesView
{
    private const string RuleDragFormat = "PTJ.RuleOrder";
    private Point _dragStart;
    private Button? _dragHandle;
    private void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        // Only this explicit handle owns drag initiation. Row actions and editor fields never do.
        _dragHandle = (Button)sender; _dragStart = e.GetPosition(this);
    }
    private void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { _dragHandle = null; return; }
        if (!ReferenceEquals(sender, _dragHandle) || _dragHandle.DataContext is not RuleRow row) return;
        Point now = e.GetPosition(this);
        if (Math.Abs(now.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var handle = _dragHandle; _dragHandle = null;
        try { DragDrop.DoDragDrop(handle, new DataObject(RuleDragFormat, row.Id), DragDropEffects.Move); }
        finally { ClearInsertion(); }
    }
    private void ClearInsertion()
    {
        foreach (var line in Descendants(RuleRows).OfType<Border>().Where(b => b.Name == "InsertionIndicator"))
            line.Visibility = Visibility.Collapsed;
    }
    private bool TryDrop(DragEventArgs e, out Guid source)
    {
        source = e.Data.GetData(RuleDragFormat) is Guid id ? id : Guid.Empty;
        Guid candidate = source;
        return DataContext is RulesViewModel { IsBusy: false } vm && vm.Rules.Any(r => r.Id == candidate);
    }
    private void OnRuleDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true; ClearInsertion(); e.Effects = DragDropEffects.None;
        if (!TryDrop(e, out _) || sender is not Grid grid) return;
        e.Effects = DragDropEffects.Move;
        ShowInsertion(grid, e.GetPosition(grid).Y >= grid.RowDefinitions[0].ActualHeight / 2);
        double y = e.GetPosition(RulesScroller).Y;
        if (y < 36) RulesScroller.LineUp();
        else if (y > RulesScroller.ActualHeight - 36) RulesScroller.LineDown();
    }
    internal static void ShowInsertion(Grid grid, bool after)
    {
        var line = Descendants(grid).OfType<Border>().First(b => b.Name == "InsertionIndicator");
        line.VerticalAlignment = after ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        line.Visibility = Visibility.Visible;
    }
    private void OnRuleDragLeave(object sender, DragEventArgs e) => ClearInsertion();
    private async void OnRuleDrop(object sender, DragEventArgs e)
    {
        e.Handled = true; ClearInsertion();
        if (!TryDrop(e, out Guid source) || sender is not Grid { DataContext: RuleRow target } grid) return;
        await DropRuleAsync(source, target.Id, e.GetPosition(grid).Y >= grid.RowDefinitions[0].ActualHeight / 2);
    }
    internal async Task DropRuleAsync(Guid source, Guid target, bool after)
    {
        if (DataContext is not RulesViewModel vm) return;
        int index = vm.Rules.ToList().FindIndex(r => r.Id == target);
        if (index < 0 || source == target) return;
        Guid? before = after ? index + 1 < vm.Rules.Count ? vm.Rules[index + 1].Id : null : target;
        await vm.MoveBeforeAsync(source, before);
        FocusHandle(source);
    }
    private async void OnHandleKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down) || sender is not Button { DataContext: RuleRow row } ||
            DataContext is not RulesViewModel vm) return;
        e.Handled = true;
        var command = e.Key == Key.Up ? vm.MoveUpCommand : vm.MoveDownCommand;
        if (command.CanExecute(row)) await command.ExecuteAsync(row);
        FocusHandle(row.Id);
    }
    private void FocusHandle(Guid id)
    {
        UpdateLayout();
        var handle = Descendants(RuleRows).OfType<Button>().FirstOrDefault(b => b.Name == "RuleDragHandle" && b.DataContext is RuleRow r && r.Id == id);
        handle?.BringIntoView(); handle?.Focus();
    }
}
