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
    public TradingPlanWorkspaceView() { InitializeComponent(); _focus = new(this); }
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _focus.Invalidate();
        if (e.OldValue is TradingPlanWorkspaceViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnState, "");
        if (e.NewValue is TradingPlanWorkspaceViewModel current) PropertyChangedEventManager.AddHandler(current, OnState, "");
    }
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
                if (target is null) { _focus.Request(() => Descendants(Scenarios).OfType<ComboBox>().FirstOrDefault(c => c.Name == "Direction" && ReferenceEquals(c.DataContext, scenario))); return; }
            }
            if (target is not null) _focus.Request(() => Descendants(this).OfType<TextBox>().FirstOrDefault(t => ReferenceEquals(t.DataContext, target)));
            else if (field?.StartsWith("Checklist.", StringComparison.Ordinal) == true)
                _focus.Request(() => Descendants(Risk).OfType<CheckBox>().FirstOrDefault(c => c.IsChecked != true));
            else if (field == "Instruments") _focus.Request(() => Descendants(Preparation).OfType<ComboBox>().FirstOrDefault(c => c.Name == "CatalogSelector"));
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
