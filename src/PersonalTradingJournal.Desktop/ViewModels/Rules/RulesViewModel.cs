using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.ViewModels.Rules;

public sealed class RuleField(string label, int limit) : ObservableObject
{
    private string _text = "", _error = "";
    private bool _validated;
    public string Label { get; } = label;
    public int Limit { get; } = limit;
    public string Text { get => _text; set { if (SetProperty(ref _text, value) && _validated) Validate(); } }
    public string Error { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(IsInvalid)); OnPropertyChanged(nameof(Help)); } }
    public bool IsInvalid => Error.Length > 0;
    public string Help => IsInvalid ? Error : $"Required; at most {Limit:N0} characters.";
    public void Validate() { _validated = true; Error = string.IsNullOrWhiteSpace(Text) ? "This field is required." : Text.Length > Limit ? $"Use at most {Limit:N0} characters." : ""; }
    public void Reset() { _validated = false; Error = ""; }
}

public sealed record RuleAccountOption(Guid? Id, string Label, bool Available = true);
public sealed record RuleChoice<T>(T Value, string Label);
public sealed record RuleRow(TradingRuleDetails Details)
{
    public Guid Id => Details.Revision.RuleId;
    public string Name => Details.Revision.Definition.Name;
    public string State => Details.Revision.IsActive ? "Active" : "Archived";
    public string Scope => Details.Revision.Definition.AccountScope.AccountId is null ? "All accounts" :
        (Details.AccountNameAtRevision ?? "Account") + (Details.AccountAvailable ? "" : " (unavailable)");
    public string Label => $"{Name} · {State} · {Scope}";
    public string Version => $"Revision {Details.Revision.Revision} · {State}";
    public string CheckSummary => Details.Revision.Definition.AutomaticCriterion is { } c
        ? $"Typed check: {(Details.Revision.Definition.Applicability == TradingRuleApplicability.Trade ? "one closed Trade" : "New York closed-day sum")}, {c.Basis} loss greater than {c.MaximumLoss} {c.Currency}. Equality passes; incomplete required evidence cannot pass."
        : "No typed criterion. Run checks does not assess this rule.";
}

/// <summary>Rule authoring and access to retained manual assessments. No evaluator.</summary>
public sealed class RulesViewModel : ObservableObject
{
    private readonly ITradingRuleReader _reader;
    private readonly ITradingRuleStore _store;
    private readonly ITradingAccountReader _accounts;
    private readonly WriteTradingRuleUseCase _writer;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _read;
    private long _generation;
    private bool _loading, _writing, _editing, _unverified;
    private RuleRow? _selected, _historical;
    private Guid _editId;
    private Guid? _pendingRuleId;
    private int _expected, _page = 1, _loadedPage = 1, _total, _historyPage;
    private readonly List<int?> _cursors = [null];
    private bool _older;
    private string _message = "Create or select a rule. Open Manual assessments to read retained results; start a new target from Trade Details or Day Performance.";
    private string[] _baseline = [];
    private Guid? _baselineAccount;
    private TradingRuleApplicability _baselineTarget;
    private TradingRuleEvaluationMode _baselineMode;
    private string _limit = "0", _currency = "USD", _criteriaBaseline = "";
    private bool _automatic;
    private RuleProfitBasis _basis = RuleProfitBasis.Gross;
    private RuleAccountOption? _account;
    private TradingRuleApplicability _target = TradingRuleApplicability.Trade;
    private TradingRuleEvaluationMode _mode = TradingRuleEvaluationMode.ManualOnly;
    private IReadOnlyList<RuleRow> _rules = [], _revisions = [];
    private IReadOnlyList<RuleAccountOption> _accountOptions = [new(null, "All accounts")];

