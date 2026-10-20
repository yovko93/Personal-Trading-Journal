using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.Validation;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

public partial class SessionPreparationSection : UserControl
{
    public static readonly DependencyProperty IsSectionExpandedProperty = DependencyProperty.Register(nameof(IsSectionExpanded), typeof(bool), typeof(SessionPreparationSection), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public bool IsSectionExpanded { get => (bool)GetValue(IsSectionExpandedProperty); set => SetValue(IsSectionExpandedProperty, value); }
    public void ExpandSection() => SetCurrentValue(IsSectionExpandedProperty, true);
    private readonly PageFocus _focus;
    public SessionPreparationSection() { InitializeComponent(); _focus = new(this); }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = ActualWidth < 760;
        Grid.SetColumn(LevelFields, narrow ? 0 : 1);
        Grid.SetRow(LevelFields, narrow ? 1 : 0);
        Grid.SetColumnSpan(ContextFields, narrow ? 2 : 1);
        Grid.SetColumnSpan(LevelFields, narrow ? 2 : 1);
        ContextFields.Margin = narrow ? new Thickness(0, 0, 0, 16) : new Thickness(0, 0, 14, 0);
    }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _focus.Invalidate();
        if (e.OldValue is SessionPreparationViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnStateChanged, "");
        if (e.NewValue is SessionPreparationViewModel current) PropertyChangedEventManager.AddHandler(current, OnStateChanged, "");
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SessionPreparationViewModel.ValidationAttempt) || sender is not SessionPreparationViewModel vm) return;
        if (vm.RiskFields.Contains(vm.FirstInvalidField!)) return;
        if (vm.FirstInvalidField == vm.StandAside || vm.Scenarios.Any(s => s.Fields.Contains(vm.FirstInvalidField!))) return;
        // Errors in a nonselected context must be made visible, not silently saved from an older value.
        var field = vm.FirstInvalidField;
        var invalid = vm.Contexts.FirstOrDefault(c => c.Fields.Contains(field!));
        if (invalid is not null) vm.SelectedInstrument = invalid;
        // Wait for the selected context's bindings/layout and respect any newer user input.
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
