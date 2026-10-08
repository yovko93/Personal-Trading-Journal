using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Views.Journals;

public partial class InlineJournalView : UserControl
{
    public InlineJournalView() => InitializeComponent();

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || (Keyboard.Modifiers & ModifierKeys.Shift) != 0) return;
        for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null && node != this;
             node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is ScrollViewer inner && inner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled &&
                (e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight)) return;
        for (DependencyObject? node = VisualTreeHelper.GetParent(this); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not ScrollViewer page || page.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled ||
                !(e.Delta > 0 ? page.VerticalOffset > 0 : page.VerticalOffset < page.ScrollableHeight)) continue;
            e.Handled = true;
            page.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                { RoutedEvent = MouseWheelEvent, Source = page });
            return;
        }
    }

    private void OnFormVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (InlineForm.IsVisible)
                InlineForm.BringIntoView(new Rect(0, 0, InlineForm.ActualWidth, Math.Min(InlineForm.ActualHeight, 260)));
        }));
    }
}
