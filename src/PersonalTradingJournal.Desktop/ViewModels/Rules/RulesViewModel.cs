using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Diagnostics;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.ViewModels.Rules;

public sealed class RuleField(string label, int limit, bool required = true) : ObservableObject
{
    private string _text = "", _error = "";
    private bool _validated;
    public string Label { get; } = label;
    public int Limit { get; } = limit;
    public bool IsRequired { get; } = required;
    public string Text { get => _text; set { if (SetProperty(ref _text, value) && _validated) Validate(); } }
    public string Error { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(IsInvalid)); OnPropertyChanged(nameof(Help)); } }
    public bool IsInvalid => Error.Length > 0;
    public string Help => IsInvalid ? Error : $"{(IsRequired ? "Required" : "Optional")}; at most {Limit:N0} characters.";
    public void Validate() { _validated = true; Error = IsRequired && string.IsNullOrWhiteSpace(Text) ? "This field is required." : Text.Length > Limit ? $"Use at most {Limit:N0} characters." : ""; }
    public void Reset() { _validated = false; Error = ""; }
}

public enum RuleFeedbackKind { Information, Success, Error }

public sealed record RuleChoice<T>(T Value, string Label);
public sealed record RuleRow(TradingRuleDetails Details, int DisplayNumber = 0)
{
    public Guid Id => Details.Revision.RuleId;
    public string Name => Details.Revision.Definition.Name;
    public string Statement => Details.Revision.Definition.Criterion;
    public bool HasStatement => !string.IsNullOrWhiteSpace(Statement);
    public string PositionDescription => $"Rule list position {DisplayNumber}";
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
    internal RulesDragDiagnostics? DragDiagnostics { get; } = RulesDragDiagnostics.Enabled ? new() : null;
    private readonly ITradingRuleReader _reader;
    private readonly ITradingRuleStore _store;
    private readonly ITradingRuleOrderStore? _order;
    private long _catalogVersion;
    private CancellationTokenSource? _move;
    private readonly WriteTradingRuleUseCase _writer;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _read;
    private long _generation;
    private bool _loading, _writing, _editing, _unverified, _loaded;
    private RuleRow? _selected;
    private Guid _editId;
    private Guid? _pendingRuleId;
    private int _expected;
    private readonly TimeProvider _clock;
    private CancellationTokenSource? _feedbackLifetime;
    private RuleFeedbackKind _feedbackKind;
    internal Task FeedbackExpiryTask { get; private set; } = Task.CompletedTask;
    private string _message = "";
    private string[] _baseline = [];
    private string _retainedEvidence = "";
    private AutomaticRuleCriterion? _retainedCriterion;
    private IReadOnlyList<RuleRow> _rules = [];

    public RulesViewModel(ITradingRuleReader reader, ITradingRuleStore store,
        WriteTradingRuleUseCase writer, IDialogService dialogs, TimeProvider? timeProvider = null, ITradingRuleOrderStore? orderStore = null)
    {
        _reader = reader; _store = store; _writer = writer; _dialogs = dialogs; _clock = timeProvider ?? TimeProvider.System;
        _order = orderStore ?? store as ITradingRuleOrderStore;
        if (DragDiagnostics is { } diagnostics) PropertyChanged += (_, _) => diagnostics.Count(RuleDragCounter.PropertyNotifications);
        MoveUpCommand = new AsyncRelayCommand<RuleRow>(row => MoveByAsync(row, -1), row => CanMove(row, -1));
        MoveDownCommand = new AsyncRelayCommand<RuleRow>(row => MoveByAsync(row, 1), row => CanMove(row, 1));
        EditRowCommand = new AsyncRelayCommand<RuleRow>(async row => { if (row is not null && TrySelect(row)) { await LoadTask; if (_loaded && _selected?.Id == row.Id && !IsEditing) Edit(); } }, row => !IsBusy && row is not null);
        DeleteCommand = new AsyncRelayCommand<RuleRow>(DeleteAsync, row => !IsBusy && row is not null);
        Fields = [new("Rule", 120), new("Criterion", 4000, required: false)];
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
        CancelOperationCommand = new RelayCommand(() => { _read?.Cancel(); _move?.Cancel(); SaveCommand.Cancel(); DeleteCommand.Cancel(); }, () => IsBusy);
    }

