using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RulesView : UserControl
{
    private bool _syncing;
    public RulesView() => InitializeComponent();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || DataContext is not RulesViewModel vm || vm.IsBusy || RuleSelector.SelectedItem is not RuleRow row) return;
        if (!vm.TrySelect(row))
        {
            _syncing = true; RuleSelector.SetCurrentValue(ComboBox.SelectedValueProperty, vm.SelectedRule?.Id); _syncing = false;
        }
    }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RulesViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnStateChanged, "");
        if (e.NewValue is RulesViewModel current) PropertyChangedEventManager.AddHandler(current, OnStateChanged, "");
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(RulesViewModel.ValidationAttempt) or nameof(RulesViewModel.IsEditing) or nameof(RulesViewModel.IsHistorical))) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || !ReferenceEquals(DataContext, sender) || sender is not RulesViewModel vm) return;
            if (e.PropertyName == nameof(RulesViewModel.ValidationAttempt))
            {
                var first = Descendants(EditorFields).OfType<TextBox>().FirstOrDefault(FormValidation.GetIsInvalid);
                first?.BringIntoView(); first?.Focus();
            }
            else if (vm.IsHistorical) ReadingCard.BringIntoView();
            else if (vm.IsEditing) { AccountSelector.BringIntoView(); AccountSelector.Focus(); }
            else { SelectedName.BringIntoView(); if (EditRule.IsEnabled) EditRule.Focus(); else CreateRule.Focus(); }
        });
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0) return;
        // Match the Playbook editor: let text scroll locally, then hand the wheel to the page.
        for (DependencyObject? source = e.OriginalSource as DependencyObject; source is not null && source != RulesScroller;
             source = source is Visual ? VisualTreeHelper.GetParent(source) : null)
            if (source is TextBox box && (e.Delta > 0 ? box.VerticalOffset > 0 : box.VerticalOffset + box.ViewportHeight < box.ExtentHeight)) return;
        RulesScroller.ScrollToVerticalOffset(RulesScroller.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
