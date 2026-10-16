using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Navigation;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed partial class TradesViewModel
{
    private BreakdownTradeContext? _breakdownScope;
    private TradeListQuery? _ordinaryQuery;
    private long _browseEpoch;
    private bool _browseActive = true;
    private bool _browseDisposed;
    private CancellationTokenSource _browseCancellation = new();
    public BreakdownTradeContext? BreakdownScope => _breakdownScope;
    public bool HasBreakdownScope => _breakdownScope is not null;
    public string EmptyTradeListMessage => HasBreakdownScope
        ? "No original contributors are currently available. Refresh this list or return to Breakdown to refresh the analysis."
        : "No trades recorded yet.";
    public string BreakdownScopeSummary => _breakdownScope is { } scope
        ? $"Breakdown · {scope.GroupingLabel}: {scope.GroupLabel}\n{scope.AppliedContext}\n{TotalCount} currently available of {scope.OriginalCount} original contributors · {scope.FinancialSource}. Current records, not historical values. New matching Trades are excluded." : "";
    public Func<Task>? ReturnToBreakdownAsync { get; set; }
    public IAsyncRelayCommand BackToBreakdownCommand { get; private set; } = null!;
    public IAsyncRelayCommand ExitBreakdownScopeCommand { get; private set; } = null!;
    private void InitializeBreakdownCommands()
    {
        BackToBreakdownCommand = new AsyncRelayCommand(async () =>
        {
            if (HasBreakdownScope && TryLeaveBreakdownScope() && ReturnToBreakdownAsync is { } back) await back();
        });
        ExitBreakdownScopeCommand = new AsyncRelayCommand(ExitBreakdownScopeAsync);
    }
    public bool TryLeaveBreakdownScope()
    {
        if (!HasBreakdownScope) return true;
        if (IsTradeFormVisible || IsCloseTradeVisible || IsAddScreenshotVisible || IsSaving || IsUpdatingTrade || IsDeletingTrade || IsTradingSetupSaving || IsAddingScreenshot || IsDeletingScreenshot || IsClosingTrade || IsTradeDetailLoading || IsTradeScreenshotsLoading || IsTradeMistakesLoading || IsScreenshotPreviewLoading)
        {
            TradeListErrorMessage = "Finish or cancel the current Trade operation before leaving the Breakdown list. Your entered work is retained.";
            return false;
        }
        return true;
    }
    private void InvalidateBrowse(bool invalidateData = true)
    {
        _browseEpoch++;
        var previous = _browseCancellation; _browseCancellation = new(); previous.Cancel(); previous.Dispose();
        if (invalidateData) _hasTradeListLoadedSuccessfully = false;
        IsTradeListLoading = false;
    }
    public void ActivateBrowse() { if (!_browseDisposed) _browseActive = true; }
    public void DeactivateBrowse()
    {
        if (_browseDisposed) return;
        _browseActive = false; InvalidateBrowse(IsTradeListLoading || HasBreakdownScope);
        ShowTradeDetailCommand.Cancel(); ShowTradeEditCommand.Cancel(); ShowSelectedTradeEditCommand.Cancel();
        IsTradeDetailLoading = false;
    }
    public void DisposeBrowse()
    {
        if (_browseDisposed) return;
        DeactivateBrowse(); _browseDisposed = true; _browseCancellation.Dispose();
    }
    public bool SetBreakdownScope(BreakdownTradeContext scope)
    {
        if (_browseDisposed || !TryLeaveBreakdownScope()) return false;
        if (_breakdownScope?.NavigationId == scope.NavigationId) return false;
        _ordinaryQuery ??= CreateTradeListQuery();
        InvalidateBrowse(); ResetTransientState();
        _breakdownScope = scope; CurrentPage = 1;
        CurrentSortColumn = TradeListSortColumn.OpenedAtUtc; CurrentSortDirection = TradeListSortDirection.Descending;
        RecentTrades = []; TotalCount = 0; TradeListErrorMessage = null;
        NotifyBreakdownScope(); return true;
    }
    public async Task ExitBreakdownScopeAsync()
    {
        if (_browseDisposed || !HasBreakdownScope || !TryLeaveBreakdownScope()) return;
        InvalidateBrowse(); ResetTransientState(); _breakdownScope = null;
        var query = _ordinaryQuery; _ordinaryQuery = null;
        CurrentPage = query?.PageNumber ?? 1; CurrentSortColumn = query?.SortColumn ?? TradeListSortColumn.OpenedAtUtc;
        CurrentSortDirection = query?.SortDirection ?? TradeListSortDirection.Descending;
        RecentTrades = []; TotalCount = 0; NotifyBreakdownScope();
        await LoadTradeListAsync(true, CancellationToken.None);
    }
    private void NotifyBreakdownScope()
    {
        OnPropertyChanged(nameof(HasBreakdownScope)); OnPropertyChanged(nameof(BreakdownScope)); OnPropertyChanged(nameof(BreakdownScopeSummary));
        OnPropertyChanged(nameof(EmptyTradeListMessage));
    }
}
