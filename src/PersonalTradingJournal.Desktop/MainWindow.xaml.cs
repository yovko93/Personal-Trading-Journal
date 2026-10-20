using PersonalTradingJournal.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace PersonalTradingJournal.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _preparingPlanClose, _planClosePrepared;
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel model && model.NeedsPlanClosePreparation && !_planClosePrepared)
        {
            // Do not raise ancestor/owned-window Closing while an async Save/Discard/Cancel decision
            // is pending. The second attempt still runs every guard and checks its final veto result.
            e.Cancel = true;
            if (!_preparingPlanClose) PreparePlanClose(model);
            return;
        }
        Dialogs.WindowCloseAttempt.Run(this, e, () => base.OnClosing(e),
            () => DataContext is not MainWindowViewModel vm || vm.CanCloseWindow(),
            () => { if (DataContext is MainWindowViewModel vm) vm.CompleteWindowClose(); });
        if (e.Cancel && DataContext is MainWindowViewModel cancelled) cancelled.CancelPlanClose();
    }
    private async void PreparePlanClose(MainWindowViewModel model)
    {
        _preparingPlanClose = true;
        try
        {
            if (await model.PreparePlanCloseAsync())
            {
                // The first OnClosing must unwind even when the dialog choice completed synchronously.
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                _planClosePrepared = true; Close();
            }
        }
        finally { _planClosePrepared = false; _preparingPlanClose = false; }
    }
}
