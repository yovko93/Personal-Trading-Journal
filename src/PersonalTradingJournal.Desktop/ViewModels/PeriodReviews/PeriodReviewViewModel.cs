using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;

namespace PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

/// <summary>Separate selections/lifetimes, shared behavior. No generation from activation or refresh.</summary>
public sealed class PeriodReviewWorkspaces
{
    public PeriodReviewWorkspaces(IPeriodComparisonEvidenceReader reader, IPeriodReviewRepository history,
        ITradingAccountReader accounts, IDialogService dialogs, TimeProvider clock,
        GenerateAndSavePeriodReviewService generator, ICoachingCredentials credentials, IDailyJournalRepository journals)
    {
        Weekly = new(ReviewPeriodKind.Week, reader, history, accounts, dialogs, clock, generator.GenerateAsync, credentials, journals);
        Monthly = new(ReviewPeriodKind.Month, reader, history, accounts, dialogs, clock, generator.GenerateAsync, credentials, journals);
    }
    public PeriodReviewWorkspaces(PeriodReviewViewModel weekly, PeriodReviewViewModel monthly) { Weekly = weekly; Monthly = monthly; }
    public PeriodReviewViewModel Weekly { get; }
    public PeriodReviewViewModel Monthly { get; }
    public IEnumerable<PeriodReviewViewModel> All => [Weekly, Monthly];
}

public sealed class PeriodReviewViewModel : ObservableObject
{
    private readonly IPeriodComparisonEvidenceReader _reader;
    private readonly IPeriodReviewRepository _history;
    private readonly ITradingAccountReader _accounts;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _clock;
    private readonly Func<PeriodReviewQuery, CancellationToken, Task<SavedPeriodGenerationResult>> _generate;
    private readonly ICoachingCredentials _credentials;
    private readonly IDailyJournalRepository? _journals;
    private CancellationTokenSource? _load, _detail, _request;
    private long _version, _detailVersion, _requestVersion;
    private bool _active, _loading, _generating, _deleting;
    private DateTime? _date;
    private ReviewAccount _account = new(null, "All accounts");
    private int _page = 1, _scopePage = 1;
    private PeriodReviewHistoryPage? _historyPage;
    private SavedPeriodScopePage? _scopes;

