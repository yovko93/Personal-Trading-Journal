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

    private void OnDateValidationError(object? sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        if (DataContext is DashboardViewModel viewModel)
            viewModel.RejectInvalidDate(ReferenceEquals(sender, RangeStart));
    }
}
