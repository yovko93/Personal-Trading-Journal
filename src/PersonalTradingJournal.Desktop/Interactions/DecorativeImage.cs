using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Interactions;

/// <summary>An image whose meaning is already conveyed by adjacent accessible text.</summary>
public sealed class DecorativeImage : Image
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorativeImagePeer(this);

    private sealed class DecorativeImagePeer(DecorativeImage owner) : ImageAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;
        protected override bool IsContentElementCore() => false;
    }
}
