using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.ViewModels.DailyReview;

/// <summary>Local reads and explicit single-analysis deletion only. No generation/provider dependency.</summary>
public sealed class DailyReviewViewModel : ObservableObject
{
    private readonly IDailyReviewEvidenceReader _reader;
    private readonly ICoachingAnalysisRepository _history;
    private readonly ITradingAccountReader _accounts;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _clock;
    private CancellationTokenSource? _read, _detailRead;
    private long _generation, _detailGeneration;
    private bool _active, _loading, _deleting;
    private DateTime? _date;
    private ReviewAccount _account = new(null, "All accounts");
    private int _page = 1;
    private CoachingAnalysisHistoryPage? _historyPage;

    public DailyReviewViewModel(IDailyReviewEvidenceReader reader, ICoachingAnalysisRepository history,
        ITradingAccountReader accounts, IDialogService dialogs, TimeProvider clock)
    {
        _reader = reader; _history = history; _accounts = accounts; _dialogs = dialogs; _clock = clock;
        _date = Today.ToDateTime(TimeOnly.MinValue);
        RefreshCommand = new AsyncRelayCommand(() => StartLoadAsync(false), AsyncRelayCommandOptions.AllowConcurrentExecutions);
        TodayCommand = new RelayCommand(() => SelectedDate = Today.ToDateTime(TimeOnly.MinValue));
        CancelLoadCommand = new RelayCommand(() =>
        {
            CancelLoad(); DetailMessage = "Loading cancelled. Refresh to load the selected day."; Notify();
        }, () => IsLoading);
        PreviousCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => !IsLoading && _historyPage?.HasPrevious == true);
        NextCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => !IsLoading && _historyPage?.HasNext == true);
        OpenAnalysisCommand = new AsyncRelayCommand<ReviewAnalysisRow>(OpenAnalysisAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CloseAnalysisCommand = new RelayCommand(CloseAnalysis);
        DeleteAnalysisCommand = new AsyncRelayCommand<ReviewAnalysisRow>(DeleteAnalysisAsync, row => row is not null && !_deleting);
        OpenTradeCommand = new AsyncRelayCommand<ReviewTradeRow>(async row =>
        {
            if (row is null || OpenTradeAsync is null) return;
            try { await OpenTradeAsync(row.Id); }
            catch (Exception) { CurrentError = "Trade navigation failed. Refresh current evidence and try again."; Notify(); }
        });
        OpenJournalCommand = new AsyncRelayCommand<ReviewJournalRow>(async row =>
        {
            if (row is null || OpenJournalAsync is null) return;
            try { await OpenJournalAsync(row.Source); }
            catch (Exception) { CurrentError = "Journal navigation failed. Your journal has not been changed."; Notify(); }
        });
    }

    private DateOnly Today => DateOnly.FromDateTime(TradingTimePolicy.ConvertUtcToTradingTime(_clock.GetUtcNow()).DateTime);
    public DateTime? SelectedDate
    {
        get => _date;
        set { if (SetProperty(ref _date, value)) { _page = 1; LoadTask = StartLoadAsync(false); } }
    }
    public ReviewAccount SelectedAccount
    {
        get => _account;
        set { if (value is not null && SetProperty(ref _account, value)) { _page = 1; LoadTask = StartLoadAsync(false); } }
    }
    public IReadOnlyList<ReviewAccount> Accounts { get; private set; } = [new(null, "All accounts")];
    public ReviewEvidencePresentation? Current { get; private set; }
    public IReadOnlyList<ReviewAnalysisRow> Analyses { get; private set; } = [];
    public ReviewSnapshot? Snapshot { get; private set; }
    public string? CurrentError { get; private set; }
    public string? HistoryError { get; private set; }
    public string? DetailMessage { get; private set; }
    public string? AccountNotice { get; private set; }
    public bool IsLoading => _loading;
    public string LoadingText => IsLoading ? "Loading selected day…" : "";
    public string HistoryLabel => $"Saved AI analysis history · {_historyPage?.TotalCount.ToString() ?? "—"} analyses · page {_page}";
    public string HistoryEmptyText => !IsLoading && HistoryError is null && _historyPage?.TotalCount == 0
        ? "No saved AI analyses for this date and analysis scope. This workspace does not generate reviews." : "";
    public string HistoryScope => SelectedAccount.Id is null
        ? "All accounts history contains aggregate analyses only. Select an account for its exact-account analyses."
        : "Exact-account analysis history. Original scope and names are retained in each snapshot.";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public Func<Guid, Task>? OpenTradeAsync { get; set; }
    public Func<DailyReviewJournalEvidence, Task>? OpenJournalAsync { get; set; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand TodayCommand { get; }
    public IRelayCommand CancelLoadCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand<ReviewAnalysisRow> OpenAnalysisCommand { get; }
    public IRelayCommand CloseAnalysisCommand { get; }
    public IAsyncRelayCommand<ReviewAnalysisRow> DeleteAnalysisCommand { get; }
    public IAsyncRelayCommand<ReviewTradeRow> OpenTradeCommand { get; }
    public IAsyncRelayCommand<ReviewJournalRow> OpenJournalCommand { get; }

    public Task ActivateAsync() { _active = true; return LoadTask = StartLoadAsync(false); }
    public void Deactivate() { _active = false; CancelLoad(); }
    public void OnDataCommitted() { if (_active) LoadTask = StartLoadAsync(false); }

    private void CancelLoad()
    {
        _generation++; _read?.Cancel(); _read = null; _loading = false;
        CloseAnalysis(); Notify();
    }
    private void CloseAnalysis()
    {
        _detailGeneration++; _detailRead?.Cancel(); _detailRead = null;
        Snapshot = null; DetailMessage = null; Notify();
    }
    private Task ChangePageAsync(int delta)
    {
        _page += delta;
        return LoadTask = StartLoadAsync(true);
    }

    private async Task StartLoadAsync(bool historyOnly)
    {
        CancelLoad();
        if (!historyOnly) Current = null;
        Analyses = []; _historyPage = null; HistoryError = null;
        if (!historyOnly) CurrentError = AccountNotice = null;
        if (!_active) { Notify(); return; }
        if (SelectedDate is not { } selected)
        {
            CurrentError = "Select a valid New York date."; Notify(); return;
        }
        long generation = _generation;
        Guid? account = SelectedAccount.Id;
        int page = _page;
        using var cancellation = new CancellationTokenSource();
        _read = cancellation; _loading = true; Notify();
        bool IsCurrent() => generation == _generation && _active && !cancellation.IsCancellationRequested;
        try
        {
            var query = new DailyReviewQuery(DateOnly.FromDateTime(selected), account);
            async Task CurrentAsync()
            {
                if (historyOnly) return;
                try
                {
                    var result = await Task.Run(async () =>
                    {
                        var accounts = await _accounts.GetAllAsync(cancellation.Token);
                        var evidence = await _reader.GetAsync(query, cancellation.Token);
                        var statistics = DailyReviewStatisticsCalculator.Calculate(evidence, cancellation.Token);
                        return (accounts, evidence, statistics);
                    }, cancellation.Token);
                    if (!IsCurrent()) return;
                    var options = new List<ReviewAccount> { new(null, "All accounts") };
                    options.AddRange(result.accounts.OrderBy(a => a.Name, StringComparer.CurrentCulture).ThenBy(a => a.Id)
                        .Select(a => new ReviewAccount(a.Id, a.Name + (a.IsActive ? "" : " (inactive)"))));
                    if (account.HasValue && options.All(a => a.Id != account))
                    {
                        options.Add(new(account, SelectedAccount.Label + " (unavailable)"));
                        AccountNotice = "Selected account is unavailable. Its exact scope is retained; saved history remains readable.";
                    }
                    Accounts = options;
                    _account = options.Single(a => a.Id == account);
                    Current = new(result.evidence, result.statistics, canNavigate: true);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception) { if (IsCurrent()) CurrentError = "Current evidence could not be loaded. Refresh to retry; saved history is independent."; }
            }
            async Task HistoryAsync()
            {
                try
                {
                    var result = await Task.Run(async () =>
                    {
                        var loaded = await _history.BrowseAsync(new(query.Date, new(account), page, 10), cancellation.Token);
                        if (page > 1 && loaded.Items.Count == 0)
                            loaded = await _history.BrowseAsync(new(query.Date, new(account), Math.Max(1, (loaded.TotalCount + 9) / 10), 10), cancellation.Token);
                        return loaded;
                    }, cancellation.Token);
                    if (!IsCurrent()) return;
                    _historyPage = result; _page = result.Page;
                    Analyses = result.Items.Select(a => new ReviewAnalysisRow(a)).ToArray();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception) { if (IsCurrent()) HistoryError = "Saved history could not be loaded. Refresh to retry."; }
            }
            await Task.WhenAll(CurrentAsync(), HistoryAsync());
        }
        catch (ArgumentOutOfRangeException) { if (IsCurrent()) CurrentError = "Select a supported New York date."; }
        finally
        {
            if (generation == _generation) { _read = null; _loading = false; Notify(); }
        }
    }

    private async Task OpenAnalysisAsync(ReviewAnalysisRow? row)
    {
        if (row is null || !Analyses.Contains(row) || !_active) return;
        CloseAnalysis();
        long generation = _generation, detail = _detailGeneration;
        using var cancellation = new CancellationTokenSource();
        _detailRead = cancellation; DetailMessage = "Loading saved snapshot…"; Notify();
        try
        {
            var saved = await Task.Run(() => _history.GetAsync(row.Source.Id, cancellation.Token), cancellation.Token);
            var snapshot = saved is null ? null : await Task.Run(() => ReviewSnapshot.Read(saved), cancellation.Token);
            if (generation != _generation || detail != _detailGeneration || !_active || cancellation.IsCancellationRequested) return;
            Snapshot = snapshot;
            DetailMessage = saved is null ? "This saved analysis no longer exists. Refresh history." : null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (generation == _generation && detail == _detailGeneration && _active)
                DetailMessage = "Saved snapshot could not be opened (unavailable, damaged, or unsupported contract). Current records were not substituted. Refresh and try again.";
        }
        finally { if (detail == _detailGeneration) { _detailRead = null; Notify(); } }
    }

    private async Task DeleteAnalysisAsync(ReviewAnalysisRow? row)
    {
        if (row is null || !Analyses.Contains(row) || !_active || _deleting) return;
        if (!_dialogs.Confirm(new("Delete saved AI analysis?",
            $"Permanently delete analysis {row.Source.Id} for {row.Source.ReviewDate:yyyy-MM-dd} New York?\n{row.Scope}\n{row.Heading}\nOnly this saved response and evidence snapshot are removed. Trades and Journals are unchanged.",
            "Delete analysis", isDestructive: true))) return;
        long generation = _generation;
        _deleting = true; DeleteAnalysisCommand.NotifyCanExecuteChanged();
        try
        {
            bool removed = await Task.Run(() => _history.DeleteAsync(row.Source.Id));
            if (generation != _generation || !_active) return;
            await (LoadTask = StartLoadAsync(true));
            // A selection change during refresh must not receive this operation's message.
            if (_active && _generation == generation + 1)
                DetailMessage = removed ? "Saved analysis deleted. Source Trades and Journals are unchanged." : "Analysis was already removed. History refreshed.";
        }
        catch (Exception) { if (generation == _generation && _active) DetailMessage = "Deletion failed. The analysis may still exist; refresh history before trying again."; }
        finally { _deleting = false; DeleteAnalysisCommand.NotifyCanExecuteChanged(); Notify(); }
    }

    private void Notify()
    {
        // One coherent publish after each generation; no old rows remain under a new date/scope.
        OnPropertyChanged(string.Empty);
        PreviousCommand?.NotifyCanExecuteChanged(); NextCommand?.NotifyCanExecuteChanged(); CancelLoadCommand?.NotifyCanExecuteChanged();
    }
}