    public RulesViewModel(ITradingRuleReader reader, ITradingRuleStore store, ITradingAccountReader accounts,
        WriteTradingRuleUseCase writer, IDialogService dialogs, IRuleAssessmentDialog? assessments = null)
    {
        _reader = reader; _store = store; _accounts = accounts; _writer = writer; _dialogs = dialogs;
        AssessmentHistoryCommand = new RelayCommand(() =>
        { if (SelectedRule is { } row && !IsBusy) assessments?.ShowRuleHistory(row.Id); }, () => !IsBusy && SelectedRule is not null && assessments is not null);
        Fields = [new("Rule name", 120), new("Criterion", 4000), new("Required evidence", 2000)];
        foreach (var field in Fields) field.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(HasErrors)); OnPropertyChanged(nameof(HasUnsavedChanges));
            if (e.PropertyName == nameof(RuleField.Error) && !HasErrors && Message.StartsWith("Cannot save.", StringComparison.Ordinal))
                Message = "Fields corrected. Save rule to create a new revision.";
        };
        CreateCommand = new RelayCommand(Create, () => !IsBusy);
        EditCommand = new RelayCommand(Edit, () => !IsBusy && !IsEditing && !IsHistorical && SelectedRule?.Details.Revision.IsActive == true && SelectedRule.Details.AccountAvailable);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditing && !IsBusy && !_unverified);
        CancelEditCommand = new RelayCommand(() => { if (TryLeave()) { IsEditing = false; Message = "Editor closed. No revision was written by Cancel."; } }, () => IsEditing && !IsWriting);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsWriting);
        CancelOperationCommand = new RelayCommand(() => { _read?.Cancel(); SaveCommand.Cancel(); ArchiveCommand?.Cancel(); }, () => IsBusy);
        ArchiveCommand = new AsyncRelayCommand(ArchiveAsync, () => !IsBusy && !_unverified && !IsEditing && !IsHistorical && SelectedRule is not null && (SelectedRule.Details.Revision.IsActive || SelectedRule.Details.AccountAvailable));
        ViewRevisionCommand = new RelayCommand<RuleRow>(row => { if (row is not null) { _historical = row; Changed(); } }, row => !IsBusy && row is not null);
        ReturnCommand = new RelayCommand(() => { _historical = null; Changed(); }, () => IsHistorical && !IsBusy);
        NextCommand = new AsyncRelayCommand(() => ChangePage(1), () => !IsBusy && _page * 20L < _total);
        PreviousCommand = new AsyncRelayCommand(() => ChangePage(-1), () => !IsBusy && _page > 1);
        OlderCommand = new AsyncRelayCommand(() => HistoryPage(1), () => !IsBusy && _older);
        NewerCommand = new AsyncRelayCommand(() => HistoryPage(-1), () => !IsBusy && _historyPage > 0);
    }

    public IReadOnlyList<RuleField> Fields { get; }
    public IReadOnlyList<RuleRow> Rules { get => _rules; private set => SetProperty(ref _rules, value); }
    public IReadOnlyList<RuleRow> Revisions { get => _revisions; private set => SetProperty(ref _revisions, value); }
    public IReadOnlyList<RuleAccountOption> Accounts { get => _accountOptions; private set => SetProperty(ref _accountOptions, value); }
    public IReadOnlyList<RuleChoice<TradingRuleApplicability>> Targets { get; } = [new(TradingRuleApplicability.Trade, "One Trade"), new(TradingRuleApplicability.NewYorkDay, "New York trading day")];
    public IReadOnlyList<RuleChoice<TradingRuleEvaluationMode>> Modes { get; } = [new(TradingRuleEvaluationMode.ManualOnly, "Manual assessment only"), new(TradingRuleEvaluationMode.AutomaticCandidate, "Eligible for typed checks")];
    public IReadOnlyList<RuleChoice<RuleProfitBasis>> Bases { get; } = [new(RuleProfitBasis.Gross, "Gross (before costs)"), new(RuleProfitBasis.StrictNet, "Strict Net (all costs required)")];
    public bool Automatic { get => _automatic; set { SetProperty(ref _automatic, value); if (value) Mode = TradingRuleEvaluationMode.AutomaticCandidate; OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    public RuleProfitBasis Basis { get => _basis; set { SetProperty(ref _basis, value); OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    public string LossLimit { get => _limit; set { SetProperty(ref _limit, value); OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    public string CheckCurrency { get => _currency; set { SetProperty(ref _currency, value); OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    private string CriteriaState => $"{Automatic}|{Basis}|{LossLimit}|{CheckCurrency}";
    public RuleAccountOption? Account { get => _account; set { SetProperty(ref _account, value); OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    public TradingRuleApplicability Target { get => _target; set { SetProperty(ref _target, value); OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    public TradingRuleEvaluationMode Mode { get => _mode; set { SetProperty(ref _mode, value); if (value == TradingRuleEvaluationMode.ManualOnly) Automatic = false; OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    public RuleRow? SelectedRule => _selected;
    public RuleRow? Displayed => _historical ?? _selected;
    public bool IsHistorical => _historical is not null;
    public string VersionLabel => IsHistorical ? $"Historical revision {Displayed!.Details.Revision.Revision} · read-only" : Displayed?.Version ?? "No rule selected";
    public string TargetLabel => Displayed?.Details.Revision.Definition.Applicability == TradingRuleApplicability.NewYorkDay ? "New York trading day" : "One Trade";
    public string ModeLabel => Displayed?.Details.Revision.Definition.AutomaticCriterion is { } c
        ? $"Typed check v{c.Version}: {c.Basis} closed loss greater than {c.MaximumLoss} {c.Currency}. Run explicitly from Trade Details or Day Performance. Equality passes; open activity is excluded."
        : "No typed check — manual assessment only; this action will leave the rule unassessed.";
    public bool ShowReading => Displayed is not null && (!IsEditing || IsHistorical);
    public bool ShowEmpty => SelectedRule is null && !IsEditing && !IsLoading;
    public bool IsEditing { get => _editing; private set { SetProperty(ref _editing, value); Changed(); } }
    public bool IsLoading { get => _loading; private set { SetProperty(ref _loading, value); Changed(); } }
    public bool IsWriting { get => _writing; private set { SetProperty(ref _writing, value); Changed(); } }
    public bool IsBusy => IsLoading || IsWriting;
    public bool CanSelect => !IsBusy;
    public bool CanEdit => !IsBusy;
    public bool HasErrors => Fields.Any(f => f.IsInvalid);
    public bool HasUnsavedChanges => IsEditing && (!Fields.Select(f => f.Text).SequenceEqual(_baseline) || Account?.Id != _baselineAccount || Target != _baselineTarget || Mode != _baselineMode || CriteriaState != _criteriaBaseline);
    public int ValidationAttempt { get; private set; }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string PageLabel => $"Rules page {_page} · {_total} rules";
    public string HistoryPageLabel => $"Revision page {_historyPage + 1}";
    public string ArchiveLabel => SelectedRule?.Details.Revision.IsActive == true ? "Archive rule" : "Reopen rule";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IRelayCommand CreateCommand { get; }
    public IRelayCommand AssessmentHistoryCommand { get; }
    public IRelayCommand EditCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public IAsyncRelayCommand ArchiveCommand { get; }
    public IRelayCommand<RuleRow> ViewRevisionCommand { get; }
    public IRelayCommand ReturnCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand OlderCommand { get; }
    public IAsyncRelayCommand NewerCommand { get; }

    public Task ActivateAsync() => LoadTask = LoadAsync(true);
    public bool TryLeave()
    {
        if (IsWriting) { Message = "Wait for the Rule operation to finish, or cancel it before leaving."; return false; }
        return !HasUnsavedChanges || _dialogs.Confirm(new("Discard Rule changes?", "Discard the unsaved Rule fields and continue? Saved revisions will remain unchanged.", "Discard changes", isDestructive: true));
    }
    public void Deactivate() { ++_generation; _read?.Cancel(); _read = null; IsLoading = false; IsEditing = false; }
    public bool TrySelect(RuleRow? row)
    {
        if (row?.Id == _selected?.Id) return true;
        if (IsBusy || !TryLeave()) return false;
        _selected = row; _pendingRuleId = null; _unverified = false; IsEditing = false; ResetHistory();
        LoadTask = LoadAsync(false); return true;
    }
    private async Task RefreshAsync()
    {
        if (!TryLeave()) return;
        // A failed refresh preserves fields and selected state; discard only after successful loading.
        await (LoadTask = LoadAsync(true));
    }
    private void ResetHistory() { _historical = null; _historyPage = 0; _cursors.Clear(); _cursors.Add(null); }
    private async Task LoadAsync(bool catalog)
    {
        _read?.Cancel(); using var cancel = new CancellationTokenSource(); _read = cancel;
        long generation = ++_generation; IsLoading = true; Message = "Loading Rules…";
        try
        {
            var accounts = catalog ? await _accounts.GetAllAsync(cancel.Token) : null;
            var page = catalog ? await _reader.ReadCurrentAsync(page: _page, pageSize: 20, cancellationToken: cancel.Token) : null;
            var selectedId = _pendingRuleId ?? _selected?.Id;
            var current = selectedId is null ? null : (await _reader.ReadRevisionsAsync(selectedId.Value, limit: 1, cancellationToken: cancel.Token)).FirstOrDefault();
            var revisions = current is null ? [] : await _reader.ReadRevisionsAsync(current.Revision.RuleId, limit: 11, cancellationToken: cancel.Token);
            cancel.Token.ThrowIfCancellationRequested(); if (generation != _generation) return;
            if (accounts is not null) Accounts = new[] { new RuleAccountOption(null, "All accounts") }.Concat(accounts.Select(a => new RuleAccountOption(a.Id, a.Name + (a.IsActive ? "" : " (inactive)")))).ToArray();
            if (page is not null) { Rules = page.Items.Select(r => new RuleRow(r)).ToArray(); _total = page.TotalCount; _loadedPage = _page; }
            _selected = current is null ? null : new(current); ResetHistory(); SetHistory(revisions);
            _unverified = false; _pendingRuleId = null; IsEditing = false;
            Message = _selected is null ? "Select a rule or create your first rule. Rules do not record a pass or violation by themselves." : "Current persisted revision loaded. Assessments are separate and are not performed here.";
        }
        catch (OperationCanceledException) { if (generation == _generation) Message = "Loading cancelled. Your fields are retained; Refresh to try again."; }
        catch (Exception) { if (generation == _generation) Message = "Rules could not be loaded. Your fields are retained; Refresh to try again."; }
        finally { if (generation == _generation) { _read = null; _page = _loadedPage; IsLoading = false; Changed(); } }
    }
    private void Create()
    {
        if (!TryLeave()) return;
        _selected = null; ResetHistory(); Revisions = []; _editId = Guid.NewGuid(); _expected = 0; _unverified = false; _pendingRuleId = null;
        Populate(null); Message = "New rule · Save creates revision 1. All accounts is an explicit scope.";
    }
    private void Edit()
    {
        _editId = _selected!.Id; _expected = _selected.Details.Revision.Revision; _unverified = false;
        Populate(_selected.Details.Revision.Definition); Message = "Editing the current rule. Save creates a new immutable revision.";
    }
    private void Populate(TradingRuleDefinition? definition)
    {
        string[] text = definition is null ? ["", "", ""] : [definition.Name, definition.Criterion, definition.RequiredEvidence];
        for (int i = 0; i < Fields.Count; i++) { Fields[i].Reset(); Fields[i].Text = text[i]; }
        var id = definition?.AccountScope.AccountId;
        Account = Accounts.FirstOrDefault(a => a.Id == id);
        if (Account is null) { Account = new(id, (_selected?.Scope ?? "Account") + " (unavailable)", false); Accounts = Accounts.Append(Account).ToArray(); }
        Target = definition?.Applicability ?? TradingRuleApplicability.Trade;
        Mode = definition?.EvaluationMode ?? TradingRuleEvaluationMode.ManualOnly;
        Automatic = definition?.AutomaticCriterion is not null;
        Basis = definition?.AutomaticCriterion?.Basis ?? RuleProfitBasis.Gross;
        LossLimit = (definition?.AutomaticCriterion?.MaximumLoss ?? 0).ToString(System.Globalization.CultureInfo.CurrentCulture);
        CheckCurrency = definition?.AutomaticCriterion?.Currency ?? "USD";
        _criteriaBaseline = CriteriaState;
        _baseline = text; _baselineAccount = Account.Id; _baselineTarget = Target; _baselineMode = Mode; IsEditing = true;
    }
    private async Task SaveAsync(CancellationToken token)
    {
        foreach (var field in Fields) field.Validate();
        if (HasErrors) { Message = "Cannot save. Correct the highlighted fields; all entered text is retained."; ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt)); return; }
        if (Account?.Available != true || !Enum.IsDefined(Target) || !Enum.IsDefined(Mode)) { Message = "Choose an available Account, target and assessment eligibility. Your fields are retained."; return; }
        TradingRuleDefinition definition;
        try
        {
            AutomaticRuleCriterion? criterion = null;
            if (Automatic)
            {
                if (!decimal.TryParse(LossLimit, out var limit)) throw new ArgumentException();
                criterion = new(Basis, limit, CheckCurrency.Trim().ToUpperInvariant());
            }
            definition = new(Fields[0].Text, Fields[1].Text, Fields[2].Text, Target,
                Account.Id is Guid id ? TradingRuleAccountScope.ForAccount(id) : TradingRuleAccountScope.AllAccounts,
                Automatic ? TradingRuleEvaluationMode.AutomaticCandidate : Mode, criterion);
        }
        catch (ArgumentException) { Message = "Cannot save. Typed check needs a nonnegative decimal loss limit, a supported basis and three-letter currency. Your fields are retained."; return; }
        await WriteAsync(new(_editId, _expected, _expected == 0 ? TradingRuleAuthoringAction.Create : TradingRuleAuthoringAction.Revise, definition), token);
    }
    private async Task ArchiveAsync(CancellationToken token)
    {
        var current = _selected!.Details.Revision;
        if (!_dialogs.Confirm(new(ArchiveLabel + "?", $"{ArchiveLabel}: {current.Definition.Name}. This appends a revision and preserves all history. Archived rules cannot receive new assessments.", ArchiveLabel))) return;
        await WriteAsync(new(current.RuleId, current.Revision, current.IsActive ? TradingRuleAuthoringAction.Archive : TradingRuleAuthoringAction.Reopen, null), token);
    }
    private async Task WriteAsync(WriteTradingRuleCommand command, CancellationToken token)
    {
        IsWriting = true; Message = "Saving Rule revision…"; bool committed = false;
        try
        {
            var result = await _writer.ExecuteAsync(command, token);
            if (result.Status != TradingRuleWriteStatus.Saved || result.Value is null)
            {
                Message = result.Status switch { TradingRuleWriteStatus.Conflict => "Rule revision or Account changed. Your fields are retained. Refresh to inspect the latest revision before editing again.", TradingRuleWriteStatus.MissingRule => "Rule no longer exists. Your fields are retained; Refresh to inspect the catalog.", _ => "Rule state does not allow this action. Your fields are retained; Refresh to inspect it." }; return;
            }
            committed = true; _unverified = true; _pendingRuleId = command.RuleId;
            var saved = result.Value;
            var exact = (await _reader.ReadRevisionsAsync(saved.RuleId, saved.Revision == int.MaxValue ? null : saved.Revision + 1, 1, token)).SingleOrDefault();
            if (exact is null || exact.Revision.RuleId != saved.RuleId || exact.Revision.Revision != saved.Revision ||
                exact.Revision.Definition != saved.Definition || exact.Revision.IsActive != saved.IsActive ||
                exact.Revision.RecordedAtUtc != saved.RecordedAtUtc) throw new InvalidOperationException("Rule readback mismatch.");
            var latest = await _store.GetLatestAsync(saved.RuleId, token);
            var history = await _reader.ReadRevisionsAsync(saved.RuleId, limit: 11, cancellationToken: token);
            var catalog = await _reader.ReadCurrentAsync(page: _page, pageSize: 20, cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (latest is null || history.Count == 0 || latest.Revision != history[0].Revision.Revision) throw new InvalidOperationException("Rule changed during readback; Refresh required.");
            _selected = new(history[0]); ResetHistory(); SetHistory(history); Rules = catalog.Items.Select(r => new RuleRow(r)).ToArray(); _total = catalog.TotalCount;
            _unverified = false; _pendingRuleId = null; IsEditing = false;
            Message = $"Saved and verified revision {saved.Revision}." + (latest.Revision > saved.Revision ? $" A newer revision {latest.Revision} is now displayed." : " History remains immutable.");
        }
        catch (OperationCanceledException) { Message = committed ? "Save committed but verification was cancelled. Your fields are retained. Refresh before saving again." : "Save cancelled. Your fields are retained; no success was reported."; }
        catch (Exception) { Message = committed ? "Save committed but readback failed. Your fields are retained. Refresh to verify before saving again." : "Rule could not be saved. Your fields are retained. Try again or Refresh."; }
        finally { IsWriting = false; }
    }
    private void SetHistory(IReadOnlyList<TradingRuleDetails> rows) { Revisions = rows.Take(10).Select(r => new RuleRow(r)).ToArray(); _older = rows.Count > 10; }
    private async Task ChangePage(int direction)
    {
        if (!TryLeave()) return;
        _page += direction; await (LoadTask = LoadAsync(true));
    }
    private async Task HistoryPage(int direction)
    {
        if (_selected is null) return;
        int next = _historyPage + direction;
        int? cursor = direction > 0 ? Revisions.Last().Details.Revision.Revision : _cursors[next];
        using var cancel = new CancellationTokenSource(); _read = cancel; long generation = ++_generation; IsLoading = true;
        try
        {
            var rows = await _reader.ReadRevisionsAsync(_selected.Id, cursor, 11, cancel.Token);
            cancel.Token.ThrowIfCancellationRequested(); if (generation != _generation) return;
            if (next == _cursors.Count) _cursors.Add(cursor); else _cursors[next] = cursor;
            _historyPage = next; _historical = null; SetHistory(rows);
        }
        catch (OperationCanceledException) { if (generation == _generation) Message = "Revision loading cancelled. Current fields and history retained."; }
        catch (Exception) { if (generation == _generation) Message = "Revisions could not be loaded. Current fields and history retained."; }
        finally { if (generation == _generation) { _read = null; IsLoading = false; } }
    }
    private void Changed()
    {
        AssessmentHistoryCommand?.NotifyCanExecuteChanged();
        foreach (string property in new[] { nameof(SelectedRule), nameof(Displayed), nameof(IsHistorical), nameof(VersionLabel), nameof(TargetLabel), nameof(ModeLabel), nameof(ShowReading), nameof(ShowEmpty), nameof(IsBusy), nameof(CanSelect), nameof(CanEdit), nameof(HasUnsavedChanges), nameof(PageLabel), nameof(HistoryPageLabel), nameof(ArchiveLabel) }) OnPropertyChanged(property);
        CreateCommand?.NotifyCanExecuteChanged(); EditCommand?.NotifyCanExecuteChanged(); SaveCommand?.NotifyCanExecuteChanged(); CancelEditCommand?.NotifyCanExecuteChanged(); RefreshCommand?.NotifyCanExecuteChanged(); CancelOperationCommand?.NotifyCanExecuteChanged(); ArchiveCommand?.NotifyCanExecuteChanged(); ViewRevisionCommand?.NotifyCanExecuteChanged(); ReturnCommand?.NotifyCanExecuteChanged(); NextCommand?.NotifyCanExecuteChanged(); PreviousCommand?.NotifyCanExecuteChanged(); OlderCommand?.NotifyCanExecuteChanged(); NewerCommand?.NotifyCanExecuteChanged();
    }
}
