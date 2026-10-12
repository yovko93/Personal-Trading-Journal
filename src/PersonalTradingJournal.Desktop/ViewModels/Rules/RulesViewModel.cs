using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

public sealed record RuleChoice<T>(T Value, string Label);
public sealed record RuleRow(TradingRuleDetails Details)
{
    public Guid Id => Details.Revision.RuleId;
    public string Name => Details.Revision.Definition.Name;
    public string Statement => Details.Revision.Definition.Criterion;
    public string State => Details.Revision.IsActive ? "Active" : "Archived";
    public string Scope => Details.Revision.Definition.IsGlobal ? "Global principle" : Details.Revision.Definition.AccountScope.AccountId is null ? "Legacy All accounts" :
        (Details.AccountNameAtRevision ?? "Account") + (Details.AccountAvailable ? "" : " (unavailable)");
    public string Label => Name + (!Details.Revision.Definition.IsGlobal ? " · legacy, read-only" : !Details.Revision.IsActive ? " · deleted" : "");
    public string Version => $"Revision {Details.Revision.Revision} · {State}";
    public bool HasAutomaticCondition => Details.Revision.IsActive && Details.Revision.Definition.AutomaticCriterion is not null;
    public string AutomaticCondition => Details.Revision.Definition.AutomaticCriterion is { } c
        ? $"Automatic check: {(c.Capability == RuleCheckCapability.ClosedTradeLoss ? "closed Trade" : "New York closed-day")} {(c.Basis == RuleProfitBasis.Gross ? "Gross" : "strict Net (all costs known)")} loss > {c.MaximumLoss} {c.Currency}. Equality passes."
        : "";
    public string CheckSummary => Details.Revision.Definition.AutomaticCriterion is { } c
        ? $"Typed check: {((Details.Revision.Definition.IsGlobal ? c.Capability == RuleCheckCapability.ClosedTradeLoss : Details.Revision.Definition.Applicability == TradingRuleApplicability.Trade) ? "one closed Trade" : "New York closed-day sum")}, {c.Basis} loss greater than {c.MaximumLoss} {c.Currency}. Equality passes; incomplete required evidence cannot pass."
        : "No typed criterion. Run checks does not assess this rule.";
}

/// <summary>Global principle catalog. Assessment targets and history belong to separate workflows.</summary>
public sealed class RulesViewModel : ObservableObject
{
    private readonly ITradingRuleReader _reader;
    private readonly ITradingRuleStore _store;
    private readonly WriteTradingRuleUseCase _writer;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _read;
    private long _generation;
    private bool _loading, _writing, _editing, _unverified, _loaded;
    private RuleRow? _selected;
    private Guid _editId;
    private Guid? _pendingRuleId;
    private int _expected, _page = 1, _loadedPage = 1, _total;
    private string _message = "";
    private string[] _baseline = [];
    private string _retainedEvidence = "";
    private AutomaticRuleCriterion? _retainedCriterion;
    private IReadOnlyList<RuleRow> _rules = [];

