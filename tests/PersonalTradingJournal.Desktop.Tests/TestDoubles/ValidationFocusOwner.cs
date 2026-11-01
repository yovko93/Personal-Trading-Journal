using System.Windows;
using System.Windows.Input;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>One-time fixture setup, followed by read-only ownership checks.
/// Validation must move focus from the neutral window to the invalid field itself.</summary>
internal static class ValidationFocusOwner
{
    internal static void Ready(Window owner, FrameworkElement scope)
    {
        Assert.Same(owner, Window.GetWindow(scope));
        Assert.Same(owner.Dispatcher, scope.Dispatcher);
        UiKeyboardProbe.Ready(owner); // Existing bounded setup; never focus the expected validation result.
        AssertReady(owner, scope);
        Assert.Same(owner, Keyboard.FocusedElement);
    }

    internal static void AssertReady(Window owner, FrameworkElement target)
    {
        owner.Dispatcher.VerifyAccess();
        Assert.Same(System.Windows.Application.Current!.Dispatcher, owner.Dispatcher);
        Assert.False(owner.Dispatcher.HasShutdownStarted);
        Assert.False(owner.Dispatcher.HasShutdownFinished);
        Assert.True(owner.IsLoaded && owner.IsVisible && owner.IsActive, "Validation owner lost readiness; do not reactivate during the operation.");
        Assert.Same(owner, Window.GetWindow(target));
        Assert.Same(owner.Dispatcher, target.Dispatcher);
        Assert.True(target.IsLoaded && target.IsVisible && target.IsEnabled);
        Assert.NotNull(PresentationSource.FromVisual(target));
        Assert.Same(PresentationSource.FromVisual(owner), PresentationSource.FromVisual(target));
        if (target is System.Windows.Controls.TextBox) Assert.True(target.Focusable);
    }
}
