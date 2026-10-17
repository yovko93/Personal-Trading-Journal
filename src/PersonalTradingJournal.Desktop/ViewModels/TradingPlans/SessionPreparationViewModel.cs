using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

/// <summary>One host-owned editing session for an exact Plan. No automatic saving/navigation.
/// The M21.7 host owns scope changes and guards using IsDirty/IsBusy, and explicitly disposes it.</summary>
public sealed partial class SessionPreparationViewModel : ObservableObject, IDisposable
{
    private readonly ITradingPlanStore _store;
    private readonly IInstrumentReader _catalog;
    private readonly CultureInfo _culture;
    private readonly ObservableCollection<InstrumentPreparationEditor> _contexts = [];
    private TradingPlan _plan;
    private TradingPlanContent _original;
    private readonly TradingPlanReadyRevision? _historical;
    private TradingPlanSession? _session;
    private InstrumentPreparationEditor? _selected;
    private InstrumentListItem? _toAdd;
    private IReadOnlyList<InstrumentListItem> _available = [];
    private string _baseline = "";
    private bool _busy, _disposed;
    private string _message = "";
    private CancellationTokenSource? _operation;

    public SessionPreparationViewModel(TradingPlan plan, ITradingPlanStore store, IInstrumentReader catalog,
        CultureInfo? culture = null, TradingPlanReadyRevision? historical = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (historical is not null && (historical.Id.PlanId != plan.Id || historical.Key != plan.Key))
            throw new ArgumentException("Historical revision must belong to this Plan.", nameof(historical));
        _plan = TradingPlan.Rehydrate(plan.Id, plan.Key, plan.Version, plan.WorkingDraft, plan.LatestReadyRevision,
            plan.CreatedAtUtc, plan.UpdatedAtUtc);
        _store = store; _catalog = catalog; _historical = historical;
        _culture = CultureInfo.ReadOnly((CultureInfo)(culture ?? CultureInfo.CurrentCulture).Clone());
        _original = historical?.Content ?? plan.WorkingDraft?.Content ?? plan.LatestReadyRevision!.Content;
        _session = _original.Session;
        Focus = new("Today's Focus", _original.Focus, TradingPlanContent.MaximumFocusLength, false, _culture, () => CanEdit, Changed);
        News = new("News & Restricted Times — manual notes", _original.NewsAndRestrictions, TradingPlanContent.MaximumTextLength, false, _culture, () => CanEdit, Changed);
        Contexts = new(_contexts);
        foreach (var context in _original.Instruments) _contexts.Add(CreateEditor(context));
        _selected = _contexts.FirstOrDefault();
        InitializeScenarios();
        _baseline = Fingerprint();
        AddInstrumentCommand = new RelayCommand(AddInstrument, () => CanEdit && InstrumentToAdd is { IsActive: true });
        RemoveInstrumentCommand = new RelayCommand(RemoveInstrument, () => CanEdit && SelectedInstrument is not null);
        SaveCommand = new AsyncRelayCommand(ct => SaveAsync(ct), () => CanEdit);
        RefreshReferencesCommand = new AsyncRelayCommand(ct => RefreshReferencesAsync(ct), () => CanEdit);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => IsBusy);
    }

    public TradingPlanKey Key => _plan.Key;
    public TradingPlanExpectedState Token => new(_plan.Id, _plan.Version);
    public bool IsReadOnly => _historical is not null || _plan.WorkingDraft is null;
    public bool IsBusy => _busy;
    public bool CanEdit => !IsReadOnly && !IsBusy && !_disposed;
    public bool CanSelectContext => !IsBusy && !_disposed;
    public bool HasSelectedContext => SelectedInstrument is not null;
    public bool HasNoContext => !HasSelectedContext;
    public bool IsDirty => !IsReadOnly && Fingerprint() != _baseline;
    public bool HasValidationErrors => Fields.Any(f => f.IsInvalid);
    public string StateLabel => IsReadOnly ? $"Ready revision {(_historical ?? _plan.LatestReadyRevision)!.Id.Revision} · read-only" : "Working Draft · changes are saved explicitly";
    public PreparationField Focus { get; }
    public PreparationField News { get; }
    public ReadOnlyObservableCollection<InstrumentPreparationEditor> Contexts { get; }
    public IReadOnlyList<InstrumentListItem> AvailableInstruments { get => _available; private set => SetProperty(ref _available, value); }
    public InstrumentListItem? InstrumentToAdd { get => _toAdd; set { if (CanEdit && SetProperty(ref _toAdd, value)) AddInstrumentCommand.NotifyCanExecuteChanged(); } }
    public InstrumentPreparationEditor? SelectedInstrument
    {
        get => _selected;
        set
        {
            if (CanSelectContext && (value is null || _contexts.Contains(value)) && SetProperty(ref _selected, value))
            {
                RemoveInstrumentCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(HasSelectedContext)); OnPropertyChanged(nameof(HasNoContext));
            }
        }
    }
    public TradingPlanSession? Session { get => _session; set { if (CanEdit && (!value.HasValue || Enum.IsDefined(value.Value)) && SetProperty(ref _session, value)) Changed(); } }
    public IReadOnlyList<TradingPlanSession?> Sessions { get; } = new TradingPlanSession?[] { null }.Concat(Enum.GetValues<TradingPlanSession>().Select(v => (TradingPlanSession?)v)).ToArray();
    public IReadOnlyList<TradingPlanBias?> Biases { get; } = new TradingPlanBias?[] { null }.Concat(Enum.GetValues<TradingPlanBias>().Select(v => (TradingPlanBias?)v)).ToArray();
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public IRelayCommand AddInstrumentCommand { get; }
    public IRelayCommand RemoveInstrumentCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshReferencesCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public int ValidationAttempt { get; private set; }
    public TradingPlanChecklist Confirmed
    {
        get
        {
            // Preview exactly the Domain mutation, without mutating the loaded save token.
            if (IsReadOnly || !TryBuildContent(out var content)) return _original.Confirmed;
            var copy = TradingPlan.Rehydrate(_plan.Id, _plan.Key, _plan.Version, _plan.WorkingDraft,
                _plan.LatestReadyRevision, _plan.CreatedAtUtc, _plan.UpdatedAtUtc);
            copy.EditDraft(copy.Version, content!, copy.UpdatedAtUtc);
            return copy.WorkingDraft!.Content.Confirmed;
        }
    }
    private IEnumerable<PreparationField> Fields => new[] { Focus }.Concat(_contexts.SelectMany(c => c.Fields))
        .Append(News).Concat(_scenarios.SelectMany(s => s.Fields)).Append(StandAside);

    public async Task LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || _disposed) return;
        using var operation = Begin(cancellationToken);
        try
        {
            var items = await Task.Run(() => _catalog.GetAllAsync(operation.Token), operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            AvailableInstruments = items.Where(i => i.IsActive).OrderBy(i => i.Symbol, StringComparer.Ordinal).ThenBy(i => i.Id).ToArray();
            foreach (var context in _contexts) context.Label = Label(context.InstrumentId, items);
        }
        catch (OperationCanceledException) { Message = "Instrument loading cancelled. Entered preparation is unchanged."; }
        catch (Exception) { Message = "Instrument catalog could not be loaded. Entered preparation is unchanged; try again."; }
        finally { End(); }
    }

    public bool TryBuildContent(out TradingPlanContent? content)
    {
        content = null;
        if (HasValidationErrors) return false;
        content = new(Session, Focus.Text, Intent, _contexts.Select(c => c.Build()),
            _scenarios.Select(s => s.Build()), News.Text, StandAside.Text, _original.RiskLimits, _original.Rules, _original.Confirmed);
        return true;
    }

    public Task<TradingPlanWriteStatus> SaveAsync(CancellationToken ct = default) => WriteAsync(TradingPlanWriteOperation.SaveDraft, ct);
    public Task<TradingPlanWriteStatus> RefreshReferencesAsync(CancellationToken ct = default) => WriteAsync(TradingPlanWriteOperation.RefreshReferences, ct);

    private async Task<TradingPlanWriteStatus> WriteAsync(TradingPlanWriteOperation operationKind, CancellationToken ct)
    {
        if (!CanEdit) return TradingPlanWriteStatus.Invalid;
        if (!TryBuildContent(out var content))
        {
            Message = "Correct the highlighted input before saving. All entered text is retained.";
            ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt)); return TradingPlanWriteStatus.Invalid;
        }
        var command = new TradingPlanWriteCommand(operationKind, Key, content!, Token);
        using var operation = Begin(ct);
        try
        {
            var result = await Task.Run(() => _store.WriteAsync(command, operation.Token), operation.Token);
            // A committed success remains success even if cancellation arrives after commit.
            if (result.Status == TradingPlanWriteStatus.Success && result.Plan is { WorkingDraft: not null } saved &&
                saved.Id == _plan.Id && saved.Key == Key && saved.Version >= _plan.Version)
            {
                _plan = saved; _original = saved.WorkingDraft.Content; _baseline = Fingerprint();
                Message = operationKind == TradingPlanWriteOperation.RefreshReferences
                    ? "References refreshed and Draft saved. All checklist confirmations must be made again."
                    : "Draft saved and verified.";
                return TradingPlanWriteStatus.Success;
            }
            var status = result.Status == TradingPlanWriteStatus.Success ? TradingPlanWriteStatus.StorageFailure : result.Status;
            Message = status switch
            {
                TradingPlanWriteStatus.Conflict => "This Plan changed elsewhere. Keep your input and resolve the newer Draft before saving.",
                TradingPlanWriteStatus.ReferenceChanged => "A selected reference changed. Review it, then explicitly refresh references and reconfirm the checklist. Input is retained.",
                TradingPlanWriteStatus.ReferenceUnavailable => "A selected reference is unavailable or inactive. Restore it or explicitly remove/relink it; input is retained.",
                TradingPlanWriteStatus.Cancelled => "Save cancelled. Entered preparation is retained.",
                TradingPlanWriteStatus.Invalid => "Preparation could not be validated. Check the entered fields; input is retained.",
                _ => "Draft could not be saved and verified. Entered preparation is retained; try again."
            };
            return status;
        }
        catch (OperationCanceledException) { Message = "Save cancelled. Entered preparation is retained."; return TradingPlanWriteStatus.Cancelled; }
        catch (Exception) { Message = "Draft could not be saved and verified. Entered preparation is retained; try again."; return TradingPlanWriteStatus.StorageFailure; }
        finally { End(); Changed(); }
    }

    private void AddInstrument()
    {
        if (!CanEdit || InstrumentToAdd is not { IsActive: true } item || !AvailableInstruments.Contains(item)) return;
        if (_contexts.Any(c => c.InstrumentId == item.Id)) { Message = "This instrument already has a context. Select it to continue editing."; return; }
        if (_contexts.Count >= TradingPlanContent.MaximumInstruments) { Message = $"Use at most {TradingPlanContent.MaximumInstruments} instruments."; return; }
        var editor = CreateEditor(new(item.Id)); editor.Label = Label(item.Id, AvailableInstruments);
        _contexts.Add(editor); SelectedInstrument = editor; Changed();
    }
    private void RemoveInstrument()
    {
        if (!CanEdit || SelectedInstrument is not { } selected) return;
        if (_scenarios.Any(s => s.InstrumentId == selected.InstrumentId))
        { Message = "This instrument owns scenarios. Remove or reassign those scenarios first; no content was discarded."; return; }
        _contexts.Remove(selected); SelectedInstrument = _contexts.FirstOrDefault(); Changed();
    }
    private InstrumentPreparationEditor CreateEditor(TradingPlanInstrumentContext content) =>
        new(content, Label(content.InstrumentId, AvailableInstruments), _culture, () => CanEdit, Changed);
    private string Label(Guid id, IReadOnlyList<InstrumentListItem> catalog)
    {
        var frozen = (_historical ?? (IsReadOnly ? _plan.LatestReadyRevision : null))?.References.Instruments.FirstOrDefault(i => i.InstrumentId == id);
        if (frozen is not null) return $"{frozen.Symbol} · {frozen.DisplayName} · saved context";
        var current = catalog.FirstOrDefault(i => i.Id == id);
        return current is null ? $"Unavailable instrument ({id})" : $"{current.Symbol} · {current.DisplayName}" + (current.IsActive ? "" : " · inactive");
    }
    private string Fingerprint() => System.Text.Json.JsonSerializer.Serialize(new
    {
        Session, Focus = Focus.Text, News = News.Text, Intent, StandAside = StandAside.Text,
        Scenarios = _scenarios.Select(s => s.RawState),
        Instruments = _contexts.Select(c => new { c.InstrumentId, c.Bias, Thesis = c.Thesis.Text, Levels = c.Levels.Select(f => f.Text) })
    });
    private CancellationTokenSource Begin(CancellationToken ct)
    { _operation = CancellationTokenSource.CreateLinkedTokenSource(ct); _busy = true; NotifyAccess(); return _operation; }
    private void End() { _operation = null; _busy = false; NotifyAccess(); }
    private void NotifyAccess()
    {
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanSelectContext));
        foreach (var field in Fields) field.NotifyAccess();
        NotifyScenarioAccess();
        AddInstrumentCommand.NotifyCanExecuteChanged(); RemoveInstrumentCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged(); RefreshReferencesCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
    }
    private void Changed()
    {
        OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(HasValidationErrors)); OnPropertyChanged(nameof(Confirmed)); OnPropertyChanged(nameof(Token));
        NotifyScenarioAccess();
    }
    public void Dispose() { _disposed = true; _operation?.Cancel(); NotifyAccess(); }
}
