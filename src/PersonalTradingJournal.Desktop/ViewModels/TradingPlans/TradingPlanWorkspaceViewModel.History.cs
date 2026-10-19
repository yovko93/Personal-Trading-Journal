using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed record PlanHistoryRow(TradingPlanLoadedState State)
{
    public string Date => State.Key.NewYorkDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    public string Account => State.Summary is { } s ? $"{s.AccountName} · {s.Currency}" + (s.AccountAvailable ? "" : " · unavailable") : "Saved Account";
    public string Session => State.Summary?.Session is { } s ? s == TradingPlanSession.NewYork ? "New York" : s.ToString() : "Session not set";
    public string Instruments => State.Summary is { Instruments.Length: > 0 } s ? string.Join(", ", s.Instruments) : "No Instruments";
    public string Lifecycle => State.Status == TradingPlanStatus.Ready ? "Ready" : State.LatestReadyRevision > 0 ? "Draft after Ready" : "Draft";
    public string Ready => State.LatestReadyRevision > 0 ? $"Latest Ready {State.LatestReadyRevision}" : "No Ready revisions";
    public string Saved => State.Summary?.SavedAtUtc.ToString("dd MMM yyyy HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "Time unavailable";
}
public sealed record PlanRevisionRow(TradingPlanReadyRevision Revision)
{
    public string Label => $"Ready {Revision.Id.Revision} · {Revision.ReadyAtUtc:dd MMM yyyy HH:mm:ss} UTC";
}
public sealed record PlanHistoryStatusOption(TradingPlanStatus? Status, string Label);

public sealed partial class TradingPlanWorkspaceViewModel
{
    private const int HistoryPageSize = 20;
    private bool _isHistory, _historyLoading, _historyTransition, _transferAttempted;
    private string _historyFrom = "", _historyTo = "", _historyError = "", _historyMessage = "Choose an Account and search saved preparation.", _copyDate = "", _copyError = "";
    private PlanAccountOption? _historyAccount;
    private TradingPlanStatus? _historyStatus;
    private TradingPlanHistoryQuery? _appliedHistory;
    private readonly List<DateOnly?> _historyCursors = [null];
    private readonly List<int?> _revisionCursors = [null];
    private int _historyPage, _revisionPage;
    private long _historyEpoch;
    private CancellationTokenSource? _historyLoad;
    private Task? _historyWriteTask;
    private TradingPlan? _historyPlan;
    private TradingPlanReadyRevision? _viewedRevision;
    private TradingPlanKey? _recoveryTarget;
    public bool IsHistorySelected => _isHistory;
    public bool IsBriefSelected => !_isHistory;
    public bool CanUseHistory => !_writing && !_historyTransition && !_guarding && Editor?.IsBusy != true && !_disposed;
    public bool IsHistoryLoading => _historyLoading;
    public string HistoryFrom { get => _historyFrom; set { if (CanUseHistory && SetProperty(ref _historyFrom, value)) NotifyHistory(); } }
    public string HistoryTo { get => _historyTo; set { if (CanUseHistory && SetProperty(ref _historyTo, value)) NotifyHistory(); } }
    public PlanAccountOption? HistoryAccount { get => _historyAccount; set { if (CanUseHistory && SetProperty(ref _historyAccount, value)) NotifyHistory(); } }
    public TradingPlanStatus? HistoryStatus { get => _historyStatus; set { if (CanUseHistory && SetProperty(ref _historyStatus, value)) NotifyHistory(); } }
    public IReadOnlyList<PlanHistoryStatusOption> HistoryStatuses { get; } = [new(null, "Draft and Ready"), new(TradingPlanStatus.Draft, "Draft (including after Ready)"), new(TradingPlanStatus.Ready, "Ready")];
    public PlanHistoryStatusOption SelectedHistoryStatus { get => HistoryStatuses.Single(s => s.Status == HistoryStatus); set { if (value is not null) HistoryStatus = value.Status; } }
    public string HistoryError => _historyError;
    public string HistoryMessage { get => _historyMessage; private set { if (SetProperty(ref _historyMessage, value)) { OnPropertyChanged(nameof(TransferNotice)); OnPropertyChanged(nameof(HistoryNotice)); } } }
    public PlanNotice HistoryNotice => new(HistoryMessage, _historyNoticeKind);
    private PlanNoticeKind _historyNoticeKind;
    private void ReportHistory(string text, PlanNoticeKind kind = PlanNoticeKind.Info)
    { _historyNoticeKind = kind; HistoryMessage = text; OnPropertyChanged(nameof(HistoryNotice)); }
    public string TransferNotice => _transferAttempted ? HistoryMessage : "";
    public bool HasUnappliedHistory => _appliedHistory is not null && (!TryHistoryQuery(out var q) || q!.AccountId != _appliedHistory.AccountId ||
        q.FirstDate != _appliedHistory.FirstDate || q.LastDate != _appliedHistory.LastDate || q.Status != _appliedHistory.Status);
    public string AppliedHistoryLabel => _appliedHistory is { } q ? $"Displayed: {Format(q.FirstDate)}–{Format(q.LastDate)} · New York · {_appliedAccountLabel} · {q.Status?.ToString() ?? "Draft and Ready"}" : "No search applied.";
    private string _appliedAccountLabel = "";
    private readonly List<PlanAccountOption> _discoveredAccounts = [];
    private int _discoveryOffset;
    private (DateOnly First, DateOnly Last)? _discoveryRange;
    public IReadOnlyList<PlanAccountOption> HistoryAccounts => Accounts.Concat(_discoveredAccounts.Where(h => !Accounts.Any(a => a.Id == h.Id))).ToArray();
    public bool HasMoreHistoryAccounts { get; private set; }
    public IAsyncRelayCommand DiscoverAccountsCommand { get; private set; } = null!;
    public IAsyncRelayCommand MoreAccountsCommand { get; private set; } = null!;
    public IReadOnlyList<PlanHistoryRow> HistoryRows { get; private set; } = [];
    public bool HasOlderPlans { get; private set; }
    public bool HasNewerPlans => _historyPage > 0;
    public string HistoryPageLabel => $"Page {_historyPage + 1} · {HistoryRows.Count} Plans";
    public PlanHistoryRow? SelectedHistoryRow { get; private set; }
    public IReadOnlyList<PlanRevisionRow> RevisionRows { get; private set; } = [];
    public bool HasOlderRevisions { get; private set; }
    public bool HasNewerRevisions => _revisionPage > 0;
    public bool HasSelectedHistory => SelectedHistoryRow is not null;
    public bool HasHistoricalViewer => HistoricalViewer is not null;
    public SessionPreparationViewModel? HistoricalViewer { get; private set; }
    public string HistoricalLabel => _viewedRevision is { } r ? $"Ready revision {r.Id.Revision} · read-only · {Format(r.Key.NewYorkDate)} New York · {r.References.Account.Name} · {r.References.Account.Currency} · {r.Content.Session?.ToString().Replace("NewYork", "New York")} session · {r.ReadyAtUtc:dd MMM yyyy HH:mm:ss} UTC" : "";
    public string CopyDate { get => _copyDate; set { if (CanUseHistory && SetProperty(ref _copyDate, value)) { _copyError = ""; NotifyHistory(); } } }
    public string CopyError => _copyError;
    public bool HasRecoveryTarget => _recoveryTarget is not null;
    public IRelayCommand ShowBriefCommand { get; private set; } = null!;
    public IRelayCommand ShowHistoryCommand { get; private set; } = null!;
    public IAsyncRelayCommand SearchHistoryCommand { get; private set; } = null!;
    public IAsyncRelayCommand OlderPlansCommand { get; private set; } = null!;
    public IAsyncRelayCommand NewerPlansCommand { get; private set; } = null!;
    public IAsyncRelayCommand<PlanHistoryRow> ShowRevisionsCommand { get; private set; } = null!;
    public IAsyncRelayCommand<PlanRevisionRow> ViewRevisionCommand { get; private set; } = null!;
    public IAsyncRelayCommand OlderRevisionsCommand { get; private set; } = null!;
    public IAsyncRelayCommand NewerRevisionsCommand { get; private set; } = null!;
    public IAsyncRelayCommand<PlanHistoryRow> OpenPlanCommand { get; private set; } = null!;
    public IAsyncRelayCommand DeriveHistoricalCommand { get; private set; } = null!;
    public IAsyncRelayCommand CopyForwardCommand { get; private set; } = null!;
    public IAsyncRelayCommand OpenRecoveryCommand { get; private set; } = null!;

    private void InitializeHistory()
    {
        _historyFrom = Format(new(Today.Year, Today.Month, 1)); _historyTo = Format(Today);
        ShowBriefCommand = new RelayCommand(() => SelectTab(false));
        ShowHistoryCommand = new RelayCommand(() => SelectTab(true));
        DiscoverAccountsCommand = new AsyncRelayCommand(() => DiscoverAccountsAsync(false), () => CanUseHistory);
        MoreAccountsCommand = new AsyncRelayCommand(() => DiscoverAccountsAsync(true), () => CanUseHistory && !_historyLoading && HasMoreHistoryAccounts);
        SearchHistoryCommand = new AsyncRelayCommand(SearchHistoryAsync, () => CanUseHistory);
        OlderPlansCommand = new AsyncRelayCommand(() => PagePlansAsync(1), () => CanUseHistory && !_historyLoading && HasOlderPlans && !HasUnappliedHistory);
        NewerPlansCommand = new AsyncRelayCommand(() => PagePlansAsync(-1), () => CanUseHistory && !_historyLoading && HasNewerPlans && !HasUnappliedHistory);
        ShowRevisionsCommand = new AsyncRelayCommand<PlanHistoryRow>(row => row is null ? Task.CompletedTask : ShowRevisionsAsync(row), row => CanUseHistory && row is not null);
        ViewRevisionCommand = new AsyncRelayCommand<PlanRevisionRow>(row => row is null ? Task.CompletedTask : ViewRevisionAsync(row), row => CanUseHistory && row is not null);
        OlderRevisionsCommand = new AsyncRelayCommand(() => PageRevisionsAsync(1), () => CanUseHistory && !_historyLoading && HasOlderRevisions);
        NewerRevisionsCommand = new AsyncRelayCommand(() => PageRevisionsAsync(-1), () => CanUseHistory && !_historyLoading && HasNewerRevisions);
        OpenPlanCommand = new AsyncRelayCommand<PlanHistoryRow>(row => row is null ? Task.CompletedTask : OpenHistoryPlanAsync(row.State.Key), row => CanUseHistory && row is not null);
        DeriveHistoricalCommand = new AsyncRelayCommand(() => TransferAsync(false), () => CanUseHistory && !_historyLoading && HasHistoricalViewer);
        CopyForwardCommand = new AsyncRelayCommand(() => TransferAsync(true), () => CanUseHistory && !_historyLoading && HasHistoricalViewer);
        OpenRecoveryCommand = new AsyncRelayCommand(() => _recoveryTarget is { } key ? OpenHistoryPlanAsync(key) : Task.CompletedTask, () => CanUseHistory && HasRecoveryTarget);
    }
    public void SelectTab(bool history)
    {
        _isHistory = history;
        // Tab switches retain the same editor, including malformed raw fields and its optimistic token.
        if (_historyAccount is null) _historyAccount = _account;
        OnPropertyChanged(nameof(IsHistorySelected)); OnPropertyChanged(nameof(IsBriefSelected)); NotifyHistory();
    }
    private static bool ParseHistoryDate(string text, out DateOnly date) => DateOnly.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date) ||
        DateOnly.TryParseExact(text, ["dd MMM yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
    private bool TryHistoryQuery(out TradingPlanHistoryQuery? query)
    {
        query = null;
        if (_historyAccount is null || !ParseHistoryDate(_historyFrom, out var first) || !ParseHistoryDate(_historyTo, out var last) || first > last) return false;
        query = new(_historyAccount.Id, first, last, _historyStatus, HistoryPageSize + 1); return true;
    }
    public Task DiscoverAccountsAsync(bool more = false)
    {
        if (!CanUseHistory || !ParseHistoryDate(HistoryFrom, out var first) || !ParseHistoryDate(HistoryTo, out var last) || first > last)
        { _historyError = "Enter valid inclusive From/To dates before discovering saved Accounts."; NotifyHistory(); return Task.CompletedTask; }
        if (more && (_discoveryRange != (first, last) || !HasMoreHistoryAccounts))
        { ReportHistory("Dates changed. Select Find saved Accounts to start a fresh discovery.", PlanNoticeKind.Warning); return Task.CompletedTask; }
        _historyError = "";
        var offset = more ? _discoveryOffset + HistoryPageSize : 0;
        return HistoryReadAsync(async (ct, owns) =>
        {
            var rows = await Task.Run(() => _store.HistoryAccountsAsync(first, last, offset, HistoryPageSize + 1, ct), ct);
            if (!owns()) return;
            _discoveryRange = (first, last); _discoveryOffset = offset; HasMoreHistoryAccounts = rows.Count > HistoryPageSize;
            // Retain discovered identities already selected; never substitute a same-name current Account.
            foreach (var a in rows.Take(HistoryPageSize))
            { _discoveredAccounts.RemoveAll(x => x.Id == a.AccountId); _discoveredAccounts.Add(new(a.AccountId, a.Name, a.Currency, a.IsActive, a.IsAvailable)); }
            _historyError = ""; ReportHistory("Saved Account identities added to the History selector. Choose one, then Search. Unavailable Accounts remain read-only.", PlanNoticeKind.Info);
        });
    }
    public Task SearchHistoryAsync()
    {
        if (!CanUseHistory) return Task.CompletedTask;
        if (!TryHistoryQuery(out var query))
        { _historyError = "Choose an exact Account and valid inclusive New York From/To dates (From must not follow To). Displayed results are unchanged."; NotifyHistory(); return Task.CompletedTask; }
        _historyError = "";
        var accountLabel = _historyAccount!.Label;
        return HistoryReadAsync(async (ct, owns) =>
        {
            var rows = await Task.Run(() => _store.HistoryAsync(query!, cancellationToken: ct), ct);
            if (!owns()) return;
            _appliedHistory = query; _appliedAccountLabel = accountLabel;
            _historyCursors.Clear(); _historyCursors.Add(null); _historyPage = 0;
            PublishPlans(rows); HistoryMessage = rows.Count == 0 ? "No saved Plans match these filters." : "Saved Plans loaded. Select Revisions to inspect immutable Ready content.";
        });
    }
    private Task PagePlansAsync(int direction)
    {
        if (!CanUseHistory || _historyLoading || HasUnappliedHistory || _appliedHistory is null || (direction > 0 ? !HasOlderPlans : !HasNewerPlans)) return Task.CompletedTask;
        int page = _historyPage + direction;
        DateOnly? cursor = direction > 0 ? HistoryRows[^1].State.Key.NewYorkDate : _historyCursors[page];
        var query = _appliedHistory;
        return HistoryReadAsync(async (ct, owns) =>
        {
            var rows = await Task.Run(() => _store.HistoryAsync(query, cursor, ct), ct);
            if (!owns()) return;
            if (page == _historyCursors.Count) _historyCursors.Add(cursor);
            _historyPage = page; PublishPlans(rows); ReportHistory("Saved Plan page loaded.", PlanNoticeKind.Info);
        });
    }
    private void PublishPlans(IReadOnlyList<TradingPlanLoadedState> rows)
    {
        HistoryRows = rows.Take(HistoryPageSize).Select(x => new PlanHistoryRow(x)).ToArray(); HasOlderPlans = rows.Count > HistoryPageSize;
        ClearHistorical(); SelectedHistoryRow = null; _historyPlan = null; RevisionRows = []; HasOlderRevisions = false; _revisionPage = 0;
    }
    public Task ShowRevisionsAsync(PlanHistoryRow row)
    {
        if (!CanUseHistory || !HistoryRows.Contains(row)) return Task.CompletedTask;
        SelectedHistoryRow = row; _historyPlan = null; ClearHistorical(); RevisionRows = []; _revisionPage = 0; HasOlderRevisions = false;
        _revisionCursors.Clear(); _revisionCursors.Add(null);
        return ReadRevisionsAsync(row, 0, null);
    }
    private Task ReadRevisionsAsync(PlanHistoryRow row, int page, int? cursor) => HistoryReadAsync(async (ct, owns) =>
    {
        var plan = await Task.Run(() => _store.LoadAsync(row.State.Key, ct), ct);
        if (plan is null || plan.Id != row.State.Token.PlanId) throw new InvalidDataException("Unavailable Plan.");
        var revisions = await Task.Run(() => _store.ReadyHistoryAsync(plan.Id, cursor, HistoryPageSize + 1, ct), ct);
        if (!owns() || SelectedHistoryRow != row) return;
        _historyPlan = plan; ClearHistorical(); _revisionPage = page;
        if (_revisionCursors.Count == page) _revisionCursors.Add(cursor);
        RevisionRows = revisions.Take(HistoryPageSize).Select(r => new PlanRevisionRow(r)).ToArray(); HasOlderRevisions = revisions.Count > HistoryPageSize;
        HistoryMessage = revisions.Count == 0 ? "This Plan has no Ready revisions. Open its saved Draft to continue." : "Choose an exact Ready revision. The Session Brief editor is unchanged.";
    });
    private Task PageRevisionsAsync(int direction)
    {
        if (!CanUseHistory || _historyLoading || SelectedHistoryRow is null || (direction > 0 ? !HasOlderRevisions : !HasNewerRevisions)) return Task.CompletedTask;
        int page = _revisionPage + direction;
        return ReadRevisionsAsync(SelectedHistoryRow, page, direction > 0 ? RevisionRows[^1].Revision.Id.Revision : _revisionCursors[page]);
    }
    public Task ViewRevisionAsync(PlanRevisionRow row)
    {
        if (!CanUseHistory || !RevisionRows.Contains(row) || _historyPlan is null) return Task.CompletedTask;
        var plan = _historyPlan; ClearHistorical();
        return HistoryReadAsync(async (ct, owns) =>
        {
            var revision = await Task.Run(() => _store.LoadReadyAsync(row.Revision.Id, ct), ct);
            if (revision is null || revision.Id != row.Revision.Id || revision.Key != plan.Key) throw new InvalidDataException("Unavailable revision.");
            if (!owns() || _historyPlan != plan) return;
            _viewedRevision = revision; HistoricalViewer = CreateEditor(plan, revision);
            await HistoricalViewer.LoadThumbnailsAsync(ct);
            if (!owns() || _historyPlan != plan) return;
            ReportHistory("Showing frozen Ready preparation. Start Draft uses this revision on its original date; Copy Forward uses it on another date. Neither uses unsaved editor input.", PlanNoticeKind.Info);
        });
    }
    private void ClearHistorical()
    { HistoricalViewer?.Dispose(); HistoricalViewer = null; _viewedRevision = null; _recoveryTarget = null; _transferAttempted = false; }
    private Task HistoryReadAsync(Func<CancellationToken, Func<bool>, Task> read)
    {
        var task = HistoryReadCoreAsync(read); lock (_loadSync) _loads.Add(task); return TrackAsync(task);
    }
    private async Task HistoryReadCoreAsync(Func<CancellationToken, Func<bool>, Task> read)
    {
        _historyLoad?.Cancel(); using var cancellation = new CancellationTokenSource(); _historyLoad = cancellation;
        long epoch = ++_historyEpoch; _historyLoading = true; ReportHistory("Loading saved preparation…", PlanNoticeKind.Info); Notify();
        bool Owns() => !_disposed && epoch == _historyEpoch && !cancellation.IsCancellationRequested;
        try { await read(cancellation.Token, Owns); cancellation.Token.ThrowIfCancellationRequested(); }
        catch (OperationCanceledException) { if (epoch == _historyEpoch && !_disposed) ReportHistory("History loading cancelled. Existing editor input is retained; select Search or Revisions to retry.", PlanNoticeKind.Info); }
        catch (Exception) { if (epoch == _historyEpoch && !_disposed) ReportHistory("Saved preparation could not be loaded. Retry the action; no Plan was changed.", PlanNoticeKind.Error); }
        finally { if (epoch == _historyEpoch) { _historyLoading = false; _historyLoad = null; Notify(); } }
    }
    public async Task OpenHistoryPlanAsync(TradingPlanKey key)
    {
        if (!CanUseHistory) return;
        if (key.AccountId == _account?.Id && key.NewYorkDate == _date)
        {
            // Explicit history/recovery opening must re-read even an already selected identity:
            // another writer may have created its Draft while our editor still shows Ready.
            if (!await PrepareLeaveAsync()) { ReportHistory(Message, Notice.Kind); return; }
            CompleteLeave(); ReplaceEditor(null); _dateText = Format(_date); ValidateDate();
            _contextLoaded = false; RestoreSelectors(); await LoadAsync(refreshAccounts: false);
            if (Editor?.Key == key) SelectTab(false); else ReportHistory(Message, Notice.Kind);
            return;
        }
        var option = Accounts.FirstOrDefault(a => a.Id == key.AccountId) ?? new(key.AccountId,
            _viewedRevision?.References.Account.Name ?? "Unavailable Account", _viewedRevision?.References.Account.Currency ?? "Unknown currency", false, false);
        var rawDate = _dateText;
        if (await SelectContextAsync(key.NewYorkDate, option))
        { if (Editor?.Key == key) SelectTab(false); else ReportHistory(Message, Notice.Kind); }
        else
        {
            // Toolbar selection normally restores its displayed date on a veto. History is
            // independent: do not normalize the retained Session Brief's malformed raw input.
            _dateText = rawDate; ValidateDate(); RestoreSelectors();
            ReportHistory(Message, Notice.Kind);
        }
    }
    public async Task TransferAsync(bool copy)
    {
        if (!CanUseHistory || _historyLoading || _viewedRevision is not { } source || _historyPlan is not { } owner) return;
        DateOnly targetDate = source.Key.NewYorkDate;
        if (copy && (!ParseHistoryDate(CopyDate, out targetDate) || targetDate == source.Key.NewYorkDate))
        { _copyError = "Enter a valid, different New York target date. Copy Forward stays within the source Account."; NotifyHistory(); return; }
        _copyError = "";
        _transferAttempted = true; ReportHistory("Resolving pending editor input before using the selected Ready revision…", PlanNoticeKind.Info);
        _historyTransition = true; Notify();
        try
        {
            if (!await PrepareLeaveAsync()) { ReportHistory(Message, Notice.Kind); return; }
            // A guard may pump the dispatcher. Do not attach a different selection afterward.
            if (_viewedRevision != source || _historyPlan != owner || _disposed) return;
            _writing = true; Notify();
            _historyWriteTask = TransferCoreAsync(copy, source, owner, new(source.Key.AccountId, targetDate));
            await _historyWriteTask;
        }
        finally { _historyWriteTask = null; _writing = false; _historyTransition = false; ClearLeaveConsent(); Notify(); }
    }
    private async Task TransferCoreAsync(bool copy, TradingPlanReadyRevision source, TradingPlan owner, TradingPlanKey target)
    {
        using var cancellation = new CancellationTokenSource(); _write = cancellation;
        HistoryMessage = copy ? "Copying the selected persisted Ready revision…" : "Starting Draft from the selected Ready revision…";
        try
        {
            var current = target.AccountId is { } accountId ? await Task.Run(() => _accounts.GetByIdAsync(accountId, cancellation.Token), cancellation.Token) : null;
            string? currency = null;
            var result = await Task.Run(() => _store.WriteAsync(new(copy ? TradingPlanWriteOperation.CopyForward : TradingPlanWriteOperation.StartDraft,
                target, source.Content, copy ? null : new(owner.Id, owner.Version), source.Id, expectedAccountCurrency: currency), cancellation.Token), cancellation.Token);
            if (result.Status != TradingPlanWriteStatus.Success)
            {
                _recoveryTarget = result.Status is TradingPlanWriteStatus.TargetAlreadyExists or TradingPlanWriteStatus.Conflict ? target : null;
                ReportHistory(result.Status switch
                {
                    TradingPlanWriteStatus.TargetAlreadyExists => "A Plan already exists for that Account and date. Nothing was overwritten. Open existing Plan or choose another date.",
                    TradingPlanWriteStatus.Conflict => "The Plan changed or already has a working Draft. Reload Revisions, or open the existing Plan; nothing was overwritten.",
                    TradingPlanWriteStatus.ReferenceChanged => "A source or Account currency changed. Review current sources and explicitly refresh references in the existing Draft. Input is retained.",
                    TradingPlanWriteStatus.ReferenceUnavailable => "A required Account, Instrument, Playbook or Rule is inactive or unavailable. Reopen or review it before retrying; the historical revision remains readable.",
                    TradingPlanWriteStatus.Cancelled => "Operation cancelled. No success was reported; editor input is retained.",
                    _ => "The Draft could not be saved and verified. Input is retained. Check the target before retrying."
                }, PlanNotice.ForWrite(result.Status)); return;
            }
            var plan = result.Plan;
            var expected = copy ? source.Content.CopyForward() : new TradingPlanContent(source.Content.Session, source.Content.Focus, source.Content.Intent,
                source.Content.Instruments, source.Content.Scenarios, source.Content.NewsAndRestrictions, source.Content.StandAside, source.Content.RiskLimits, source.Content.Rules, planCurrency: source.References.Account.Currency, screenshots: source.Content.Screenshots);
            if (plan?.WorkingDraft is null || plan.Key != target || !SessionPreparationViewModel.SameContent(plan.WorkingDraft.Content, expected) ||
                (copy ? plan.Id == owner.Id || plan.LatestReadyRevision is not null : plan.Id != owner.Id || plan.Version <= owner.Version ||
                    plan.LatestReadyRevision?.Id != owner.LatestReadyRevision?.Id || plan.WorkingDraft.DerivedFromRevision != source.Id))
            { ReportHistory("Commit readback could not be verified. Input retained; open the target to check its stored state before retrying.", PlanNoticeKind.Error); _recoveryTarget = target; return; }
            // Committed success wins over late cancellation. Replace only with the verified returned aggregate.
            var candidate = CreateEditor(plan);
            _date = target.NewYorkDate; _dateText = Format(_date); ValidateDate();
            _account = Accounts.FirstOrDefault(a => a.Id == target.AccountId) ?? new(target.AccountId, source.References.Account.Name, source.References.Account.Currency, false, false);
            ReplaceEditor(candidate); _contextLoaded = true; ClearLeaveConsent(); RestoreSelectors(); SelectTab(false);
            string success = copy ? "Copied Draft saved and verified. Daily context was reset; source Ready revision preserved." : "Draft saved and verified from the selected revision. Ready history is preserved.";
            Report(success, PlanNoticeKind.Success); ReportHistory(success, PlanNoticeKind.Success);
            try
            {
                await LoadSourcesAsync(candidate, CancellationToken.None);
                if (_appliedHistory is not null)
                {
                    var rows = await Task.Run(() => _store.HistoryAsync(_appliedHistory, cancellationToken: CancellationToken.None));
                    _historyPage = 0; _historyCursors.Clear(); _historyCursors.Add(null); PublishPlans(rows);
                }
                // Reference/catalog loading also publishes editor messages. Do not let those
                // replace the verified transfer result that the user needs to see.
                Report(success, PlanNoticeKind.Success); ReportHistory(success, PlanNoticeKind.Success);
            }
            catch (Exception) { Report("Draft committed and read back. Supporting lists could not refresh; reload before further editing. No automatic retry was made.", PlanNoticeKind.Error); ReportHistory(Message, Notice.Kind); }
        }
        catch (OperationCanceledException) { ReportHistory("Operation cancelled. Input and the source revision are retained; no success reported.", PlanNoticeKind.Info); }
        catch (Exception) { ReportHistory("Operation could not be verified. Input is retained; check the target before retrying.", PlanNoticeKind.Error); _recoveryTarget = target; }
        finally { _write = null; }
    }
    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(HistoryAccounts)); OnPropertyChanged(nameof(HasMoreHistoryAccounts)); OnPropertyChanged(nameof(SelectedHistoryStatus)); OnPropertyChanged(nameof(TransferNotice));
        DiscoverAccountsCommand?.NotifyCanExecuteChanged(); MoreAccountsCommand?.NotifyCanExecuteChanged();
        foreach (var name in new[] { nameof(CanUseHistory), nameof(IsHistoryLoading), nameof(HistoryAccount), nameof(HistoryRows), nameof(HistoryError), nameof(HasUnappliedHistory), nameof(AppliedHistoryLabel), nameof(HasOlderPlans), nameof(HasNewerPlans), nameof(HistoryPageLabel), nameof(SelectedHistoryRow), nameof(HasSelectedHistory), nameof(RevisionRows), nameof(HasOlderRevisions), nameof(HasNewerRevisions), nameof(HistoricalViewer), nameof(HasHistoricalViewer), nameof(HistoricalLabel), nameof(CopyError), nameof(HasRecoveryTarget) }) OnPropertyChanged(name);
        foreach (var c in new IRelayCommand?[] { SearchHistoryCommand, OlderPlansCommand, NewerPlansCommand, ShowRevisionsCommand, ViewRevisionCommand, OlderRevisionsCommand, NewerRevisionsCommand, OpenPlanCommand, DeriveHistoricalCommand, CopyForwardCommand, OpenRecoveryCommand }) c?.NotifyCanExecuteChanged();
    }
}
