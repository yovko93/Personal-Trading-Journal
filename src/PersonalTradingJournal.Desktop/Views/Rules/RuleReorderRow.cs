using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Views.Rules;

/// <summary>A focusable list row, not a checkbox or a statement of rule adherence.</summary>
public sealed class RuleReorderRow : Border
{
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
