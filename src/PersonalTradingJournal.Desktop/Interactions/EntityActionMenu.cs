using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PersonalTradingJournal.Desktop.Interactions;

public static class EntityActionMenu
{
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

        button.Click -= OpenContextMenu;

        if (eventArgs.NewValue is true)
        {
            if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
            {
                AutomationProperties.SetName(button, "More actions");
            }

            button.ToolTip ??= "More actions";
            button.Click += OpenContextMenu;
        }
    }

    private static void OpenContextMenu(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not ButtonBase { ContextMenu: not null } button)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
        eventArgs.Handled = true;
    }
}