    public IReadOnlyList<RuleField> Fields { get; }
    public IReadOnlyList<RuleRow> Rules { get => _rules; private set => SetProperty(ref _rules, value); }
    public RuleRow? SelectedRule => _selected;
    public Guid? EditingRuleId => IsEditing && _expected > 0 ? _editId : null;
    public RulesViewModel? NewRuleEditor => IsEditing && _expected == 0 ? this : null;
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
    public string Message { get => _message; private set => Feedback(value); }
    public RuleFeedbackKind FeedbackKind => _feedbackKind;
    public string FeedbackHeading => FeedbackKind switch { RuleFeedbackKind.Success => "Success", RuleFeedbackKind.Error => "Error", _ => "Information" };
    private void Feedback(string message, RuleFeedbackKind kind = RuleFeedbackKind.Information)
    {
        _feedbackLifetime?.Cancel();
        _feedbackLifetime = null;
        _feedbackKind = kind;
        SetProperty(ref _message, message, nameof(Message));
        OnPropertyChanged(nameof(HasMessage)); OnPropertyChanged(nameof(FeedbackKind)); OnPropertyChanged(nameof(FeedbackHeading));
        if (message.Length > 0)
        {
            var lifetime = new CancellationTokenSource();
            _feedbackLifetime = lifetime;
            FeedbackExpiryTask = ExpireFeedbackAsync(lifetime);
        }
    }
    private async Task ExpireFeedbackAsync(CancellationTokenSource lifetime)
    {
        try
        {
            // Capture the UI synchronization context; timer completion never changes focus.
            await Task.Delay(TimeSpan.FromSeconds(10), _clock, lifetime.Token);
            if (!ReferenceEquals(_feedbackLifetime, lifetime)) return;
            _feedbackLifetime = null;
            SetProperty(ref _message, "", nameof(Message)); OnPropertyChanged(nameof(HasMessage));
        }
        catch (OperationCanceledException) { }
        finally { lifetime.Dispose(); }
    }
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand<RuleRow> MoveUpCommand { get; }
    public IAsyncRelayCommand<RuleRow> MoveDownCommand { get; }
    public IRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand<RuleRow> EditRowCommand { get; }
    public IAsyncRelayCommand<RuleRow> DeleteCommand { get; }
    public IRelayCommand EditCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }

    public Task ActivateAsync() => LoadTask = LoadAsync(true);
    public bool TryLeave()
    {
        if (IsWriting) { Message = "Wait for the Rule operation to finish, or cancel it before leaving."; return false; }
        if (!HasUnsavedChanges || _dialogs.Confirm(new("Discard Rule changes?", "Discard the unsaved Rule fields and continue? The saved rule will remain unchanged.", "Discard changes", isDestructive: true))) return true;
        Message = "Action cancelled. Your unsaved fields are retained.";
        return false;
    }
    public void Deactivate() { Message = ""; ++_generation; _read?.Cancel(); _read = null; IsLoading = false; IsEditing = false; }
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
            if (selectedId is not null) DragDiagnostics?.Count(RuleDragCounter.RevisionReads);
            var current = selectedId is null ? null : (await _reader.ReadRevisionsAsync(selectedId.Value, limit: 1, cancellationToken: cancel.Token)).FirstOrDefault();
            cancel.Token.ThrowIfCancellationRequested(); if (generation != _generation) return;
            if (page is not null) PublishCatalog(page);
            _selected = current is { Revision.IsActive: true, Revision.Definition.IsGlobal: true } ? new(current) : null;
            _unverified = false; _pendingRuleId = null; IsEditing = false;
            _loaded = true; Feedback(catalog ? "Rules refreshed." : "Rule loaded.", RuleFeedbackKind.Success);
        }
        catch (OperationCanceledException) { if (generation == _generation) Message = "Loading cancelled. Your fields are retained; Refresh to try again."; }
        catch (Exception) { if (generation == _generation) Feedback("Rules could not be loaded completely. Your previous list and fields are retained; Refresh to try again.", RuleFeedbackKind.Error); }
        finally { if (generation == _generation) { _read = null; IsLoading = false; Changed(); } }
    }
    private void Create()
    {
        if (!TryLeave()) return;
        _selected = null; _editId = Guid.NewGuid(); _expected = 0; _unverified = false; _pendingRuleId = null;
        Populate(null); Message = "Enter a Rule, optionally add a Criterion, then Save.";
    }
    private void Edit()
    {
        _editId = _selected!.Id; _expected = _selected.Details.Revision.Revision; _unverified = false;
        Populate(_selected.Details.Revision.Definition); Message = "Editing rule. Changes are not saved until you choose Save.";
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
        if (HasErrors) { Feedback("Cannot save. Correct the highlighted fields; all entered text is retained.", RuleFeedbackKind.Error); ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt)); return; }

        var definition = new TradingRuleDefinition(Fields[0].Text, Fields[1].Text, _retainedEvidence, _retainedCriterion);
        await WriteAsync(new(_editId, _expected, _expected == 0 ? TradingRuleAuthoringAction.Create : TradingRuleAuthoringAction.Revise, definition), token);
    }
    private async Task DeleteAsync(RuleRow? row, CancellationToken token)
    {
        if (row is null || !TryLeave()) return;
        if (!_dialogs.Confirm(new("Delete rule?", $"Delete {row.Name} from the Rules list? Existing assessments and their original rule text will be retained. This cannot be undone.", "Delete rule", isDestructive: true))) { Message = "Delete cancelled. The rule is unchanged."; return; }
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
                Feedback(result.Status switch { TradingRuleWriteStatus.Conflict => "Rule changed. Your fields are retained. Refresh before trying again.", TradingRuleWriteStatus.MissingRule => "Rule no longer exists. Your fields are retained; Refresh to inspect the catalog.", _ => "Rule state does not allow this action. Your fields are retained; Refresh to inspect it." }, RuleFeedbackKind.Error); return;
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
            _selected = new(new(latest, null, true)); PublishCatalog(catalog);
            _unverified = false; _pendingRuleId = null; IsEditing = false;
            Feedback(command.Action == TradingRuleAuthoringAction.Delete ? "Rule deleted. Existing assessments are retained." : "Rule saved.", RuleFeedbackKind.Success);
            if (!latest.IsActive) _selected = null;
        }
        catch (OperationCanceledException) { Message = committed ? "Save committed but verification was cancelled. Your fields are retained. Refresh before saving again." : "Save cancelled. Your fields are retained; no success was reported."; }
        catch (Exception) { Feedback(committed ? "Save committed but readback failed. Your fields are retained. Refresh to verify before saving again." : "Rule could not be saved. Your fields are retained. Try again or Refresh.", RuleFeedbackKind.Error); }
        finally { IsWriting = false; }
    }
    private sealed record Catalog(IReadOnlyList<TradingRuleDetails> Items, long Version);
    private void PublishCatalog(Catalog catalog)
    {
        DragDiagnostics?.Count(RuleDragCounter.CatalogPublishes);
        _catalogVersion = catalog.Version;
        Rules = catalog.Items.Select((r, i) => new RuleRow(r, i + 1)).ToArray();
    }
    private Task<Catalog> CatalogAsync(CancellationToken token) => Task.Run(async () =>
    {
        // SQLite async calls may complete synchronously. Keep bounded reads off the UI thread.
        const int size = 20;
        var items = new List<TradingRuleDetails>();
        var ids = new HashSet<Guid>();
        int? total = null;
        long? version = null;
        for (int number = 1; ; number = checked(number + 1))
        {
            token.ThrowIfCancellationRequested();
            DragDiagnostics?.Count(RuleDragCounter.CatalogReads);
            var page = await _reader.ReadCatalogAsync(number, size, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            total ??= page.TotalCount;
            version ??= page.CatalogVersion;
            int expected = (int)Math.Min(size, (long)total.Value - items.Count);
            if (page.CatalogVersion != version || total < 0 || page.TotalCount != total || page.Page != number || page.PageSize != size ||
                expected < 0 || page.Items.Count != expected || page.Items.Any(r => !ids.Add(r.Revision.RuleId)))
                throw new InvalidOperationException("Catalog changed during loading. Refresh required.");
            items.AddRange(page.Items);
            if (items.Count == total) return new Catalog(items, version.Value);
        }
    }, token);
    private bool CanMove(RuleRow? row, int direction)
    {
        int index = row is null ? -1 : Rules.ToList().FindIndex(r => r.Id == row.Id);
        return _order is not null && !IsBusy && index >= 0 && index + direction >= 0 && index + direction < Rules.Count;
    }
    private Task MoveByAsync(RuleRow? row, int direction)
    {
        if (!CanMove(row, direction)) return Task.CompletedTask;
        int index = Rules.ToList().FindIndex(r => r.Id == row!.Id);
        int before = direction < 0 ? index - 1 : index + 2;
        return MoveBeforeAsync(row!.Id, before < Rules.Count ? Rules[before].Id : null);
    }
    public async Task MoveBeforeAsync(Guid ruleId, Guid? beforeRuleId)
    {
        if (_order is null || IsBusy) return;
        int current = Rules.ToList().FindIndex(r => r.Id == ruleId);
        if (current < 0) return;
        Guid? next = current + 1 < Rules.Count ? Rules[current + 1].Id : null;
        // Same insertion slot is not a mutation: no prompt, busy state, read or write.
        if (beforeRuleId == ruleId || beforeRuleId == next) return;
        if (!TryLeave()) return;
        using var cancel = new CancellationTokenSource(); _move = cancel;
        IsWriting = true;
        string message; var kind = RuleFeedbackKind.Error; bool saved = false;
        try
        {
            var command = new MoveTradingRuleCommand(ruleId, beforeRuleId, _catalogVersion);
            DragDiagnostics?.Count(RuleDragCounter.MoveWrites);
            var result = await Task.Run(() => _order.MoveAsync(command, cancel.Token), cancel.Token);
            saved = result == RuleMoveStatus.Saved;
            message = result switch
            {
                RuleMoveStatus.Saved => "",
                RuleMoveStatus.Conflict => "Rules changed elsewhere. The persisted order has been reloaded; move again if needed.",
                _ => "The Rule is no longer available. The persisted order has been reloaded."
            };
            if (saved) kind = RuleFeedbackKind.Success;
        }
        catch (OperationCanceledException) { message = "Move cancelled. The persisted order has been reloaded."; }
        catch (Exception) { message = "Rule could not be moved. The persisted order has been reloaded; Refresh before trying again."; }
        try
        {
            // Read after cancellation too: a commit may already have completed. Never invent a local order.
            PublishCatalog(await CatalogAsync(CancellationToken.None));
            if (saved) IsEditing = false;
            Feedback(message, kind);
        }
        catch (Exception)
        {
            Feedback("The persisted order could not be verified. Refresh before moving again. Your fields are retained.", RuleFeedbackKind.Error);
        }
        finally { _move = null; IsWriting = false; }
    }
    private void Changed()
    {
        MoveUpCommand?.NotifyCanExecuteChanged(); MoveDownCommand?.NotifyCanExecuteChanged();
        EditRowCommand?.NotifyCanExecuteChanged(); DeleteCommand?.NotifyCanExecuteChanged();
        foreach (string property in new[] { nameof(SelectedRule), nameof(EditingRuleId), nameof(NewRuleEditor), nameof(ShowEmpty), nameof(IsBusy), nameof(CanSelect), nameof(CanEdit), nameof(HasUnsavedChanges) }) OnPropertyChanged(property);
        CreateCommand?.NotifyCanExecuteChanged(); EditCommand?.NotifyCanExecuteChanged(); SaveCommand?.NotifyCanExecuteChanged(); CancelEditCommand?.NotifyCanExecuteChanged(); RefreshCommand?.NotifyCanExecuteChanged(); CancelOperationCommand?.NotifyCanExecuteChanged();
    }
}
