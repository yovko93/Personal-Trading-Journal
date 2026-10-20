using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

/// <summary>Native ExpandCollapse automation and Space behavior; Enter applies only to its header.</summary>
public sealed class PlanSectionExpander : Expander
{
    private static readonly DependencyPropertyKey ActionDescriptionPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActionDescription), typeof(string), typeof(PlanSectionExpander), new PropertyMetadata("Expand section"));
    public static readonly DependencyProperty ActionDescriptionProperty = ActionDescriptionPropertyKey.DependencyProperty;
    public string ActionDescription => (string)GetValue(ActionDescriptionProperty);
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsExpandedProperty || e.Property == HeaderProperty)
            SetValue(ActionDescriptionPropertyKey, $"{(IsExpanded ? "Collapse" : "Expand")} {Header}");
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.OriginalSource is ToggleButton { Name: "HeaderToggle" } toggle && toggle.TemplatedParent == this)
        { SetCurrentValue(IsExpandedProperty, !IsExpanded); e.Handled = true; }
        base.OnKeyDown(e);
    }
}
