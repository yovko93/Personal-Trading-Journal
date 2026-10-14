using PersonalTradingJournal.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace PersonalTradingJournal.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Dialogs.WindowCloseAttempt.Run(this, e, () => base.OnClosing(e),
            () => DataContext is not MainWindowViewModel vm || vm.CanCloseWindow(),
            () => { if (DataContext is MainWindowViewModel vm) vm.CompleteWindowClose(); });
    }
}
