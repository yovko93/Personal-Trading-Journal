using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Interactions;

/// <summary>A command button whose expansion state comes only from its guarded host.
/// Unlike ToggleButton, a click does not optimistically change state before a close veto.</summary>
public sealed class ExpansionButton : Button
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(ExpansionButton), new PropertyMetadata(false, OnExpansionChanged));

    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpansionPeer(this);

    protected override void OnClick()
    {
        Focus(); // The row survives collapse; keep keyboard focus on its existing action.
        base.OnClick();
    }

    private static void OnExpansionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (UIElementAutomationPeer.FromElement((ExpansionButton)sender) is { } peer)
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                (bool)e.OldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                (bool)e.NewValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }

    private sealed class ExpansionPeer(ExpansionButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);
        public ExpandCollapseState ExpandCollapseState => owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        public void Expand() => SetExpanded(true);
        public void Collapse() => SetExpanded(false);
        private void SetExpanded(bool expanded)
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            if (owner.IsExpanded != expanded) ((IInvokeProvider)this).Invoke();
        }
    }
}
