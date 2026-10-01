using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Interactions;

/// <summary>Lets a horizontal-only table pass ordinary vertical wheel input to its page.</summary>
public static class NestedTableWheelRouting
{
    public static readonly DependencyProperty ForwardVerticalWheelProperty = DependencyProperty.RegisterAttached(
        "ForwardVerticalWheel", typeof(bool), typeof(NestedTableWheelRouting),
        new PropertyMetadata(false, OnForwardVerticalWheelChanged));

    public static bool GetForwardVerticalWheel(DependencyObject element) =>
        (bool)element.GetValue(ForwardVerticalWheelProperty);

    public static void SetForwardVerticalWheel(DependencyObject element, bool value) =>
        element.SetValue(ForwardVerticalWheelProperty, value);

    private static void OnForwardVerticalWheelChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ScrollViewer viewer)
            throw new ArgumentException("NestedTableWheelRouting.ForwardVerticalWheel requires a ScrollViewer.", nameof(element));

        viewer.PreviewMouseWheel -= ForwardVerticalWheel;
        if (e.NewValue is true) viewer.PreviewMouseWheel += ForwardVerticalWheel;
    }

    private static void ForwardVerticalWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || (Keyboard.Modifiers & ModifierKeys.Shift) != 0) return;

        var table = (ScrollViewer)sender;
        for (DependencyObject? parent = VisualTreeHelper.GetParent(table); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is not ScrollViewer page || page.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
                continue;
            bool canMove = e.Delta > 0 ? page.VerticalOffset > 0 : page.VerticalOffset < page.ScrollableHeight;
            if (!canMove) continue;

            e.Handled = true;
            page.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = page,
            });
            return;
        }
    }
}
