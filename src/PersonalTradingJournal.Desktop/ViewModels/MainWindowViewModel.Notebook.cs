using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private NavigationDestination? _notebookTargetDestination;
    private Guid? _notebookTargetOrigin;
    public bool CanReturnToNotebook => _notebookTargetDestination == CurrentDestination && !_disposed &&
        _notebookTargetOrigin == _notebook?.SelectedNote?.Id;
    public IRelayCommand ReturnToNotebookCommand { get; }
    private void NotifyNotebookReturn()
    {
        if (_notebookTargetDestination != CurrentDestination) { _notebookTargetDestination = null; _notebookTargetOrigin = null; }
        OnPropertyChanged(nameof(CanReturnToNotebook)); ReturnToNotebookCommand.NotifyCanExecuteChanged();
    }
    private async Task<bool> OpenNotebookTargetAsync(NotebookWorkspaceViewModel.LinkedNavigationRequest request)
    {
        bool Owns() => !_disposed && CurrentDestination == NavigationDestination.Notebook && request.IsCurrent;
        if (_planNavigation || !Owns()) return false;
        var link = request.Link;
        _planNavigation = true;
        NavigationDestination? targetDestination = null;
        try
        {
            if (_tradingPlan is not null && !await _tradingPlan.PrepareLeaveAsync()) return false;
            if (!Owns() || !await _notebook!.PrepareLinkedLeaveAsync(request) || !Owns()) return false;
            var destination = link.Kind == NotebookTargetKind.Trade ? NavigationDestination.Trades : NavigationDestination.Setups;
            targetDestination = destination;
            // Continuations resume on the UI context. Validate the original owner and
            // commit synchronously; never recapture the loaded Note after a guard await.
            if (!Owns()) return false;
            NavigateCore(destination, null, commitGuard: Owns);
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
                await _notebook!.LoadTask;
                _notebook.ReportLinkedNavigationFailure();
                return false;
            }
            _notebook.CompleteLeave(); _tradingPlan?.CompleteLeave();
            _notebookTargetOrigin = request.NoteId;
            _notebookTargetDestination = destination; NotifyNotebookReturn();
            return true;
        }
        catch (Exception)
        {
            if (!_disposed && targetDestination == CurrentDestination)
            {
                NavigateCore(NavigationDestination.Notebook, null);
                await _notebook!.LoadTask;
                _notebook.ReportLinkedNavigationFailure();
            }
            return false;
        }
        finally
        {
            _tradingPlan?.ClearLeaveConsent();
            // A stale continuation must not clear consent belonging to a new selection.
            _notebook?.ClearLinkedLeaveConsent(request);
            _planNavigation = false;
        }
    }
}
