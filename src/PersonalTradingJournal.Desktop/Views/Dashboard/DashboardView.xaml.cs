using System.Windows.Controls;
using System.Windows;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel viewModel) await viewModel.ActivateAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel viewModel) viewModel.Deactivate();
    }

    private void OnRangeKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && DataContext is DashboardViewModel viewModel)
        {
            viewModel.CancelRangeCommand.Execute(null);
            e.Handled = true;
            DateRangeControl.Focus();
        }
    }

    private void OnDailyHelpGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is Button { ToolTip: ToolTip tip }) tip.IsOpen = true;
    }

    private void OnDailyHelpLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is Button { ToolTip: ToolTip tip }) tip.IsOpen = false;
    }
}
