using System.Windows;
using System.Windows.Automation;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

public partial class CalendarDayDialogWindow
{
    private Window? _subscribedOwner;
    private bool _minimizedWithOwner;
    private double _restorePanelWidth, _restorePanelHeight;
    private Thickness _restorePanelMargin;
    internal bool IsPanelMaximized { get; private set; }

    private void AttachOwner()
    {
        if (Owner is not { } owner || _subscribedOwner is not null) return;
        _subscribedOwner = owner;
        owner.SizeChanged += OnOwnerSizeChanged;
        owner.LocationChanged += OnOwnerLocationChanged;
        owner.StateChanged += OnOwnerStateChanged;
    }

    private void DetachOwner()
    {
        if (_subscribedOwner is not { } owner) return;
        owner.SizeChanged -= OnOwnerSizeChanged;
        owner.LocationChanged -= OnOwnerLocationChanged;
        owner.StateChanged -= OnOwnerStateChanged;
        _subscribedOwner = null;
    }

    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e) => FitToOwner();
    private void OnOwnerLocationChanged(object? sender, EventArgs e) => FitToOwner();
    private void OnOwnerStateChanged(object? sender, EventArgs e)
    {
        if (Owner is not { } owner) return;
        if (owner.WindowState == WindowState.Minimized)
        {
            _minimizedWithOwner = true;
            WindowState = WindowState.Minimized;
            return;
        }
        if (_minimizedWithOwner)
        {
            _minimizedWithOwner = false;
            WindowState = WindowState.Normal;
            Activate();
        }
        FitToOwner();
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        // Do not close/hide or end ShowDialog: its owner stays disabled and drafts stay live.
        // Windows restores the owner from its taskbar button; StateChanged brings this modal back.
        if (Owner is { } owner)
        {
            _minimizedWithOwner = true;
            owner.WindowState = WindowState.Minimized;
        }
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
    {
        // Maximize the PANEL, not the transparent Window to the monitor. Keep the backdrop
        // within the owner's client area and a small dismissal inset in either state.
        if (!IsPanelMaximized)
        {
            _restorePanelWidth = DayPanel.MaxWidth;
            _restorePanelHeight = DayPanel.MaxHeight;
            _restorePanelMargin = DayPanel.Margin;
            DayPanel.MaxWidth = DayPanel.MaxHeight = double.PositiveInfinity;
            DayPanel.Margin = new Thickness(8);
        }
        else
        {
            DayPanel.MaxWidth = _restorePanelWidth;
            DayPanel.MaxHeight = _restorePanelHeight;
            DayPanel.Margin = _restorePanelMargin;
        }
        IsPanelMaximized = !IsPanelMaximized;
        MaximizeButton.Content = IsPanelMaximized ? "❐" : "□";
        MaximizeButton.ToolTip = IsPanelMaximized ? "Restore Day Performance" : "Maximize Day Performance within the application";
        AutomationProperties.SetName(MaximizeButton, IsPanelMaximized ? "Restore Day Performance" : "Maximize Day Performance");
    }
}
