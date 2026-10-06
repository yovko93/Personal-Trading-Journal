using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed partial class CalendarViewModel
{
    private Guid? _inlineTradeId;
    private bool _inlineLoading, _inlineRefreshPending;
    public TradesViewModel? TradeEditor { get; private set; }
    public event EventHandler? TradeDataCommitted;
    public IRelayCommand CloseInlineTradeCommand { get; private set; } = null!;
    public bool HasInlineWork => _inlineLoading || TradeEditor is { IsTradeEditVisible: true } or { IsUpdatingTrade: true } or { IsTradeDetailLoading: true };

    private void InitializeInlineEditor(TradesViewModel? editor)
    {
        TradeEditor = editor;
        CloseInlineTradeCommand = new RelayCommand(CloseInlineDetails, () => !HasInlineWork);
        if (editor is null) return;
        editor.TradeDataCommitted += (_, _) =>
        {
            // The editor raises this only after a successful write. Wait until it finishes
            // reloading its own details before replacing Calendar rows or a draft form.
            _inlineRefreshPending = true;
            TradeDataCommitted?.Invoke(this, EventArgs.Empty);
        };
        editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not (nameof(editor.IsTradeEditVisible) or nameof(editor.IsUpdatingTrade) or nameof(editor.IsTradeDetailLoading))) return;
            NotifyInlineState();
            FlushInlineRefresh();
        };
    }

    private void NotifyInlineState()
    {
        ViewTradeCommand.NotifyCanExecuteChanged();
        CloseInlineTradeCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasInlineWork));
    }

    private void FlushInlineRefresh()
    {
        if (!_inlineRefreshPending || HasInlineWork || !_isActive) return;
        _inlineRefreshPending = false;
        _ = RefreshAsync();
    }

    private async Task ReloadExpandedTradeAsync(long generation)
    {
        if (TradeEditor is null || _inlineTradeId is not { } id ||
            DayTrades.FirstOrDefault(r => r.Trade.Id == id) is not { } row || HasInlineWork) return;
        _inlineLoading = true;
        NotifyInlineState();
        try
        {
            await TradeEditor.ShowTradeDetailCommand.ExecuteAsync(row.Trade);
            if (generation != _dayGeneration || !_isActive) CloseInlineDetails();
        }
        catch (OperationCanceledException) { }
        finally { _inlineLoading = false; NotifyInlineState(); FlushInlineRefresh(); }
    }

    private async Task ViewTradeAsync(TradeListItem? item)
    {
        if (item is null || !_isActive || IsDayLoading || HasInlineWork || !DayTrades.Any(row => row.Trade.Id == item.Id)) return;
        if (_inlineTradeId == item.Id) { CloseInlineDetails(); return; }
        CloseInlineDetails();
        _inlineTradeId = item.Id;
        foreach (var row in DayTrades) row.IsExpanded = row.Trade.Id == item.Id;
        if (TradeEditor is null) { DayErrorMessage = "Trade details are unavailable."; return; }
        _inlineLoading = true;
        NotifyInlineState();
        long generation = _dayGeneration;
        try
        {
            await TradeEditor.ShowTradeDetailCommand.ExecuteAsync(item);
            if (!_isActive || generation != _dayGeneration) CloseInlineDetails();
        }
        catch (OperationCanceledException) { }
        finally { _inlineLoading = false; NotifyInlineState(); FlushInlineRefresh(); }
    }

    public bool TryCloseDayDialog()
    {
        if (HasInlineWork)
        {
            DayErrorMessage = "Finish loading, or Save or Cancel your Trade edits before closing.";
            return false;
        }
        if (!TryCloseInlineJournal()) return false;
        CloseInlineDetails();
        return true;
    }

    private void CloseInlineDetails()
    {
        _inlineTradeId = null;
        foreach (var row in DayTrades) row.IsExpanded = false;
        TradeEditor?.ResetTransientState();
    }
}
