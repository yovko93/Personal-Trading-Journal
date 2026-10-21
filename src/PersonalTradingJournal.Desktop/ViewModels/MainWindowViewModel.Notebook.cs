using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private NavigationDestination? _notebookTargetDestination;
    public bool CanReturnToNotebook => _notebookTargetDestination == CurrentDestination && !_disposed;
    public IRelayCommand ReturnToNotebookCommand { get; }
    private void NotifyNotebookReturn()
    {
        if (_notebookTargetDestination != CurrentDestination) _notebookTargetDestination = null;
        OnPropertyChanged(nameof(CanReturnToNotebook)); ReturnToNotebookCommand.NotifyCanExecuteChanged();
    }
    private async Task<bool> OpenNotebookTargetAsync(NotebookLink link, CancellationToken ct)
    {
        if (_disposed || _planNavigation || CurrentDestination != NavigationDestination.Notebook || _notebook?.Note is not { } note) return false;
        // Capture before the save/consent await. Existing shell guards remain authoritative.
        _planNavigation = true;
        NavigationDestination? targetDestination = null;
        try
        {
            if (!await PreparePlanCloseAsync() || _disposed || ct.IsCancellationRequested || _notebook.Note?.Id != note.Id) return false;
            var destination = link.Kind == NotebookTargetKind.Trade ? NavigationDestination.Trades : NavigationDestination.Setups;
            targetDestination = destination;
            NavigateCore(destination, null);
            if (CurrentDestination != destination) return false;
            // Notebook departure cancels its read lifetime. The destination owns its own detail read.
            bool opened;
            if (destination == NavigationDestination.Trades)
            {
                await _tradeNavigationLoad;
                if (_disposed || CurrentDestination != destination) return false;
                await _tradesViewModel.ShowTradeByIdAsync(link.TargetId);
                opened = _tradesViewModel.SelectedTradeDetail?.Id == link.TargetId;
            }
            else
            {
                opened = await _tradingSetupsViewModel.OpenDetailsByIdAsync(link.TargetId);
            }
            if (_disposed || CurrentDestination != destination) return false;
            if (!opened)
            {
                // Keep uncommitted discard consent/drafts until destination success is established.
                NavigateCore(NavigationDestination.Notebook, null);
                await _notebook.LoadTask;
                _notebook.ReportLinkedNavigationFailure();
                return false;
            }
            _notebook.CompleteLeave(); _tradingPlan?.CompleteLeave();
            _notebookTargetDestination = destination; NotifyNotebookReturn();
            return true;
        }
        catch (Exception)
        {
            if (!_disposed && targetDestination == CurrentDestination)
            {
                NavigateCore(NavigationDestination.Notebook, null);
                await _notebook.LoadTask;
                _notebook.ReportLinkedNavigationFailure();
            }
            return false;
        }
        finally { CancelPlanClose(); _planNavigation = false; }
    }
}
