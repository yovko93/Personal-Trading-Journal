using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
namespace PersonalTradingJournal.Desktop.Views.Settings;
public partial class MaintenanceWindow : Window
{
    public MaintenanceWindow() => InitializeComponent();
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosing(CancelEventArgs e)
    { base.OnClosing(e); if (DataContext is MaintenanceViewModel vm && !vm.TryClose()) e.Cancel = true; }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    { if (e.Key == Key.Escape) { e.Handled = true; Close(); } base.OnPreviewKeyDown(e); }
}
