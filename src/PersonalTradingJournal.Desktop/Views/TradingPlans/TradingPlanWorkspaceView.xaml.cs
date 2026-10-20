using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

public partial class TradingPlanWorkspaceView : UserControl
{
    private readonly PageFocus _focus;
    private PrincipleRow? _removingPrinciple;
    public TradingPlanWorkspaceView() { InitializeComponent(); _focus = new(this); }
    private void OnTopBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < 700;
        Grid.SetRow(TopActions, narrow ? 1 : 0); Grid.SetColumn(TopActions, narrow ? 0 : 1);
        Grid.SetColumnSpan(TopActions, narrow ? 2 : 1);
        TopActions.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _focus.Invalidate(); _removingPrinciple = null;
        if (e.OldValue is TradingPlanWorkspaceViewModel old)
        { PropertyChangedEventManager.RemoveHandler(old, OnState, ""); PropertyChangedEventManager.RemoveHandler(old.Principles, OnPrincipleState, ""); }
        if (e.NewValue is TradingPlanWorkspaceViewModel current)
        { PropertyChangedEventManager.AddHandler(current, OnState, ""); PropertyChangedEventManager.AddHandler(current.Principles, OnPrincipleState, ""); }
    }
    private void OnPrincipleState(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DailyTradingPrinciplesViewModel.IsBusy) && sender is DailyTradingPrinciplesViewModel { IsBusy: false })
            _removingPrinciple = null; // A failed/cancelled removal must not influence a later reload's focus.
        if (e.PropertyName == nameof(DailyTradingPrinciplesViewModel.ValidationAttempt))
            _focus.Request(() => PrincipleInput);
        if (e.PropertyName == nameof(DailyTradingPrinciplesViewModel.Rows) && _removingPrinciple is { } removed &&
            sender is DailyTradingPrinciplesViewModel vm && vm.Rows.All(r => r.Value.Id != removed.Value.Id))
        {
            _removingPrinciple = null;
            _focus.Request(() =>
            {
                var next = vm.Rows.ElementAtOrDefault(Math.Min(removed.Number - 1, vm.Rows.Count - 1));
                return next is null ? PrincipleInput : Descendants(PrincipleRows).OfType<Button>()
                    .FirstOrDefault(b => b.DataContext is PrincipleRow row && row.Value.Id == next.Value.Id);
            });
        }
    }
    private void OnPrincipleEntrySizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < 320;
        Grid.SetRow(PrincipleAdd, narrow ? 1 : 0); Grid.SetColumn(PrincipleAdd, narrow ? 0 : 1);
        Grid.SetColumnSpan(PrincipleInput, narrow ? 2 : 1);
        PrincipleAdd.HorizontalAlignment = narrow ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
    }
    private void OnPrincipleRemoveClick(object sender, RoutedEventArgs e)
    { _removingPrinciple = sender is Button b && (b.IsKeyboardFocusWithin || ReferenceEquals(FocusManager.GetFocusedElement(this), b)) ? b.DataContext as PrincipleRow : null; }
    private void OnState(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TradingPlanWorkspaceViewModel.DateValidationAttempt))
            _focus.Request(() => PlanDate.Template.FindName("RawDate", PlanDate) as FrameworkElement);
        if (e.PropertyName == nameof(TradingPlanWorkspaceViewModel.ReadinessFocusRequest) && DataContext is TradingPlanWorkspaceViewModel { Editor: { } editor })
        {
            if (editor.HasValidationErrors) return; // The owning component routes raw errors.
            string? field = editor.ReadinessBlockers.FirstOrDefault()?.Field;
            if (field == "Session") { _focus.Request(() => Session); return; }
            if (field is "Draft" or "Save") { _focus.Request(() => SaveDraft); return; }
            if (field == "Conflict") { _focus.Request(() => Reload); return; }
            if (field is "Instruments" || field?.StartsWith("Instruments[", StringComparison.Ordinal) == true) Preparation.ExpandSection();
            if (field is "Scenarios" or "StandAside" || field?.StartsWith("Scenarios[", StringComparison.Ordinal) == true) Scenarios.ExpandSection();
            if (field == "References") Risk.ExpandSection();
            object? target = field switch { "Focus" => editor.Focus, "StandAside" => editor.StandAside, _ => null };
            if (field?.StartsWith("Instruments[", StringComparison.Ordinal) == true)
            {
                int index = int.Parse(field.Split('[', ']')[1], System.Globalization.CultureInfo.InvariantCulture);
                editor.SelectedInstrument = editor.Contexts[index];
                if (field.EndsWith("MarketThesis", StringComparison.Ordinal)) target = editor.SelectedInstrument.Thesis;
                else { _focus.Request(() => Descendants(Preparation).OfType<ComboBox>().FirstOrDefault(c => c.Name == "BiasSelector")); return; }
            }
            if (field?.StartsWith("Scenarios[", StringComparison.Ordinal) == true)
            {
                int index = int.Parse(field.Split('[', ']')[1], System.Globalization.CultureInfo.InvariantCulture);
                var scenario = editor.Scenarios[index];
                target = field.Split('.')[1] switch { "Name" => scenario.Name, "Condition" => scenario.Condition,
                    "Response" => scenario.Response, "Invalidation" => scenario.Invalidation, _ => null };
                if (target is null) { _focus.Request(() => Descendants(Scenarios).OfType<Button>().FirstOrDefault(c => c.Name == "Direction" && ReferenceEquals(c.DataContext, scenario))); return; }
            }
            if (target is not null) _focus.Request(() => Descendants(this).OfType<TextBox>().FirstOrDefault(t => ReferenceEquals(t.DataContext, target)));
            else if (field == "Currency") _focus.Request(() => PlannedCurrency);
            else if (field == "Instruments")
            {
                ((Expander)Preparation.FindName("InstrumentPicker")).IsExpanded = true;
                _focus.Request(() => Descendants(Preparation).OfType<ComboBox>().FirstOrDefault(c => c.Name == "CatalogSelector"));
            }
            else if (field == "Scenarios") _focus.Request(() => Descendants(Scenarios).OfType<Button>().FirstOrDefault(b => Equals(b.Content, "Add Scenario")));
            else if (field == "References") _focus.Request(() => Descendants(Risk).OfType<Button>().FirstOrDefault(b => b.Command == editor.CheckReferencesCommand));
        }
    }
    private void OnDateKey(object sender, KeyEventArgs e)
    {
        if (DataContext is not TradingPlanWorkspaceViewModel vm) return;
        if (e.Key == Key.Enter && vm.OpenDateCommand.CanExecute(null)) { vm.OpenDateCommand.Execute(null); e.Handled = true; }
        if (e.Key == Key.F4) { PlanDate.IsDropDownOpen = !PlanDate.IsDropDownOpen; e.Handled = true; }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var item in Descendants(child)) yield return item;
        }
    }
}