    public PeriodReviewViewModel(ReviewPeriodKind kind, IPeriodComparisonEvidenceReader reader, IPeriodReviewRepository history,
        ITradingAccountReader accounts, IDialogService dialogs, TimeProvider clock,
        Func<PeriodReviewQuery, CancellationToken, Task<SavedPeriodGenerationResult>> generate,
        ICoachingCredentials credentials, IDailyJournalRepository? journals = null)
    {
        Kind = kind; _reader = reader; _history = history; _accounts = accounts; _dialogs = dialogs; _clock = clock;
        _generate = generate; _credentials = credentials; _journals = journals; _date = Today.ToDateTime(TimeOnly.MinValue);
        RefreshCommand = new AsyncRelayCommand(() => LoadTask = LoadAsync(), () => !_deleting, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        TodayCommand = new RelayCommand(() => SelectedDate = Today.ToDateTime(TimeOnly.MinValue), () => !_deleting);
        PreviousPeriodCommand = new RelayCommand(() => MovePeriod(-1), () => !_deleting);
        NextPeriodCommand = new RelayCommand(() => MovePeriod(1), () => !_deleting);
        PreviousHistoryCommand = new AsyncRelayCommand(() => Page(-1), () => !IsBusy && _historyPage?.HasPrevious == true);
        NextHistoryCommand = new AsyncRelayCommand(() => Page(1), () => !IsBusy && _historyPage?.HasNext == true);
        PreviousScopesCommand = new AsyncRelayCommand(() => ScopePage(-1), () => !IsBusy && _scopePage > 1);
        NextScopesCommand = new AsyncRelayCommand(() => ScopePage(1), () => !IsBusy && (long)_scopePage * 25 < (_scopes?.TotalCount ?? 0));
        CancelLoadCommand = new RelayCommand(() => { CancelLoad(); Message = "Loading cancelled. Refresh to load this selection."; Notify(); }, () => IsLoading);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, () => _active && !IsBusy && Query is not null && !_account.IsHistorical && !NeedsConfiguration);
        CancelGenerationCommand = new RelayCommand(CancelGeneration, () => _generating && _request?.IsCancellationRequested == false);
        ConfigureCommand = new RelayCommand(() => OpenSettings?.Invoke());
        OpenCommand = new AsyncRelayCommand<PeriodHistoryRow>(OpenAsync, row => row is not null && !_deleting, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CloseCommand = new RelayCommand(() => { ClearDetail(); Notify(); });
        DeleteCommand = new AsyncRelayCommand<PeriodHistoryRow>(DeleteAsync, row => row is not null && !IsBusy);
        OpenCitationCommand = new RelayCommand<PeriodCitation>(c => { Citation = c; Notify(); });
        OpenTradeCommand = new AsyncRelayCommand<PeriodCitation>(async c =>
        {
            if (IsBusy || c?.TradeId is not { } id || OpenTradeAsync is null) return;
            try { await OpenTradeAsync(id); }
            catch (Exception) { Message = "The exact Trade could not be opened. Saved evidence is unchanged."; Notify(); }
        }, c => c?.HasTrade == true && !IsBusy);
        OpenJournalCommand = new AsyncRelayCommand<PeriodCitation>(async c =>
        {
            if (IsBusy || c?.Journal is not { } j || OpenJournalAsync is null || _journals is null) return;
            try
            {
                var current = await _journals.GetAsync(j.TradingDate, j.TradingAccountId);
                if (current?.Entry.Id != j.JournalId) { Message = "The original Journal is unavailable or moved. No replacement was opened; the saved snapshot remains readable."; Notify(); return; }
                await OpenJournalAsync(j);
            }
            catch (Exception) { Message = "The exact Journal could not be opened. Saved evidence is unchanged."; Notify(); }
        }, c => c?.HasJournal == true && !IsBusy);
    }
    public ReviewPeriodKind Kind { get; }
    private DateOnly Today => DateOnly.FromDateTime(TradingTimePolicy.ConvertUtcToTradingTime(_clock.GetUtcNow()).DateTime);
    public PeriodReviewQuery? Query { get { try { return _date is { } d ? new(Kind, DateOnly.FromDateTime(d), _account.Id) : null; } catch (ArgumentOutOfRangeException) { return null; } } }
    public DateTime? SelectedDate { get => _date; set { if (_deleting || !SetProperty(ref _date, value)) return; _page = _scopePage = 1; LoadTask = LoadAsync(); } }
    public ReviewAccount SelectedAccount { get => _account; set { if (_deleting || value is null || !SetProperty(ref _account, value)) return; _page = 1; LoadTask = LoadAsync(); } }
    public IReadOnlyList<ReviewAccount> Accounts { get; private set; } = [new(null, "All accounts")];
    public string Heading => Query is { } q ? $"{q.FirstDate:dd MMM yyyy} – {q.LastDate:dd MMM yyyy} · New York" : "Choose a valid New York date";
    public string Scope => _account.Label + (_account.IsHistorical ? " · Current evidence may be unavailable; saved snapshots retain the original Account." : "");
    public string ScopeIdentity => _account.Id?.ToString() ?? "All accounts · aggregate review";
    public bool IsLoading => _loading || _detail is not null;
    public bool IsGenerating => _generating;
    public bool IsBusy => IsLoading || _generating || _deleting;
    public bool CanSelect => !_deleting;
    public bool NeedsConfiguration => _credentials.GetSource() is CoachingCredentialSource.None or CoachingCredentialSource.Unreadable;
    public string Provider => (_credentials as ICoachingConfiguration)?.SelectedProvider switch { CoachingProviderKind.Groq => "Groq", CoachingProviderKind.Unavailable => "the configured provider (unavailable)", _ => "OpenAI" };
    public string Disclosure => $"Generate sends period summaries and selected source examples to {Provider}. Usage may incur charges.";
    public string Privacy => "Selected and previous period aggregates cover all included Trades. At most two examples per source kind and period supply Trade facts, Journal observations, captured Playbook and Rule text; other individual details remain local. Examples are not representative sampling. No chart images are sent. Content may be sensitive. Groq Free tier has limits and upgraded accounts may be billed. Cancelling locally may not prevent charges or undo an already committed save. No automatic retry or provider fallback. Saved reviews retain the complete local evidence and exact supplied projection, unencrypted; citations do not prove an AI claim is true.";
    public string Availability => _account.IsHistorical ? "Generation is unavailable for this historical Account. Its saved reviews remain readable." : "";
    public PeriodCurrentPresentation? Current { get; private set; }
    public PeriodReport? Report { get; private set; }
    public bool ShowIntroduction => Report is null && string.IsNullOrEmpty(Message) && !IsBusy;
    public PeriodCitation? Citation { get; private set; }
    public IReadOnlyList<PeriodHistoryRow> History { get; private set; } = [];
    public string? Message { get; private set; }
    public string? CurrentError { get; private set; }
    public string? HistoryError { get; private set; }
    public string? ScopeError { get; private set; }
    public string HistoryHeading => $"Saved {(Kind == ReviewPeriodKind.Week ? "weekly" : "monthly")} reviews · {_historyPage?.TotalCount.ToString() ?? "—"} · page {_page}";
    public string HistoryScope => _account.Id is null ? "All accounts history contains aggregate reviews only. Select an Account for its exact-account history." : "Exact original Account history; no same-name substitution.";
    public string EmptyHistory => !_loading && HistoryError is null && _historyPage?.TotalCount == 0 ? "No saved reviews for this period and scope." : "";
    public string ScopePaging => (_scopes?.TotalCount ?? 0) > 25 ? $"Saved scope discovery · page {_scopePage}. Historical Accounts are included in the Account selector." : "";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public Action? OpenSettings { get; set; }
    public Func<Guid, Task>? OpenTradeAsync { get; set; }
    public Func<DailyReviewJournalEvidence, Task>? OpenJournalAsync { get; set; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand TodayCommand { get; }
    public IRelayCommand PreviousPeriodCommand { get; }
    public IRelayCommand NextPeriodCommand { get; }
    public IAsyncRelayCommand PreviousHistoryCommand { get; }
    public IAsyncRelayCommand NextHistoryCommand { get; }
    public IAsyncRelayCommand PreviousScopesCommand { get; }
    public IAsyncRelayCommand NextScopesCommand { get; }
    public IRelayCommand CancelLoadCommand { get; }
    public IAsyncRelayCommand GenerateCommand { get; }
    public IRelayCommand CancelGenerationCommand { get; }
    public IRelayCommand ConfigureCommand { get; }
    public IAsyncRelayCommand<PeriodHistoryRow> OpenCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public IAsyncRelayCommand<PeriodHistoryRow> DeleteCommand { get; }
    public IRelayCommand<PeriodCitation> OpenCitationCommand { get; }
    public IAsyncRelayCommand<PeriodCitation> OpenTradeCommand { get; }
    public IAsyncRelayCommand<PeriodCitation> OpenJournalCommand { get; }

    public Task ActivateAsync() { _active = true; return LoadTask = LoadAsync(); }
    public bool TryLeave()
    {
        if (_deleting) { Message = "Wait for deletion to finish before leaving."; Notify(); return false; }
        return !_generating || _dialogs.Confirm(new("Leave while generating?", "Leaving cancels this local request. An accepted provider request may still incur charges; a completed save remains in history.", "Cancel generation and leave"));
    }
    public void Deactivate() { _active = false; CancelGeneration(); CancelLoad(); }
    public void OnDataCommitted() { if (_active && !_generating && !_deleting) LoadTask = LoadAsync(); }
    private void MovePeriod(int direction) { if (Query is { } q) try { SelectedDate = (Kind == ReviewPeriodKind.Week ? q.FirstDate.AddDays(direction * 7) : q.FirstDate.AddMonths(direction)).ToDateTime(TimeOnly.MinValue); } catch (ArgumentOutOfRangeException) { Message = "That period is outside the supported calendar."; Notify(); } }
    private Task Page(int direction) { _page += direction; return LoadTask = LoadAsync(); }
    private Task ScopePage(int direction) { _scopePage += direction; return LoadTask = LoadAsync(); }
    private void ClearDetail() { _detailVersion++; _detail?.Cancel(); _detail = null; Report = null; Citation = null; }
    private void CancelLoad() { _version++; _load?.Cancel(); _load = null; _loading = false; ClearDetail(); Notify(); }
    private void CancelGeneration() { _requestVersion++; _request?.Cancel(); if (_generating) Message = "Generation cancelled locally. No automatic retry; check history for a save already committed."; Notify(); }
    private async Task LoadAsync(bool generated = false)
    {
        if (!generated) CancelGeneration();
        CancelLoad(); Current = null; History = []; _historyPage = null; CurrentError = HistoryError = ScopeError = null;
        if (!_active || Query is not { } q) { Notify(); return; }
        long version = _version; using var cts = new CancellationTokenSource(); _load = cts; _loading = true; Notify();
        bool Valid() => _active && version == _version && !cts.IsCancellationRequested;
        async Task Evidence()
        {
            try
            {
                var result = await Task.Run(async () =>
                {
                    var read = await _reader.ReadComparisonAsync(q, cts.Token);
                    if (read.Evidence is not { } e || e.Current.Query != q) return null;
                    return PeriodReviewComparisonCalculator.Calculate(e, cts.Token).Comparison is { } c ? new PeriodCurrentPresentation(c) : null;
                }, cts.Token);
                if (Valid()) { Current = result; CurrentError = result is null ? "Complete evidence could not fit or match this selection. No partial calculation is shown." : null; }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
            catch (Exception) { if (Valid()) CurrentError = "Current evidence could not be loaded. Refresh to retry; saved history remains independent."; }
        }
        async Task HistoryRead()
        {
            try
            {
                var page = await Task.Run(() => _history.BrowseAsync(new(q, _page, 10), cts.Token), cts.Token);
                if (page.Items.Count == 0 && page.Page > 1) page = await _history.BrowseAsync(new(q, Math.Max(1, (page.TotalCount + 9) / 10), 10), cts.Token);
                if (Valid()) { _historyPage = page; _page = page.Page; History = page.Items.Select(s => new PeriodHistoryRow(s)).ToArray(); }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
            catch (Exception) { if (Valid()) HistoryError = "Saved history could not be loaded. Refresh to retry."; }
        }
        async Task Scopes()
        {
            IReadOnlyList<ReviewAccount> live = []; SavedPeriodScopePage? saved = null;
            try { live = (await Task.Run(() => _accounts.GetAllAsync(cts.Token), cts.Token)).OrderBy(a => a.Name).ThenBy(a => a.Id).Select(a => new ReviewAccount(a.Id, a.Name + (a.IsActive ? "" : " (inactive)"))).ToArray(); }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) { return; }
            catch (Exception) { if (Valid()) ScopeError = "Current Accounts could not be loaded. Refresh before generating."; }
            try
            {
                saved = await Task.Run(() => _history.BrowseScopesAsync(new(new(q.Kind, q.FirstDate), _scopePage, 25), cts.Token), cts.Token);
                if (saved.Items.Count == 0 && saved.Page > 1)
                    saved = await _history.BrowseScopesAsync(new(new(q.Kind, q.FirstDate), Math.Max(1, (saved.TotalCount + 24) / 25), 25), cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) { return; }
            catch (Exception) { if (Valid()) ScopeError = "Historical scope discovery failed. Selected scope is unchanged; Refresh to retry."; }
            if (!Valid()) return;
            _scopes = saved; if (saved is not null) _scopePage = saved.Page;
            var options = new List<ReviewAccount> { new(null, "All accounts") }; options.AddRange(live);
            options.AddRange((saved?.Items ?? []).Where(s => s.Scope.AccountId.HasValue && options.All(a => a.Id != s.Scope.AccountId))
                .Select(s => new ReviewAccount(s.Scope.AccountId, (s.AccountDisplayName ?? "Name unavailable") + " (historical / unavailable)", true)));
            if (options.All(a => a.Id != _account.Id)) options.Add(_account with { IsHistorical = true });
            _account = options.First(a => a.Id == _account.Id); Accounts = options;
        }
        try { await Task.WhenAll(Evidence(), HistoryRead(), Scopes()); }
        finally { if (version == _version) { _load = null; _loading = false; Notify(); } }
    }
    private async Task GenerateAsync()
    {
        if (!_active || IsBusy || _account.IsHistorical || NeedsConfiguration || Query is not { } q) return;
        long request = ++_requestVersion; using var cts = new CancellationTokenSource(); _request = cts; _generating = true;
        Message = "Reading fresh evidence, generating, validating and saving…"; Notify();
        bool Valid() => _active && request == _requestVersion && !cts.IsCancellationRequested && Query == q;
        try
        {
            if (q.Scope.AccountId is { } id && !(await _accounts.GetAllAsync(cts.Token)).Any(a => a.Id == id))
            { if (Valid()) Message = "This Account is unavailable. Refresh to discover saved history; nothing was sent."; return; }
            var result = await Task.Run(() => _generate(q, cts.Token), cts.Token);
            if (!Valid()) return;
            Message = result.Message;
            if (result.Status != SavedCoachingGenerationStatus.Saved || result.Review is not { } saved) return;
            if (!PeriodReport.Matches(saved.Summary, q)) { Message = "Saved result did not match this selection. Refresh history; no substitute was displayed."; return; }
            _page = 1; await (LoadTask = LoadAsync(generated: true));
            if (!Valid()) return;
            try { Report = PeriodReport.Read(saved); Message = "Review saved and opened."; }
            catch (Exception) { Message = "Review saved, but its snapshot could not be displayed. Reopen history; do not regenerate automatically."; }
        }
        catch (OperationCanceledException) { if (Valid()) Message = "Generation cancelled. No automatic retry."; }
        catch (Exception) { if (Valid()) Message = "Generation could not finish. Refresh history before another explicit attempt; no automatic retry was made."; }
        finally { _request = null; _generating = false; Notify(); }
    }
    private async Task OpenAsync(PeriodHistoryRow? row)
    {
        if (row is null || !_active || !History.Contains(row)) return;
        ClearDetail(); long version = _version, detail = _detailVersion; using var cts = new CancellationTokenSource(); _detail = cts;
        Message = "Loading saved report…"; Notify();
        try
        {
            var saved = await Task.Run(() => _history.GetAsync(row.Source.Id, cts.Token), cts.Token);
            var report = saved is null ? null : await Task.Run(() => PeriodReport.Read(saved), cts.Token);
            if (!_active || version != _version || detail != _detailVersion || cts.IsCancellationRequested) return;
            if (saved is not null && (Query is not { } q || !PeriodReport.Matches(saved.Summary, q))) throw new InvalidOperationException();
            Report = report; Message = saved is null ? "This saved review is no longer available. Refresh history." : "Saved snapshot opened; current sources were not substituted.";
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (Exception) { if (_active && version == _version && detail == _detailVersion) Message = "Saved review is unavailable, damaged or unsupported. Current sources were not substituted."; }
        finally { if (detail == _detailVersion) { _detail = null; Notify(); } }
    }
    private async Task DeleteAsync(PeriodHistoryRow? row)
    {
        if (row is null || !_active || IsBusy || !History.Contains(row) || !_dialogs.Confirm(new("Delete saved period review?",
            $"{row.Scope}\n{row.Heading}\nPermanently delete only this review and its evidence snapshot? Trades, Journals, Rules and other reviews are unchanged.", "Delete review", isDestructive: true))) return;
        _deleting = true; Notify();
        try { var result = await Task.Run(() => _history.DeleteAsync(row.Source.Id, true)); await (LoadTask = LoadAsync()); Message = result == PeriodReviewDeleteStatus.Deleted ? "Saved review deleted. Source data is unchanged." : "Review was already removed. History refreshed."; }
        catch (Exception) { Message = "Deletion was not confirmed. Refresh history before trying again."; }
        finally { _deleting = false; Notify(); }
    }
    private void Notify()
    {
        OnPropertyChanged(string.Empty);
        foreach (var c in new IRelayCommand?[] { RefreshCommand, TodayCommand, PreviousPeriodCommand, NextPeriodCommand, PreviousHistoryCommand, NextHistoryCommand,
            PreviousScopesCommand, NextScopesCommand, CancelLoadCommand, GenerateCommand, CancelGenerationCommand, DeleteCommand, OpenCommand, OpenTradeCommand, OpenJournalCommand }) c?.NotifyCanExecuteChanged();
    }
}