    public RulesViewModel(ITradingRuleReader reader, ITradingRuleStore store,
        WriteTradingRuleUseCase writer, IDialogService dialogs)
    {
        _reader = reader; _store = store; _writer = writer; _dialogs = dialogs;
        EditRowCommand = new AsyncRelayCommand<RuleRow>(async row => { if (row is not null && TrySelect(row)) { await LoadTask; if (_loaded && _selected?.Id == row.Id && !IsEditing) Edit(); } }, row => !IsBusy && row is not null);
        DeleteCommand = new AsyncRelayCommand<RuleRow>(DeleteAsync, row => !IsBusy && row is not null);
        Fields = [new("Rule name", 120), new("Criterion", 4000)];
        foreach (var field in Fields) field.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(HasErrors)); OnPropertyChanged(nameof(HasUnsavedChanges));
            if (e.PropertyName == nameof(RuleField.Error) && !HasErrors && Message.StartsWith("Cannot save.", StringComparison.Ordinal))
                Message = "Fields corrected. Ready to save.";
        };
        CreateCommand = new RelayCommand(Create, () => !IsBusy);
        EditCommand = new RelayCommand(Edit, () => !IsBusy && !IsEditing && SelectedRule?.Details.Revision.IsActive == true && SelectedRule.Details.Revision.Definition.IsGlobal);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditing && !IsBusy && !_unverified);
        CancelEditCommand = new RelayCommand(() => { if (TryLeave()) { IsEditing = false; Message = "Editing cancelled."; } }, () => IsEditing && !IsWriting);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsWriting);
        CancelOperationCommand = new RelayCommand(() => { _read?.Cancel(); SaveCommand.Cancel(); DeleteCommand.Cancel(); }, () => IsBusy);
        NextCommand = new AsyncRelayCommand(() => ChangePage(1), () => !IsBusy && _page * 20L < _total);
        PreviousCommand = new AsyncRelayCommand(() => ChangePage(-1), () => !IsBusy && _page > 1);
    }

    public IReadOnlyList<RuleField> Fields { get; }
    public IReadOnlyList<RuleRow> Rules { get => _rules; private set => SetProperty(ref _rules, value); }
    public RuleRow? SelectedRule => _selected;
    public bool ShowEmpty => Rules.Count == 0 && !IsEditing && !IsLoading;
    public bool IsEditing { get => _editing; private set { SetProperty(ref _editing, value); Changed(); } }
    public bool IsLoading { get => _loading; private set { SetProperty(ref _loading, value); Changed(); } }
    public bool IsWriting { get => _writing; private set { SetProperty(ref _writing, value); Changed(); } }
    public bool IsBusy => IsLoading || IsWriting;
    public bool CanSelect => !IsBusy;
    public bool CanEdit => !IsBusy;
    public bool HasErrors => Fields.Any(f => f.IsInvalid);
    public bool HasUnsavedChanges => IsEditing && !Fields.Select(f => f.Text).SequenceEqual(_baseline);
    public int ValidationAttempt { get; private set; }
    public bool HasMessage => Message.Length > 0;
    public string Message { get => _message; private set { SetProperty(ref _message, value); OnPropertyChanged(nameof(HasMessage)); } }
    public string PageLabel => $"Rules page {_page} · {_total} rules";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand<RuleRow> EditRowCommand { get; }
    public IAsyncRelayCommand<RuleRow> DeleteCommand { get; }
    public IRelayCommand EditCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }

    public Task ActivateAsync() => LoadTask = LoadAsync(true);
    public bool TryLeave()
    {
        if (IsWriting) { Message = "Wait for the Rule operation to finish, or cancel it before leaving."; return false; }
        return !HasUnsavedChanges || _dialogs.Confirm(new("Discard Rule changes?", "Discard the unsaved Rule fields and continue? The saved rule will remain unchanged.", "Discard changes", isDestructive: true));
    }
    public void Deactivate() { ++_generation; _read?.Cancel(); _read = null; IsLoading = false; IsEditing = false; }
    public bool TrySelect(RuleRow? row)
    {
        if (row?.Id == _selected?.Id) return true;
        if (IsBusy || !TryLeave()) return false;
        _selected = row; _pendingRuleId = null; _unverified = false; IsEditing = false;
        LoadTask = LoadAsync(false); return true;
    }
    private async Task RefreshAsync()
    {
        if (!TryLeave()) return;
        // A failed refresh preserves fields and selected state; discard only after successful loading.
        await (LoadTask = LoadAsync(true));
    }
    private async Task LoadAsync(bool catalog)
    {
        _read?.Cancel(); using var cancel = new CancellationTokenSource(); _read = cancel;
        long generation = ++_generation; _loaded = false; IsLoading = true; Message = "Loading Rules…";
        try
        {
            var page = catalog ? await CatalogAsync(cancel.Token) : null;
            var selectedId = _pendingRuleId ?? _selected?.Id;
            var current = selectedId is null ? null : (await _reader.ReadRevisionsAsync(selectedId.Value, limit: 1, cancellationToken: cancel.Token)).FirstOrDefault();
            cancel.Token.ThrowIfCancellationRequested(); if (generation != _generation) return;
            if (page is not null) { Rules = page.Items.Select(r => new RuleRow(r)).ToArray(); _total = page.TotalCount; _loadedPage = _page = page.Page; }
            _selected = current is { Revision.IsActive: true, Revision.Definition.IsGlobal: true } ? new(current) : null;
            _unverified = false; _pendingRuleId = null; IsEditing = false;
            _loaded = true; Message = "";
        }
        catch (OperationCanceledException) { if (generation == _generation) Message = "Loading cancelled. Your fields are retained; Refresh to try again."; }
        catch (Exception) { if (generation == _generation) Message = "Rules could not be loaded. Your fields are retained; Refresh to try again."; }
        finally { if (generation == _generation) { _read = null; _page = _loadedPage; IsLoading = false; Changed(); } }
    }
    private void Create()
    {
        if (!TryLeave()) return;
        _selected = null; _editId = Guid.NewGuid(); _expected = 0; _unverified = false; _pendingRuleId = null;
        Populate(null); Message = "";
    }
    private void Edit()
    {
        _editId = _selected!.Id; _expected = _selected.Details.Revision.Revision; _unverified = false;
        Populate(_selected.Details.Revision.Definition); Message = "";
    }
    private void Populate(TradingRuleDefinition? definition)
    {
        string[] text = definition is null ? ["", ""] : [definition.Name, definition.Criterion];
        for (int i = 0; i < Fields.Count; i++) { Fields[i].Reset(); Fields[i].Text = text[i]; }
        // Hidden historical metadata is carried forward exactly; this form authors prose only.
        _retainedEvidence = definition?.RequiredEvidence ?? "";
        _retainedCriterion = definition?.AutomaticCriterion;
        _baseline = text; IsEditing = true;
    }
    private async Task SaveAsync(CancellationToken token)
    {
        foreach (var field in Fields) field.Validate();
        if (HasErrors) { Message = "Cannot save. Correct the highlighted fields; all entered text is retained."; ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt)); return; }

        var definition = new TradingRuleDefinition(Fields[0].Text, Fields[1].Text, _retainedEvidence, _retainedCriterion);
        await WriteAsync(new(_editId, _expected, _expected == 0 ? TradingRuleAuthoringAction.Create : TradingRuleAuthoringAction.Revise, definition), token);
    }
    private async Task DeleteAsync(RuleRow? row, CancellationToken token)
    {
        if (row is null || !TryLeave()) return;
        if (!_dialogs.Confirm(new("Delete rule?", $"Delete {row.Name} from the Rules list? Existing assessments and their original rule text will be retained. This cannot be undone.", "Delete rule", isDestructive: true))) return;
        await WriteAsync(new(row.Id, row.Details.Revision.Revision, TradingRuleAuthoringAction.Delete, null), token);
    }
    private async Task WriteAsync(WriteTradingRuleCommand command, CancellationToken token)
    {
        IsWriting = true; Message = command.Action == TradingRuleAuthoringAction.Delete ? "Deleting rule…" : "Saving rule…"; bool committed = false;
        try
        {
            var result = await _writer.ExecuteAsync(command, token);
            if (result.Status != TradingRuleWriteStatus.Saved || result.Value is null)
            {
                Message = result.Status switch { TradingRuleWriteStatus.Conflict => "Rule changed. Your fields are retained. Refresh before trying again.", TradingRuleWriteStatus.MissingRule => "Rule no longer exists. Your fields are retained; Refresh to inspect the catalog.", _ => "Rule state does not allow this action. Your fields are retained; Refresh to inspect it." }; return;
            }
            committed = true; _unverified = true; _pendingRuleId = command.RuleId;
            var saved = result.Value;
            var exact = (await _reader.ReadRevisionsAsync(saved.RuleId, saved.Revision == int.MaxValue ? null : saved.Revision + 1, 1, token)).SingleOrDefault();
            if (exact is null || exact.Revision.RuleId != saved.RuleId || exact.Revision.Revision != saved.Revision ||
                exact.Revision.Definition != saved.Definition || exact.Revision.IsActive != saved.IsActive ||
                exact.Revision.RecordedAtUtc != saved.RecordedAtUtc) throw new InvalidOperationException("Rule readback mismatch.");
            var latest = await _store.GetLatestAsync(saved.RuleId, token);
            var catalog = await CatalogAsync(token);
            token.ThrowIfCancellationRequested();
            if (latest is null || latest.Revision < saved.Revision) throw new InvalidOperationException("Rule changed during readback; Refresh required.");
            _selected = new(new(latest, null, true)); Rules = catalog.Items.Select(r => new RuleRow(r)).ToArray(); _total = catalog.TotalCount; _page = _loadedPage = catalog.Page;
            _unverified = false; _pendingRuleId = null; IsEditing = false;
            Message = command.Action == TradingRuleAuthoringAction.Delete ? "Rule deleted. Existing assessments are retained." : "Rule saved.";
            if (!latest.IsActive) _selected = null;
        }
        catch (OperationCanceledException) { Message = committed ? "Save committed but verification was cancelled. Your fields are retained. Refresh before saving again." : "Save cancelled. Your fields are retained; no success was reported."; }
        catch (Exception) { Message = committed ? "Save committed but readback failed. Your fields are retained. Refresh to verify before saving again." : "Rule could not be saved. Your fields are retained. Try again or Refresh."; }
        finally { IsWriting = false; }
    }
    private async Task ChangePage(int direction)
    {
        if (!TryLeave()) return;
        _page += direction; await (LoadTask = LoadAsync(true));
    }
    private async Task<TradingRulePage> CatalogAsync(CancellationToken token)
    {
        var page = await _reader.ReadCatalogAsync(page: _page, pageSize: 20, cancellationToken: token);
        int lastPage = (int)Math.Max(1, ((long)page.TotalCount + 19) / 20);
        return page.Page > lastPage ? await _reader.ReadCatalogAsync(page: lastPage, pageSize: 20, cancellationToken: token) : page;
    }
    private void Changed()
    {
        EditRowCommand?.NotifyCanExecuteChanged(); DeleteCommand?.NotifyCanExecuteChanged();
        foreach (string property in new[] { nameof(SelectedRule), nameof(ShowEmpty), nameof(IsBusy), nameof(CanSelect), nameof(CanEdit), nameof(HasUnsavedChanges), nameof(PageLabel) }) OnPropertyChanged(property);
        CreateCommand?.NotifyCanExecuteChanged(); EditCommand?.NotifyCanExecuteChanged(); SaveCommand?.NotifyCanExecuteChanged(); CancelEditCommand?.NotifyCanExecuteChanged(); RefreshCommand?.NotifyCanExecuteChanged(); CancelOperationCommand?.NotifyCanExecuteChanged(); NextCommand?.NotifyCanExecuteChanged(); PreviousCommand?.NotifyCanExecuteChanged();
    }
}
