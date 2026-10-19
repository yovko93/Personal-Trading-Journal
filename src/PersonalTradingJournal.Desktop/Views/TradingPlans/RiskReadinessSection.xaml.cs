using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

public partial class RiskReadinessSection : UserControl
{
    public static readonly DependencyProperty IsSectionExpandedProperty = DependencyProperty.Register(nameof(IsSectionExpanded), typeof(bool), typeof(RiskReadinessSection), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public bool IsSectionExpanded { get => (bool)GetValue(IsSectionExpandedProperty); set => SetValue(IsSectionExpandedProperty, value); }
    public void ExpandSection() => SetCurrentValue(IsSectionExpandedProperty, true);
    private readonly PageFocus _focus;
    public RiskReadinessSection() { InitializeComponent(); _focus = new(this); }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        LimitsGrid.Columns = ActualWidth >= 720 ? 4 : 2;
        LimitsAndRules.Margin = new(0);
    }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _focus.Invalidate();
        if (e.OldValue is SessionPreparationViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnStateChanged, "");
        if (e.NewValue is SessionPreparationViewModel current) PropertyChangedEventManager.AddHandler(current, OnStateChanged, "");
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SessionPreparationViewModel.ValidationAttempt) || sender is not SessionPreparationViewModel vm ||
            vm.FirstInvalidField is not { } field || !vm.RiskFields.Contains(field)) return;
        ExpandSection();
        _focus.Request(() => Descendants(this).OfType<TextBox>().FirstOrDefault(t => ReferenceEquals(t.DataContext, field)));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var next in Descendants(child)) yield return next;
        }
    }
}
