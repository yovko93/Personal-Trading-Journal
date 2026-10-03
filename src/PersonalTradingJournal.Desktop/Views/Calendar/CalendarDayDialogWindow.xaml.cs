using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

public partial class CalendarDayDialogWindow : Window
{
    private bool _backdropPressed;
    public CalendarDayDialogWindow()
    {
        InitializeComponent();
        Closing += (_, e) =>
        {
            if (DataContext is ViewModels.Calendar.CalendarViewModel vm && !vm.TryCloseDayDialog()) e.Cancel = true;
        };
    }
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnDialogMouseDown(object sender, MouseButtonEventArgs e)
    {
        // A sibling of DayPanel, not an ancestor: content, scrollbars and popups never use this route.
        _backdropPressed = e.ChangedButton == MouseButton.Left && ReferenceEquals(e.OriginalSource, DialogBackdrop)
            && !IsPointOnOpenToolTip(e.GetPosition(this));
        if (_backdropPressed) e.Handled = true;
    }

    private void OnDialogMouseUp(object sender, MouseButtonEventArgs e)
    {
        bool dismiss = _backdropPressed && e.ChangedButton == MouseButton.Left && ReferenceEquals(e.OriginalSource, DialogBackdrop)
            && !IsPointOnOpenToolTip(e.GetPosition(this));
        _backdropPressed = false;
        if (!dismiss) return;
        // Close on release, not press: the owner must stay disabled for BOTH halves of the click,
        // since Calendar cells select on mouse-up. Consume even when inline edits veto closing.
        e.Handled = true;
        Close();
    }

    internal bool IsPointOnOpenToolTip(Point position)
    {
        var screen = PointToScreen(position);
        return ContainsTooltip(DayPanel);

        bool ContainsTooltip(DependencyObject element)
        {
            // Chart tips intentionally do not capture input. Their popup can extend over
            // the backdrop; use actual screen bounds rather than treating that as an outside click.
            if (element is FrameworkElement { ToolTip: ToolTip { IsOpen: true } tip } &&
                PresentationSource.FromVisual(tip) is not null &&
                new Rect(new Point(), tip.RenderSize).Contains(tip.PointFromScreen(screen))) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                if (ContainsTooltip(VisualTreeHelper.GetChild(element, i))) return true;
            return false;
        }
    }

    internal void FitToOwner()
    {
        if (Owner?.Content is not FrameworkElement { IsLoaded: true } content) return;
        // PointToScreen is in device pixels; Window positions and layout use DIPs.
        // Cover the owner's client area, including a maximized or high-DPI owner.
        var fromDevice = PresentationSource.FromVisual(Owner)?.CompositionTarget?.TransformFromDevice;
        if (fromDevice is not { } transform) return;
        var origin = transform.Transform(content.PointToScreen(new Point()));
        Left = origin.X;
        Top = origin.Y;
        Width = content.ActualWidth;
        Height = content.ActualHeight;
    }
    private void OnDialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }
}
