using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RulesView : UserControl
{
    public RulesView() => InitializeComponent();
    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        // Moving a clean edit to Create can keep IsEditing=true. The new host still needs focus.
        if (ReferenceEquals(sender, e.OriginalSource) && DataContext is RulesViewModel vm)
            OnStateChanged(vm, new PropertyChangedEventArgs(vm.HasErrors ? nameof(RulesViewModel.ValidationAttempt) : nameof(RulesViewModel.IsEditing)));
    }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RulesViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnStateChanged, "");
        if (e.NewValue is RulesViewModel current) PropertyChangedEventManager.AddHandler(current, OnStateChanged, "");
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(RulesViewModel.ValidationAttempt) or nameof(RulesViewModel.IsEditing))) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || !ReferenceEquals(DataContext, sender) || sender is not RulesViewModel vm) return;
            if (e.PropertyName == nameof(RulesViewModel.ValidationAttempt))
            {
                var first = Descendants(this).OfType<TextBox>().FirstOrDefault(FormValidation.GetIsInvalid);
                first?.BringIntoView(); first?.Focus();
            }
            else if (vm.IsEditing)
            {
                UpdateLayout();
                var first = Descendants(this).OfType<TextBox>().FirstOrDefault();
                // Reveal the row header and first field together, not the entire tall form.
                if (vm.EditingRuleId is Guid id)
                {
                    var row = Descendants(RuleRows).OfType<Grid>().FirstOrDefault(g => g.Name == "RuleRowLayout" && g.DataContext is RuleRow r && r.Id == id);
                    if (row is not null && first is not null)
                        row.BringIntoView(new Rect(0, 0, row.ActualWidth, first.TranslatePoint(new Point(0, first.ActualHeight), row).Y));
                }
                first?.BringIntoView(); first?.Focus();
            }
            else
            {
                var edit = Descendants(RuleRows).OfType<Button>().FirstOrDefault(b => Equals(b.Content, "Edit") && b.DataContext is RuleRow r && r.Id == vm.SelectedRule?.Id);
                if (edit is not null) edit.Focus(); else CreateRule.Focus();
            }
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

/// <summary>Only the captured editing identity hosts the shared form, never a row index.</summary>
public sealed class RuleInlineEditorConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [Guid row, Guid editing, RulesViewModel vm] && row == editing ? vm : null;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
