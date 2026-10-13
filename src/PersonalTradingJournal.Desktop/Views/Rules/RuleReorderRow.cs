using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows;
using PersonalTradingJournal.Desktop.Diagnostics;
using PersonalTradingJournal.Desktop.ViewModels.Rules;

namespace PersonalTradingJournal.Desktop.Views.Rules;

/// <summary>A focusable list row, not a checkbox or a statement of rule adherence.</summary>
public sealed class RuleReorderRow : Border
{
    protected override Size MeasureOverride(Size constraint)
    {
        (Tag as RulesViewModel)?.DragDiagnostics?.Count(RuleDragCounter.RowMeasures);
        return base.MeasureOverride(constraint);
    }
    protected override Size ArrangeOverride(Size size)
    {
        (Tag as RulesViewModel)?.DragDiagnostics?.Count(RuleDragCounter.RowArranges);
        return base.ArrangeOverride(size);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(RuleReorderRow owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(RuleReorderRow);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override bool IsKeyboardFocusableCore() => owner.Focusable && owner.IsEnabled;
        protected override bool HasKeyboardFocusCore() => owner.IsKeyboardFocused;
    }
}
