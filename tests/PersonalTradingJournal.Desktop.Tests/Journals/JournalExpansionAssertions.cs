using System.Reflection;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Interactions;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

internal static class JournalExpansionAssertions
{
    public static void State(Button button, bool expanded)
    {
        var toggle = Assert.IsType<ExpansionButton>(button);
        Assert.Equal(expanded, toggle.IsExpanded);
        var peer = UIElementAutomationPeer.CreatePeerForElement(toggle);
        var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.Equal(expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
        Assert.StartsWith(expanded ? "Close" : "Open", AutomationProperties.GetName(button));
        if (expanded)
        {
            Assert.Equal(((SolidColorBrush)button.FindResource("PtjDangerBrush")).Color, ((SolidColorBrush)button.Foreground).Color);
            Assert.Equal(Colors.Transparent, ((SolidColorBrush)button.Background).Color);
        }
    }

    // Execute the actual Button click path, including focus and its bound command.
    public static void Click(Button button) => typeof(ExpansionButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
}
