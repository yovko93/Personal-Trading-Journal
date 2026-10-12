using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

public partial class RuleAssessmentView : UserControl
{
    private bool _syncing;
    public RuleAssessmentView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is RuleAssessmentViewModel old) old.PropertyChanged -= OnModelChanged;
            if (e.NewValue is RuleAssessmentViewModel current) current.PropertyChanged += OnModelChanged;
        };
    }
    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RuleAssessmentViewModel.HasValidation) || sender is not RuleAssessmentViewModel { HasValidation: true }) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (DataContext is not RuleAssessmentViewModel { HasValidation: true }) return;
            Explanation.BringIntoView(); Explanation.Focus();
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(Explanation), Explanation);
        }));
    }
    private void OnRuleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || DataContext is not RuleAssessmentViewModel vm || vm.IsBusy) return;
        if (!vm.TrySelect(RuleSelector.SelectedItem as RuleRow))
        { _syncing = true; RuleSelector.SelectedValue = vm.SelectedRule?.Id; _syncing = false; }
    }
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0) return;
        for (DependencyObject? source = e.OriginalSource as DependencyObject; source is not null && source != PageScroll;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : null)
            if (source is TextBox box && (e.Delta > 0 ? box.VerticalOffset > 0 : box.VerticalOffset + box.ViewportHeight < box.ExtentHeight)) return;
        PageScroll.ScrollToVerticalOffset(PageScroll.VerticalOffset - e.Delta); e.Handled = true;
    }
}
