using System.Windows.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;

namespace PersonalTradingJournal.Desktop.Views.Breakdown;

public partial class BreakdownView : UserControl
{
    public BreakdownView() => InitializeComponent();
    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not BreakdownViewModel vm || !Enum.TryParse<BreakdownSortColumn>(e.Column.SortMemberPath, out var column)) return;
        vm.SortGroups(column);
        UpdateSortGlyphs();
    }
    private void OnTableLoaded(object sender, RoutedEventArgs e) => UpdateSortGlyphs();
    private void UpdateSortGlyphs()
    {
        if (DataContext is not BreakdownViewModel vm) return;
        foreach (var column in GroupTable.Columns)
            column.SortDirection = column.SortMemberPath == vm.SortColumn?.ToString()
                ? vm.SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending : null;
    }
    private void OnTableWheel(object sender, MouseWheelEventArgs e)
    {
        // Let the virtualized table scroll first. At its edge continue scrolling the page.
        if (e.Handled || e.Delta == 0 || Keyboard.Modifiers != ModifierKeys.None) return;
        var inner = FindScroll(GroupTable);
        if (inner is not null && (e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight)) return;
        e.Handled = true;
        PageScroll.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = PageScroll });
    }
    private static ScrollViewer? FindScroll(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer scroll) return scroll;
            if (FindScroll(child) is { } nested) return nested;
        }
        return null;
    }
}
