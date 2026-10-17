using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Interactions;

/// <summary>Opt-in status text only: selection/sorting are not live announcements.</summary>
public static class LiveAnnouncement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string),
        typeof(LiveAnnouncement), new PropertyMetadata(null, Changed));
    public static string? GetText(DependencyObject value) => (string?)value.GetValue(TextProperty);
    public static void SetText(DependencyObject value, string? text) => value.SetValue(TextProperty, text);
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBlock { IsLoaded: true, IsVisible: true } text || string.IsNullOrWhiteSpace(e.NewValue as string)) return;
        AutomationProperties.SetName(text, (string)e.NewValue);
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            UIElementAutomationPeer.CreatePeerForElement(text)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
