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
            TrackOverflowDismissal(trigger, trigger.ContextMenu);
        }
    }

    private Action TrackOverflowDismissal(Button trigger, ContextMenu menu)
    {
            // MenuBase dismisses on outside-down before the underlying Button.Click.
            // Remember only that pointer gesture, not a persistent "ignore next click".
            MouseButtonEventHandler outside = (_, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                        RememberOverflowDismissal(trigger, e.Timestamp, e.GetPosition(trigger));
                };
            MouseButtonEventHandler down = (_, e) =>
            {
                if (e.Timestamp != _overflowDismissalTimestamp || _dismissedOverflowTrigger != trigger)
                    _dismissedOverflowTrigger = null;
            };
            KeyEventHandler key = (_, _) => _dismissedOverflowTrigger = null;
            menu.AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent, outside, handledEventsToo: true);
            trigger.PreviewMouseLeftButtonDown += down;
            trigger.PreviewKeyDown += key;
            return () =>
            {
                menu.RemoveHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent, outside);
                trigger.PreviewMouseLeftButtonDown -= down;
                trigger.PreviewKeyDown -= key;
                if (_dismissedOverflowTrigger == trigger) _dismissedOverflowTrigger = null;
            };
    }

    internal void RememberOverflowDismissal(Button trigger, int timestamp, Point position)
    {
        if (!trigger.IsVisible || !new Rect(trigger.RenderSize).Contains(position)) return;
        _dismissedOverflowTrigger = trigger;
        _overflowDismissalTimestamp = timestamp;
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
        ReleaseTagMenu();
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
