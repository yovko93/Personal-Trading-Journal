using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PersonalTradingJournal.Desktop.Interactions;

public static class EntityActionMenu
{
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(ToggleState), typeof(EntityActionMenu));
    public static readonly DependencyProperty OpensContextMenuProperty =
        DependencyProperty.RegisterAttached(
            "OpensContextMenu",
            typeof(bool),
            typeof(EntityActionMenu),
            new PropertyMetadata(false, OnOpensContextMenuChanged));

    public static bool GetOpensContextMenu(DependencyObject element) =>
        (bool)element.GetValue(OpensContextMenuProperty);

    public static void SetOpensContextMenu(DependencyObject element, bool value) =>
        element.SetValue(OpensContextMenuProperty, value);

    private static void OnOpensContextMenuChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not ButtonBase button)
        {
            throw new ArgumentException(
                "EntityActionMenu.OpensContextMenu can only be used on a button.",
                nameof(dependencyObject));
        }

        (button.GetValue(StateProperty) as ToggleState)?.Dispose();
        button.ClearValue(StateProperty);

        if (eventArgs.NewValue is true)
        {
            if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
            {
                AutomationProperties.SetName(button, "More actions");
            }

            button.ToolTip ??= "More actions";
            button.SetValue(StateProperty, new ToggleState(button));
        }
    }

    private sealed class ToggleState : IDisposable
    {
        private readonly ButtonBase _button;
        private ContextMenu? _menu;
        private bool _dismissedByPress;
        private int _dismissalTimestamp;

        internal ToggleState(ButtonBase button)
        {
            _button = button;
            button.Click += Toggle;
            button.PreviewMouseDown += PreviewDown;
            button.PreviewKeyDown += PreviewKey;
            button.ContextMenuOpening += Opening;
            button.Loaded += Loaded;
            button.Unloaded += Unloaded;
            TrackMenu();
        }

        private void TrackMenu()
        {
            if (ReferenceEquals(_menu, _button.ContextMenu)) return;
            UntrackMenu();
            _menu = _button.ContextMenu;
            _menu?.AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler(OutsideDown), handledEventsToo: true);
        }

        private void UntrackMenu()
        {
            _menu?.RemoveHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler(OutsideDown));
            _menu = null;
            _dismissedByPress = false;
        }

        private void OutsideDown(object sender, MouseButtonEventArgs e)
        {
            // MenuBase dismisses on outside-down, before the trigger's Button.Click.
            // Remember that same press, rather than reopening the just-dismissed menu.
            if (e.ChangedButton == MouseButton.Left && _button.IsVisible &&
                new Rect(_button.RenderSize).Contains(e.GetPosition(_button)))
            {
                _dismissedByPress = true;
                _dismissalTimestamp = e.Timestamp;
            }
        }

        private void PreviewDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Timestamp != _dismissalTimestamp) _dismissedByPress = false;
        }
        private void PreviewKey(object sender, KeyEventArgs e) => _dismissedByPress = false;
        private void Opening(object sender, ContextMenuEventArgs e) => TrackMenu();
        private void Loaded(object sender, RoutedEventArgs e) => TrackMenu();
        private void Unloaded(object sender, RoutedEventArgs e) => UntrackMenu();

        private void Toggle(object sender, RoutedEventArgs e)
        {
            TrackMenu();
            if (_menu is null) return;
            bool close = _menu.IsOpen || _dismissedByPress;
            _dismissedByPress = false;
            _menu.PlacementTarget = _button;
            _menu.Placement = PlacementMode.Bottom;
            _menu.IsOpen = !close;
            e.Handled = true;
        }

        public void Dispose()
        {
            UntrackMenu();
            _button.Click -= Toggle;
            _button.PreviewMouseDown -= PreviewDown;
            _button.PreviewKeyDown -= PreviewKey;
            _button.ContextMenuOpening -= Opening;
            _button.Loaded -= Loaded;
            _button.Unloaded -= Unloaded;
        }
    }
}
