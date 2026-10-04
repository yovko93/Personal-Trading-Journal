using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Views.Journals;

/// <summary>A focusable read-only row exposed to assistive technology, with no edit/invoke action.</summary>
public sealed class JournalTradeContextRow : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    private sealed class RowPeer(JournalTradeContextRow owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(JournalTradeContextRow);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
