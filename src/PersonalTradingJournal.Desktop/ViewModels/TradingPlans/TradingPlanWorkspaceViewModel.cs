using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public enum PlanLeaveChoice { Cancel, Save, Discard }
public interface ITradingPlanInteraction
{
    PlanLeaveChoice ConfirmLeave();
    bool ConfirmDelete(string date, string scope) => false;
}
public sealed record PlanAccountOption(Guid? Id, string Name, string Currency, bool IsActive, bool IsAvailable = true)
{
    public string Label => Id is null ? "All Accounts" : $"{Name} · {Currency}" + (!IsAvailable ? " · unavailable" : IsActive ? "" : " · inactive");
}

/// <summary>Retained page coordinator; the section session alone owns draft fields and the save token.
/// Selection is committed after consent. A leave decision is consumed only after all shell guards accept.</summary>
public sealed partial class TradingPlanWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly ITradingPlanStore _store;
    private readonly ITradingAccountReader _accounts;
    private readonly IInstrumentReader _instruments;
    private readonly ITradingPlanReferenceReader _references;
    private readonly ITradingSetupReader _setups;
    private readonly ITradingSetupPlaybookStore _playbooks;
    private readonly ITradingSetupPlaybookHistoryReader _history;
    private readonly ITradingRuleReader _rules;
    private readonly ITradingRuleStore _ruleStore;
    private readonly ITradingPlanInteraction _interaction;
    private readonly TimeProvider _clock;
    private readonly HashSet<Task> _loads = [];
    private readonly object _loadSync = new();
    private bool HasOutstandingLoads { get { lock (_loadSync) return _loads.Any(t => !t.IsCompleted); } }
    private CancellationTokenSource? _load, _write;
    private TaskCompletionSource? _deleteCompletion;
    private long _epoch;
    private bool _initialized, _disposed, _loading, _writing, _guarding;
    private bool _discardAccepted;
    private string? _acceptedIdentity;
    private DateOnly _date;
    private string _dateText, _message = "Select an Account to prepare a New York session.", _dateError = "";
    private PlanAccountOption? _account;
    private IReadOnlyList<PlanAccountOption> _choices = [];
    private SessionPreparationViewModel? _editor;
    private readonly PersonalTradingJournal.Desktop.Screenshots.ITradingPlanImages? _images;

    public TradingPlanWorkspaceViewModel(ITradingPlanStore store, ITradingAccountReader accounts, IInstrumentReader instruments,
        ITradingPlanReferenceReader references, ITradingSetupReader setups, ITradingSetupPlaybookStore playbooks,
        ITradingSetupPlaybookHistoryReader history, ITradingRuleReader rules, ITradingRuleStore ruleStore,
        ITradingPlanInteraction interaction, TimeProvider? clock = null, PersonalTradingJournal.Desktop.Screenshots.ITradingPlanImages? images = null,
        IDailyTradingPrincipleStore? principles = null)
    {
        _store = store; _accounts = accounts; _instruments = instruments; _references = references;
        _setups = setups; _playbooks = playbooks; _history = history; _rules = rules; _ruleStore = ruleStore;
        _interaction = interaction; _clock = clock ?? TimeProvider.System; _images = images;
        Principles = new(principles);
        _date = Today; _dateText = Format(_date);
        PreviousDayCommand = new AsyncRelayCommand(() => MoveDateAsync(-1), () => CanSelectContext && _date > DateOnly.MinValue);
        NextDayCommand = new AsyncRelayCommand(() => MoveDateAsync(1), () => CanSelectContext && _date < DateOnly.MaxValue);
        TodayCommand = new AsyncRelayCommand(() => SelectContextAsync(Today, _account), () => CanSelectContext);
        OpenDateCommand = new AsyncRelayCommand(OpenDateAsync, () => CanSelectContext);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => !IsBusy && !_guarding);
        CreateDraftCommand = new AsyncRelayCommand(CreateDraftAsync, () => CanCreateDraft);
        SaveCommand = new AsyncRelayCommand(async () => await SaveAsync(), () => CanSave);
        MarkReadyCommand = new AsyncRelayCommand(MarkReadyAsync, () => Editor?.CanEdit == true && !IsBusy && !HasPendingDate);
        StartDraftCommand = new AsyncRelayCommand(StartDraftAsync, () => Editor?.IsReadOnly == true && !IsBusy && !HasPendingDate);
        DeletePlanCommand = new AsyncRelayCommand(DeletePlanAsync, () => HasEditor && !IsBusy && !_guarding);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsBusy);
        InitializeHistory();
        InitializeReviewNavigation();
    }

    public DailyTradingPrinciplesViewModel Principles { get; }
    private bool _preparationExpanded = true, _scenariosExpanded = true, _riskExpanded = true;
    public bool PreparationExpanded { get => _preparationExpanded; set => SetProperty(ref _preparationExpanded, value); }
    public bool ScenariosExpanded { get => _scenariosExpanded; set => SetProperty(ref _scenariosExpanded, value); }
    public bool RiskExpanded { get => _riskExpanded; set => SetProperty(ref _riskExpanded, value); }

    private DateOnly Today => DateOnly.FromDateTime(TradingTimePolicy.ConvertUtcToTradingTime(_clock.GetUtcNow()).DateTime);
    private static string Format(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    public DateOnly SelectedDate => _date;
    public DateTime? CalendarDate
    {
        get => _date.ToDateTime(TimeOnly.MinValue);
        set { if (value.HasValue && CanSelectContext) ContextTask = SelectContextAsync(DateOnly.FromDateTime(value.Value), _account); }
    }
    public string DateText { get => _dateText; set { if (CanSelectContext && SetProperty(ref _dateText, value)) { ValidateDate(); Notify(); } } }
    public string DateError => _dateError;
    public bool HasDateError => _dateError.Length > 0;
    public bool HasPendingDate => !TryDate(out var date) || date != _date;
    public bool CanInteractEditor => !HasPendingDate && !_loading && !_writing;
    public IReadOnlyList<PlanAccountOption> Accounts => _choices;
    private string _globalCurrency = "";
    public bool IsGlobalScope => _account is { Id: null };
    public bool ShowCurrencyField => IsGlobalScope || HasEditor;
    public string PlanCurrency
    {
        get => Editor?.PlanCurrency ?? _globalCurrency;
        set { if (Editor is { } editor) editor.PlanCurrency = value; else _globalCurrency = value; OnPropertyChanged(); Notify(); }
    }
    private static bool ValidCurrency(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 8 && value == value.Trim().ToUpperInvariant();
    public PlanAccountOption? SelectedAccount
    {
        get => _account;
        set { if (CanSelectContext && (value is null || Accounts.Contains(value))) ContextTask = SelectContextAsync(_date, value); }
    }
    public SessionPreparationViewModel? Editor => _editor;
    public bool HasEditor => Editor is not null;
    public bool IsLoading => _loading;
    public bool IsBusy => _loading || _writing || _historyLoading || _historyTransition || Editor?.IsBusy == true;
    public bool CanSelectContext => !_writing && !_historyTransition && Editor?.IsBusy != true && !_guarding && !_disposed;
    public bool CanSave => Editor?.CanEdit == true && !IsBusy && !HasPendingDate;
    public bool CanCreateDraft => !HasEditor && !IsBusy && _account?.IsActive == true && !HasPendingDate && _contextLoaded;
    private bool _contextLoaded;
    public bool ShowEmpty => !HasEditor && !IsLoading;
    public string EmptyMessage => !_initialized ? "Load Accounts to begin." : _account is null
        ? Accounts.Count == 0 ? "No Accounts are available. Add an Account in Accounts before creating a Trading Plan." :
            Accounts.All(a => !a.IsActive) ? "No active Accounts. Reopen an Account before creating preparation; existing Plans remain readable under their exact inactive Account." :
            "Select All Accounts for shared preparation, or an exact Account."
        : !_contextLoaded ? "The selected context could not be loaded. Retry loading; no empty Plan has been saved."
        : !_account.IsActive ? "No Plan for this date. Reopen this Account in Accounts before creating preparation."
        : IsGlobalScope && !ValidCurrency(PlanCurrency) ? "Create Draft reuses the previous saved shared preparation when available. Otherwise enter an explicit currency (up to 8 uppercase characters). No currency conversion is performed."
        : "No Plan for this scope and New York date. Create Draft explicitly to begin; opening this page saves nothing.";
    public string ContextLabel => $"{Format(_date)} · New York · " + (Editor?.IsReadOnly == true
        ? $"{Editor.CurrentPlan.LatestReadyRevision!.References.Account.Name} · {Editor.CurrentPlan.LatestReadyRevision.References.Account.Currency} · saved Account context"
        : _account?.Label ?? "No Account selected");
    public string StateLabel => Editor?.StateLabel ?? "No Plan";
    public string Message { get => _message; private set => Report(value); }
    private PlanNoticeKind _messageKind;
    public PlanNotice Notice => new(Message, _messageKind);
    private void Report(string text, PlanNoticeKind kind = PlanNoticeKind.Info)
    {
        _messageKind = kind;
        SetProperty(ref _message, text, nameof(Message));
        OnPropertyChanged(nameof(Notice));
    }
    public Task ContextTask { get; private set; } = Task.CompletedTask;
    public Func<Guid, CancellationToken, Task<bool>>? OpenPlaybook { get; set; }
    public Func<Guid, CancellationToken, Task<bool>>? OpenRule { get; set; }
    public IAsyncRelayCommand PreviousDayCommand { get; }
    public IAsyncRelayCommand NextDayCommand { get; }
    public IAsyncRelayCommand TodayCommand { get; }
    public IAsyncRelayCommand OpenDateCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IAsyncRelayCommand CreateDraftCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand MarkReadyCommand { get; }
    public IAsyncRelayCommand StartDraftCommand { get; }
    public IAsyncRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand DeletePlanCommand { get; }

    public Task ActivateAsync() => _disposed || _initialized && (_editor is not null || _contextLoaded)
        ? Task.CompletedTask : ContextTask = LoadAsync(refreshAccounts: true);

    public async Task<bool> SelectContextAsync(DateOnly date, PlanAccountOption? account)
    {
        if (!CanSelectContext || date == _date && account?.Id != _account?.Id && HasPendingDate) { RestoreSelectors(); return false; }
        if (date == _date && account?.Id == _account?.Id) { _dateText = Format(_date); ValidateDate(); RestoreSelectors(); return true; }
        if (_loading && Editor is null) _load?.Cancel(); // Supersede reads; close/disposal drains all owners.
        else if (!await PrepareLeaveAsync()) { _dateText = Format(_date); ValidateDate(); RestoreSelectors(); return false; }
        CompleteLeave(); _date = date; _account = account; _dateText = Format(date); ValidateDate();
        ReplaceEditor(null); _contextLoaded = false; RestoreSelectors();
        await LoadAsync(refreshAccounts: false); return true;
    }
    private Task MoveDateAsync(int days) => HasDateError ? InvalidDateAsync() : SelectContextAsync(_date.AddDays(days), _account);
    private Task InvalidDateAsync() { Report("Correct the typed date, then select Open date (or choose a date from the calendar). The loaded Plan is unchanged.", PlanNoticeKind.Warning); DateValidationAttempt++; OnPropertyChanged(nameof(DateValidationAttempt)); return Task.CompletedTask; }
    public int DateValidationAttempt { get; private set; }
    private Task OpenDateAsync() => TryDate(out var date) ? SelectContextAsync(date, _account) : InvalidDateAsync();
    private bool TryDate(out DateOnly date) => DateOnly.TryParse(_dateText, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date) ||
        DateOnly.TryParseExact(_dateText, ["dd MMM yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
    private void ValidateDate()
    { _dateError = TryDate(out _) ? "" : "Enter a valid New York date, for example 19 Oct 2026."; OnPropertyChanged(nameof(DateError)); OnPropertyChanged(nameof(HasDateError)); }
    private void RestoreSelectors()
    { OnPropertyChanged(nameof(SelectedAccount)); OnPropertyChanged(nameof(IsGlobalScope)); OnPropertyChanged(nameof(PlanCurrency)); OnPropertyChanged(nameof(CalendarDate)); OnPropertyChanged(nameof(DateText)); Notify(); }

    public async Task ReloadAsync()
    {
        if (IsBusy || !await PrepareLeaveAsync()) return;
        CompleteLeave(); ReplaceEditor(null); await LoadAsync(refreshAccounts: true);
    }
    private Task LoadAsync(bool refreshAccounts)
    {
        var task = LoadCoreAsync(refreshAccounts); lock (_loadSync) _loads.Add(task);
        return TrackAsync(task);
    }
    private async Task TrackAsync(Task task) { try { await task; } finally { lock (_loadSync) _loads.Remove(task); } }
    private async Task LoadCoreAsync(bool refreshAccounts)
    {
        _load?.Cancel(); using var cancellation = new CancellationTokenSource(); _load = cancellation;
        long epoch = ++_epoch; var date = _date; var account = _account;
        _loading = true; _contextLoaded = false; Report("Loading Session Brief…", PlanNoticeKind.Info); Notify();
        SessionPreparationViewModel? candidate = null;
        try
        {
            // Include first-use reminder initialization in the public, tracked context lifetime.
            // Review navigation awaits ContextTask; closing cancels and drains this same owner.
            await Principles.LoadAsync();
            cancellation.Token.ThrowIfCancellationRequested();
            if (refreshAccounts)
            {
                var rows = await Task.Run(() => _accounts.GetAllAsync(cancellation.Token), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (epoch != _epoch || _disposed) return;
                var currencies = rows.Where(a => a.IsActive).Select(a => a.Currency).Distinct().ToArray();
                if (!_initialized && currencies.Length == 1) _globalCurrency = currencies[0];
                _choices = [new(null, "All Accounts", "", true), .. rows.OrderBy(a => a.Name, StringComparer.Ordinal).ThenBy(a => a.Id).Select(a => new PlanAccountOption(a.Id, a.Name, a.Currency, a.IsActive))];
                // Keep exact unavailable identity; never choose a same-name replacement.
                account = account is null ? _choices[0] : _choices.FirstOrDefault(a => a.Id == account.Id) ?? account with { IsActive = false, IsAvailable = false };
                if (account is { IsAvailable: false }) _choices = [.. _choices, account];
                _account = account; _initialized = true; OnPropertyChanged(nameof(Accounts)); RestoreSelectors();
            }
            if (account is null) { _contextLoaded = true; Report("Select an Account to begin.", PlanNoticeKind.Info); return; }
            var key = new TradingPlanKey(account.Id, date);
            var plan = await Task.Run(() => _store.LoadAsync(key, cancellation.Token), cancellation.Token);
            if (plan is not null && plan.Key != key) throw new InvalidOperationException("Mismatched Plan context.");
            if (plan is not null)
            {
                candidate = CreateEditor(plan);
                await LoadSourcesAsync(candidate, cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (_disposed || epoch != _epoch || date != _date || account.Id != _account?.Id) return;
            ReplaceEditor(candidate); candidate = null; _contextLoaded = true;
            Message = plan is null ? "" : "Session Brief loaded.";
        }
        catch (OperationCanceledException) { if (epoch == _epoch && !_disposed) Report("Loading cancelled. Select Reload to try again; no Plan was changed.", PlanNoticeKind.Info); }
        catch (Exception) { if (epoch == _epoch && !_disposed) Report("Session Brief could not be loaded. Reload to try again; no Plan was changed.", PlanNoticeKind.Error); }
        finally
        {
            candidate?.Dispose();
            if (epoch == _epoch) { _load = null; _loading = false; Notify(); }
        }
    }
    private SessionPreparationViewModel CreateEditor(TradingPlan plan, TradingPlanReadyRevision? historical = null)
    {
        var editor = new SessionPreparationViewModel(plan, _store, _instruments, historical: historical) { IsHosted = true };
        if (_images is not null) editor.ConfigureImages(_images);
        editor.ConfigurePlaybooks(_setups, _playbooks, _history); editor.ConfigureRules(_rules, _ruleStore); editor.ConfigureReferenceReader(_references);
        editor.PlaybookNavigation = (id, ct) => OpenPlaybook?.Invoke(id, ct) ?? Task.FromResult(false);
        editor.RuleNavigation = (id, ct) => OpenRule?.Invoke(id, ct) ?? Task.FromResult(false);
        return editor;
    }
    private static async Task LoadSourcesAsync(SessionPreparationViewModel editor, CancellationToken ct)
    {
        await editor.LoadCatalogAsync(ct); ct.ThrowIfCancellationRequested();
        await editor.LoadThumbnailsAsync(ct); ct.ThrowIfCancellationRequested();
        await editor.LoadPlaybooksAsync(ct); ct.ThrowIfCancellationRequested();
        await editor.LoadRulesAsync(ct); ct.ThrowIfCancellationRequested();
        if (!editor.IsReadOnly) await editor.CheckReferencesAsync(ct);
        ct.ThrowIfCancellationRequested();
    }
    private void ReplaceEditor(SessionPreparationViewModel? editor)
    {
        if (_editor is not null) { _editor.PropertyChanged -= EditorChanged; _editor.Dispose(); }
        _editor = editor;
        if (editor is not null) editor.PropertyChanged += EditorChanged;
        OnPropertyChanged(nameof(Editor)); OnPropertyChanged(nameof(PlanCurrency)); Notify();
    }
    private void EditorChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SessionPreparationViewModel.Notice)) Report(Editor!.Message, Editor.Notice.Kind);
        if (args.PropertyName is nameof(SessionPreparationViewModel.IsBusy) or nameof(SessionPreparationViewModel.IsDirty) or nameof(SessionPreparationViewModel.StateLabel)) Notify();
    }
    public async Task<bool> SaveAsync()
    {
        if (!CanSave) { if (HasPendingDate) await InvalidDateAsync(); return false; }
        Report("Saving Draft…", PlanNoticeKind.Info);
        return await Editor!.SaveAsync() == TradingPlanWriteStatus.Success;
    }
    public async Task CreateDraftAsync()
    {
        if (!CanCreateDraft) return;
        var key = new TradingPlanKey(_account!.Id, _date);
        _writing = true; using var cancellation = new CancellationTokenSource(); _write = cancellation; Notify();
        try
        {
            Report("Creating Draft…", PlanNoticeKind.Info);
            // Let the atomic Create policy find saved shared currency before requiring a
            // blank-form fallback. Invalid raw text remains on screen if no source exists.
            var empty = new TradingPlanContent(planCurrency: IsGlobalScope && ValidCurrency(PlanCurrency) ? PlanCurrency : null);
            var result = await Task.Run(() => _store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, empty), cancellation.Token), cancellation.Token);
            if (result.Status == TradingPlanWriteStatus.Success && result.Plan is { WorkingDraft: not null } plan && plan.Key == key &&
                plan.Version == 1 && plan.LatestReadyRevision is null && plan.WorkingDraft.DerivedFromRevision is null && plan.WorkingDraft.Content.Screenshots.IsEmpty)
            { ReplaceEditor(CreateEditor(plan)); await LoadSourcesAsync(Editor!, CancellationToken.None); Report(result.PrefilledFromDate is { } sourceDate ? $"Draft created from saved preparation on {Format(sourceDate)}; screenshots excluded. Review references and values before Ready." : "Draft created and verified. Complete preparation, then Save Draft.", result.References.IsDefaultOrEmpty ? PlanNoticeKind.Success : PlanNoticeKind.Warning); }
            else Report(result.Status == TradingPlanWriteStatus.TargetAlreadyExists ? "A Plan was created elsewhere. Reload this exact date and Account." :
                result.Status == TradingPlanWriteStatus.Cancelled ? "Creation cancelled. No success was reported." :
                IsGlobalScope && !ValidCurrency(PlanCurrency) ? "No usable shared preparation supplied a currency. Enter a valid currency, then Create Draft. Your input is retained." : "Draft could not be created and verified. Reload to check the stored state.",
                result.Status == TradingPlanWriteStatus.Cancelled ? PlanNoticeKind.Info : PlanNoticeKind.Error);
        }
        catch (OperationCanceledException) { Report("Creation cancelled.", PlanNoticeKind.Info); }
        catch (Exception) { Report("Draft could not be created and verified. Reload to check the stored state.", PlanNoticeKind.Error); }
        finally { _write = null; _writing = false; Notify(); }
    }
    public async Task MarkReadyAsync()
    {
        if (IsBusy || HasPendingDate || Editor is null) return;
        var selected = Editor.SelectedInstrument?.InstrumentId;
        _writing = true; Notify();
        try
        {
            Report("Marking preparation Ready…", PlanNoticeKind.Info);
            var result = await Editor.MarkReadyAsync();
            if (result.Status == TradingPlanWriteStatus.Success)
            {
                ReplaceEditor(CreateEditor(result.Plan!));
                Editor!.SelectedInstrument = Editor.Contexts.FirstOrDefault(c => c.InstrumentId == selected);
                await Editor.LoadThumbnailsAsync(CancellationToken.None);
                Report($"Ready revision {result.Plan!.LatestReadyRevision!.Id.Revision} saved and verified · read-only.", PlanNoticeKind.Success);
            }
            else { ReadinessFocusRequest++; OnPropertyChanged(nameof(ReadinessFocusRequest)); }
        }
        finally { _writing = false; Notify(); }
    }
    public int ReadinessFocusRequest { get; private set; }
    public async Task StartDraftAsync()
    {
        if (IsBusy || HasPendingDate || Editor?.IsReadOnly != true) return;
        var editor = Editor; _writing = true; Notify();
        Report("Starting a new Draft from the preserved Ready revision…", PlanNoticeKind.Info);
        using var cancellation = new CancellationTokenSource(); _write = cancellation;
        try
        {
            string? reviewed = null;
            var current = editor.Key.AccountId is { } accountId ? await Task.Run(() => _accounts.GetByIdAsync(accountId, cancellation.Token), cancellation.Token) : null;
            var saved = editor.CurrentPlan.LatestReadyRevision!.References.Account.Currency;
            if (editor.HasMonetaryLimits && current is not null && current.Currency != saved)
            {
                Report("The Account currency changed. Saved limits keep their original currency; correct the Draft currency and amounts before Ready.", PlanNoticeKind.Warning);
            }
            var result = await editor.StartDraftAsync(reviewed, cancellation.Token);
            if (result.Status == TradingPlanWriteStatus.Success)
            {
                ReplaceEditor(CreateEditor(result.Plan!)); await LoadSourcesAsync(Editor!, CancellationToken.None);
                Report("New working Draft saved. The source Ready revision is preserved.", PlanNoticeKind.Warning);
            }
        }
        catch (OperationCanceledException) { Report("Start Draft cancelled. No success was reported.", PlanNoticeKind.Info); }
        catch (Exception) { Report("Draft transition could not be verified. Ready context is retained; Reload to check stored state.", PlanNoticeKind.Error); }
        finally { _write = null; _writing = false; Notify(); }
    }

    public async Task DeletePlanAsync()
    {
        if (!HasEditor || IsBusy || _guarding || _disposed) return;
        var editor = Editor!; var target = editor.CurrentPlan;
        _deleteCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // Deliberately bypass Save/Discard: the explicit destructive confirmation covers the draft.
        _writing = true; Notify();
        using var cancellation = new CancellationTokenSource(); _write = cancellation;
        try
        {
            if (!_interaction.ConfirmDelete(Format(target.Key.NewYorkDate), ContextLabel)) return;
            if (!ReferenceEquals(Editor, editor)) return;
            var result = await Task.Run(() => _store.WriteAsync(new(TradingPlanWriteOperation.Delete,
                target.Key, target.WorkingDraft?.Content ?? target.LatestReadyRevision!.Content,
                new(target.Id, target.Version)), cancellation.Token), cancellation.Token);
            if (result.Status == TradingPlanWriteStatus.Success && result.DeletedPlanId == target.Id)
            {
                ReplaceEditor(null); ClearLeaveConsent(); _contextLoaded = true; Report("");
                PublishPlans([]); ClearHistorical(); _historyPlan = null; RevisionRows = []; NotifyHistory();
            }
            else Report(result.Status == TradingPlanWriteStatus.Conflict
                ? "The Plan changed elsewhere. Deletion was rejected; your entered input is retained. Reload after preserving it."
                : "The Plan could not be deleted. Your input and saved history are retained.", PlanNotice.ForWrite(result.Status));
        }
        catch (OperationCanceledException) { Report("Deletion cancelled. Preparation retained."); }
        catch (Exception) { Report("Deletion could not be verified. Input retained; reload to check stored state.", PlanNoticeKind.Error); }
        finally { _write = null; _writing = false; _deleteCompletion.TrySetResult(); Notify(); }
    }

    public async Task CancelAsync()
    {
        var principlesCancellation = Principles.CancelAsync();
        _load?.Cancel(); _write?.Cancel(); _historyLoad?.Cancel(); Editor?.CancelOperation();
        await principlesCancellation;
        Task[] loads; lock (_loadSync) loads = _loads.ToArray();
        await Task.WhenAll(loads);
        if (_deleteCompletion is { } deletion) await deletion.Task;
        if (Editor is { } editor) await editor.OperationCompletion;
        if (_historyWriteTask is { } write) await write;
    }
    public bool NeedsClosePreparation => Principles.IsBusy || IsBusy || HasOutstandingLoads || Editor?.IsDirty == true;
    public bool HasLeaveConsent => !Principles.IsBusy && !IsBusy && !HasOutstandingLoads && (Editor?.IsDirty != true || _acceptedIdentity == Editor.EditIdentity);
    public async Task<bool> PrepareLeaveAsync()
    {
        await Principles.CancelAsync();
        if (_guarding || _writing || Editor?.IsBusy == true)
        { Report("Wait for the Trading Plan operation, or cancel it before leaving.", PlanNoticeKind.Info); return false; }
        _guarding = true; Notify();
        try
        {
            if (_loading || HasOutstandingLoads) await CancelAsync();
            ClearLeaveConsent();
            if (Editor?.IsDirty != true) return true;
            var choice = _interaction.ConfirmLeave();
            if (choice == PlanLeaveChoice.Cancel) { Report("Continue editing. Unsaved preparation is retained.", PlanNoticeKind.Info); return false; }
            if (choice == PlanLeaveChoice.Save)
            {
                if (HasDateError) { await InvalidDateAsync(); return false; }
                // Save the still-selected identity, not a typed destination date awaiting consent.
                if (await Editor.SaveAsync() != TradingPlanWriteStatus.Success) return false;
            }
            _discardAccepted = choice == PlanLeaveChoice.Discard;
            _acceptedIdentity = Editor.EditIdentity; return true;
        }
        finally { _guarding = false; Notify(); }
    }
    public void CompleteLeave()
    {
        if (_discardAccepted && HasLeaveConsent) { ReplaceEditor(null); _contextLoaded = false; }
        ClearLeaveConsent();
    }
    public void ClearLeaveConsent() { _discardAccepted = false; _acceptedIdentity = null; }
    private void Notify()
    {
        foreach (string name in new[] { nameof(HasEditor), nameof(IsBusy), nameof(IsLoading), nameof(CanSelectContext), nameof(CanSave), nameof(CanCreateDraft), nameof(ShowEmpty), nameof(EmptyMessage), nameof(ContextLabel), nameof(StateLabel), nameof(HasPendingDate), nameof(CanInteractEditor), nameof(ShowCurrencyField) }) OnPropertyChanged(name);
        foreach (var command in new[] { PreviousDayCommand, NextDayCommand, TodayCommand, OpenDateCommand, ReloadCommand, CreateDraftCommand, SaveCommand, MarkReadyCommand, StartDraftCommand, DeletePlanCommand, CancelCommand }) command?.NotifyCanExecuteChanged();
        NotifyHistory();
    }
    public void Dispose()
    { _disposed = true; Principles.Dispose(); ++_epoch; _load?.Cancel(); _write?.Cancel(); _historyLoad?.Cancel(); HistoricalViewer?.Dispose(); ReplaceEditor(null); }
}
