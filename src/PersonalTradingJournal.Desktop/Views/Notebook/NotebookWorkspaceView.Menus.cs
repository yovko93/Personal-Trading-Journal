using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView
{
    private Button? _dismissedOverflowTrigger;
    private int _overflowDismissalTimestamp;

    private void InitializeOverflowMenus()
    {
        foreach (var trigger in new[] { NoteOverflow })
        {
            // MenuBase dismisses on outside-down before the underlying Button.Click.
            // Remember only that pointer gesture, not a persistent "ignore next click".
            trigger.ContextMenu.AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler((_, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left && trigger.IsVisible &&
                        new Rect(trigger.RenderSize).Contains(e.GetPosition(trigger)))
                    {
                        _dismissedOverflowTrigger = trigger;
                        _overflowDismissalTimestamp = e.Timestamp;
                    }
                }), handledEventsToo: true);
            trigger.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (e.Timestamp != _overflowDismissalTimestamp || _dismissedOverflowTrigger != trigger)
                    _dismissedOverflowTrigger = null;
            };
            trigger.PreviewKeyDown += (_, _) => _dismissedOverflowTrigger = null;
        }
    }

    private void ToggleOverflowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } trigger) return;
        e.Handled = true;
        bool dismissedByThisPress = _dismissedOverflowTrigger == trigger;
        _dismissedOverflowTrigger = null;
        if (dismissedByThisPress || menu.IsOpen) { menu.IsOpen = false; return; }
        // Explicit exclusivity also covers keyboard/automation activation; native mouse
        // activation will normally have already dismissed the other captured menu.
        if (_tagMenu is not null) _tagMenu.IsOpen = false;
        menu.PlacementTarget = trigger;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
        // ContextMenu retains its own synchronous Escape/focus restoration. Do not queue
        // a focus request on outside dismissal, selection replacement or command completion.
    }

    private void CloseOverflowMenus()
    {
        _dismissedOverflowTrigger = null;
        NoteOverflow.ContextMenu.IsOpen = false;
    }
}
