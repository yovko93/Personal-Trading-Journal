using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PersonalTradingJournal.Desktop.Interactions;

/// <summary>View-owned, one-shot focus requests. Never focus a replaced page or interrupt newer input.</summary>
internal sealed class PageFocus
{
    private readonly FrameworkElement owner;
    private long _version;
    public PageFocus(FrameworkElement owner)
    {
        this.owner = owner;
        owner.PreviewKeyDown += (_, _) => Invalidate();
        owner.PreviewMouseDown += (_, _) => Invalidate();
        owner.PreviewMouseWheel += (_, _) => Invalidate();
    }
    public void Invalidate() => _version++;
    public void Request(Func<FrameworkElement?> target)
    {
        long version = ++_version;
        object context = owner.DataContext;
        var input = Keyboard.FocusedElement;
        var logicalInput = FocusManager.GetFocusedElement(owner);
        owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (version != _version || !owner.IsLoaded || !owner.IsVisible ||
                !ReferenceEquals(context, owner.DataContext) || !UnchangedOrRemoved(input, Keyboard.FocusedElement) ||
                !UnchangedOrRemoved(logicalInput, FocusManager.GetFocusedElement(owner))) return;
            if (target() is not { IsVisible: true, IsEnabled: true, Focusable: true } element) return;
            element.BringIntoView();
            FocusManager.SetFocusedElement(owner, element);
            if (Window.GetWindow(owner)?.IsActive == true) element.Focus();
        });
    }
    private static bool UnchangedOrRemoved(IInputElement? before, IInputElement? current) =>
        ReferenceEquals(before, current) || before is UIElement { IsVisible: false } hidden &&
        (current is null || current is UIElement ancestor && ancestor.IsAncestorOf(hidden));
}
