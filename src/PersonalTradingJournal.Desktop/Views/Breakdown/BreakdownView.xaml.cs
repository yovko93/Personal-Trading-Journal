using System.Windows.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Desktop.Interactions;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;

namespace PersonalTradingJournal.Desktop.Views.Breakdown;

public partial class BreakdownView : UserControl
{
    private readonly PageFocus _focus;
    private BreakdownViewModel? _observed;
    public BreakdownView()
    {
        InitializeComponent();
        FromDate.CalendarClosed += OnDateCalendarClosed;
        ThroughDate.CalendarClosed += OnDateCalendarClosed;
        FocusManager.SetIsFocusScope(this, true);
        _focus = new(this);
        Loaded += (_, _) => { Observe(); _focus.Request(ReturnTarget); };
        Unloaded += (_, _) => { _focus.Invalidate(); Unobserve(); };
        DataContextChanged += (_, _) =>
        {
            _focus.Invalidate(); Unobserve();
            if (DataContext is BreakdownViewModel { CurrencyValidation: not null }) MoreFilters.IsExpanded = true;
            if (IsLoaded) Observe();
        };
    }
    private FrameworkElement ReturnTarget()
    {
        if (DataContext is BreakdownViewModel { SelectedGroup: not null })
            return SelectedTradesButton.IsEnabled ? SelectedTradesButton : ChartMetricSelector;
        return PeriodFilter;
    }
    private void Observe()
    {
        if (DataContext is not BreakdownViewModel vm || ReferenceEquals(vm, _observed)) return;
        _observed = vm; vm.FocusRequested += OnFocusRequested; vm.PropertyChanged += OnStateChanged;
    }
    private void Unobserve()
    {
        if (_observed is not { } vm) return;
        vm.FocusRequested -= OnFocusRequested; vm.PropertyChanged -= OnStateChanged; _observed = null;
    }
    private void OnFocusRequested(string name)
    {
        if (name is "CurrencyFilter" or "ModeFilter") MoreFilters.IsExpanded = true;
        _focus.Request(() => FindName(name) is DatePicker picker
            ? picker.Template.FindName("DraftDateText", picker) as FrameworkElement : FindName(name) as FrameworkElement);
    }
    private void OnMoreFilters(object sender, RoutedEventArgs e) => MoreFilters.IsExpanded = !MoreFilters.IsExpanded;
    private void OnDateKey(object sender, KeyEventArgs e)
    {
        if (sender is DatePicker picker && (e.Key == Key.F4 ||
            (e.Key == Key.System ? e.SystemKey : e.Key) == Key.Down && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)))
        { picker.IsDropDownOpen = !picker.IsDropDownOpen; e.Handled = true; }
    }
    private void OnDateCalendarClosed(object sender, RoutedEventArgs e)
    {
        if (sender is DatePicker picker && picker.IsLoaded)
            _focus.Request(() => picker.Template.FindName("DraftDateText", picker) as FrameworkElement);
    }
    private void OnMoreFiltersChanged(object sender, RoutedEventArgs e)
        => AutomationProperties.SetItemStatus(MoreFiltersButton, MoreFilters.IsExpanded ? "Expanded" : "Collapsed");
    private void OnKpiSizeChanged(object sender, SizeChangedEventArgs e)
        => KpiCards.Columns = e.NewSize.Width < 480 ? 1 : e.NewSize.Width < 900 ? 2 : 4;
    private void OnGroupingSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < 930;
        Grid.SetColumn(MetricControls, narrow ? 0 : 1); Grid.SetRow(MetricControls, narrow ? 1 : 0);
        MetricControls.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        MetricControls.Margin = new Thickness(narrow ? 0 : 10, 0, 0, 6);
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BreakdownViewModel.CurrencyValidation) && _observed?.CurrencyValidation is not null)
            MoreFilters.IsExpanded = true;
        if (e.PropertyName is nameof(BreakdownViewModel.SelectedGroup) or nameof(BreakdownViewModel.Chart) && _observed?.SelectedGroup is null &&
            (ComparisonChart.IsKeyboardFocusWithin || GroupTable.IsKeyboardFocusWithin || SelectedTradesButton.IsKeyboardFocused ||
             ReferenceEquals(FocusManager.GetFocusedElement(this), SelectedTradesButton)))
            _focus.Request(() => ChartMetricSelector);
    }
    private void OnComparisonSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < 900;
        ChartColumn.Width = new GridLength(narrow ? 1 : 2, GridUnitType.Star);
        DetailsColumn.Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(DetailsCard, narrow ? 0 : 1); Grid.SetRow(DetailsCard, narrow ? 1 : 0);
        DetailsCard.Margin = narrow ? new Thickness(0, 12, 0, 0) : new Thickness(12, 0, 0, 0);
    }
    private void OnChartSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Only move this logical viewport, never BringIntoView on the enclosing page.
        int index = ComparisonChart.SelectedIndex;
        if (index < 0 || FindScroll(ComparisonChart) is not { } scroll) return;
        if (index < scroll.VerticalOffset) scroll.ScrollToVerticalOffset(index);
        else if (index >= scroll.VerticalOffset + scroll.ViewportHeight)
            scroll.ScrollToVerticalOffset(index - Math.Max(0, scroll.ViewportHeight - 1));
    }
    private void OnChartWheel(object sender, MouseWheelEventArgs e) => ForwardWheelAtEdge(ComparisonChart, e);
    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not BreakdownViewModel vm || !Enum.TryParse<BreakdownSortColumn>(e.Column.SortMemberPath, out var column)) return;
        vm.SortGroups(column);
        UpdateSortGlyphs();
    }
    private void OnTableLoaded(object sender, RoutedEventArgs e) => UpdateSortGlyphs();
    private void OnTableSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep financial columns readable and the action at the right. A star column in a
        // horizontally scrolling, virtualized DataGrid can retain its minimum after sorting.
        var group = GroupTable.Columns.Single(c => c.SortMemberPath == nameof(BreakdownSortColumn.Group));
        double otherWidths = GroupTable.Columns.Where(c => c != group).Sum(c => c.Width.Value);
        group.Width = new DataGridLength(Math.Max(group.MinWidth,
            e.NewSize.Width - otherWidths - 2 - SystemParameters.VerticalScrollBarWidth));
    }
    private void UpdateSortGlyphs()
    {
        if (DataContext is not BreakdownViewModel vm) return;
        foreach (var column in GroupTable.Columns)
            column.SortDirection = column.SortMemberPath == vm.SortColumn?.ToString()
                ? vm.SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending : null;
        foreach (var header in Headers(GroupTable))
            AutomationProperties.SetItemStatus(header, header.Column?.SortDirection?.ToString() ?? "Not sorted");
    }
    private static IEnumerable<DataGridColumnHeader> Headers(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is DataGridColumnHeader header) yield return header;
            foreach (var nested in Headers(child)) yield return nested;
        }
    }
    private void OnTableWheel(object sender, MouseWheelEventArgs e)
        => ForwardWheelAtEdge(GroupTable, e);
    private void ForwardWheelAtEdge(DependencyObject control, MouseWheelEventArgs e)
    {
        // Let the virtualized table scroll first. At its edge continue scrolling the page.
        if (e.Handled || e.Delta == 0 || Keyboard.Modifiers != ModifierKeys.None) return;
        var inner = FindScroll(control);
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
