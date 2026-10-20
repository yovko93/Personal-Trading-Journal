using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed partial class SessionPreparationViewModel
{
    private readonly ObservableCollection<ScenarioEditor> _scenarios = [];
    private TradingPlanIntent _intent;
    private ITradingSetupReader? _setups;
    private ITradingSetupPlaybookStore? _playbooks;
    private ITradingSetupPlaybookHistoryReader? _playbookHistory;
    private IReadOnlyList<TradingSetupListItem> _availablePlaybooks = [];
    private Func<Guid, CancellationToken, Task<bool>>? _navigation;
    public ReadOnlyObservableCollection<ScenarioEditor> Scenarios { get; private set; } = null!;
    public PreparationField StandAside { get; private set; } = null!;
    public IRelayCommand AddScenarioCommand { get; private set; } = null!;
    public IAsyncRelayCommand ReloadPlaybooksCommand { get; private set; } = null!;
    public bool HasNoScenarios => _scenarios.Count == 0;
    public IReadOnlyList<TradingPlanScenarioRole> ScenarioRoles { get; } = Enum.GetValues<TradingPlanScenarioRole>();
    public IReadOnlyList<TradeDirection?> ScenarioDirections { get; } = [null, TradeDirection.Long, TradeDirection.Short];
    public TradingPlanIntent Intent
    {
        get => _intent;
        set
        {
            if (!CanEdit || !Enum.IsDefined(value) || !SetProperty(ref _intent, value)) return;
            OnPropertyChanged(nameof(IsStandAside)); OnPropertyChanged(nameof(IntentMessage)); Changed();
        }
    }
    public bool IsStandAside { get => Intent == TradingPlanIntent.StandAside; set => Intent = value ? TradingPlanIntent.StandAside : TradingPlanIntent.Opportunities; }
    public string IntentMessage => IsStandAside
        ? (_scenarios.Count == 0 ? "Intentional stand-aside plan. Explain the no-trade decision below; no scenario or Playbook is required."
            : "Stand aside selected. Existing scenarios are retained in this Draft. Remove them explicitly before marking Ready.")
        : "Conditional scenarios and ordinary no-trade conditions may coexist. Scenario text and Playbook links are optional; blank text is unspecified, not completed market evidence.";
    public IReadOnlyList<TradingSetupListItem> AvailablePlaybooks { get => _availablePlaybooks; private set => SetProperty(ref _availablePlaybooks, value); }
    /// <summary>The host routes exact Setup identity through its existing guarded navigation. False means veto/unavailable.
    /// This component neither closes itself nor discards/saves input during a request.</summary>
    public Func<Guid, CancellationToken, Task<bool>>? PlaybookNavigation
    { get => _navigation; set { _navigation = value; NotifyScenarioAccess(); } }
    public PreparationField? FirstInvalidField => Fields.FirstOrDefault(f => f.IsInvalid);

    private void InitializeScenarios()
    {
        _intent = _original.Intent;
        StandAside = ScenarioField("Stand Aside — no-trade conditions for this plan", _original.StandAside);
        Scenarios = new(_scenarios);
        foreach (var scenario in _original.Scenarios) _scenarios.Add(new(this, scenario));
        AddScenarioCommand = new RelayCommand(AddScenario, () => CanEdit && _contexts.Count > 0 && _scenarios.Count < TradingPlanContent.MaximumScenarios);
        ReloadPlaybooksCommand = new AsyncRelayCommand(LoadPlaybooksAsync, () => !IsBusy && !_disposed && _setups is not null);
    }
    internal PreparationField ScenarioField(string label, string text, int limit = TradingPlanContent.MaximumTextLength) =>
        new(label, text, limit, false, _culture, () => CanEdit, Changed);
    internal void ScenarioChanged() => Changed();
    private void AddScenario()
    {
        if (!AddScenarioCommand.CanExecute(null)) return;
        var instrument = SelectedInstrument ?? _contexts[0];
        _scenarios.Add(new(this, new(Guid.NewGuid(), instrument.InstrumentId,
            role: _scenarios.Count == 0 ? TradingPlanScenarioRole.Primary : TradingPlanScenarioRole.Alternative, direction: TradeDirection.Long)));
        ScenarioListChanged();
    }
    internal bool CanMoveScenario(ScenarioEditor row, int delta) => CanEdit && _scenarios.Contains(row) && _scenarios.IndexOf(row) + delta >= 0 && _scenarios.IndexOf(row) + delta < _scenarios.Count;
    internal void MoveScenario(ScenarioEditor row, int delta)
    { if (!CanMoveScenario(row, delta)) return; int index = _scenarios.IndexOf(row); _scenarios.Move(index, index + delta); ScenarioListChanged(); }
    internal void RemoveScenario(ScenarioEditor row)
    { if (CanEdit && _scenarios.Remove(row)) ScenarioListChanged(); }
    private void ScenarioListChanged()
    { OnPropertyChanged(nameof(HasNoScenarios)); OnPropertyChanged(nameof(IntentMessage)); Changed(); }
    private void NotifyScenarioAccess()
    {
        AddScenarioCommand?.NotifyCanExecuteChanged(); ReloadPlaybooksCommand?.NotifyCanExecuteChanged();
        foreach (var row in _scenarios) row.NotifyAccess();
    }

    /// <summary>Reuse existing catalogs; configuring does not query them or modify the Draft.</summary>
    public void ConfigurePlaybooks(ITradingSetupReader setups, ITradingSetupPlaybookStore playbooks, ITradingSetupPlaybookHistoryReader history)
    { _setups = setups; _playbooks = playbooks; _playbookHistory = history; NotifyScenarioAccess(); }

    public async Task LoadPlaybooksAsync(CancellationToken ct = default)
    {
        if (IsBusy || _disposed || _setups is null || _playbooks is null || _playbookHistory is null) return;
        using var operation = Begin(ct);
        try
        {
            var items = await Task.Run(() => _setups.GetAllAsync(operation.Token), operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            AvailablePlaybooks = items.Where(s => s.IsActive).OrderBy(s => s.Name, StringComparer.Ordinal).ThenBy(s => s.Id).ToArray();
            // At most 24 selected references; do not load guidance for the whole catalog.
            foreach (var group in _scenarios.Where(s => s.Setup is not null).GroupBy(s => s.Setup!))
            {
                var link = group.Key;
                var live = items.SingleOrDefault(s => s.Id == link.SetupId);
                var latest = live is null ? null : await Task.Run(() => _playbooks.GetLatestAsync(link.SetupId, operation.Token), operation.Token);
                var exact = IsReadOnly ? null : latest?.Revision == link.ExpectedPlaybookRevision ? latest :
                    await Task.Run(() => _playbookHistory.GetRevisionAsync(link.SetupId, link.ExpectedPlaybookRevision, operation.Token), operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                foreach (var row in group)
                {
                    string status = live is null ? "Current Setup unavailable." : !live.IsActive ? "Current Setup archived; new links are not permitted." :
                        latest?.Revision != link.ExpectedPlaybookRevision || live.UpdatedAtUtc != link.ExpectedUpdatedAtUtc
                        ? (IsReadOnly ? "Current Setup changed. The saved context remains unchanged. Start a Draft to change preparation."
                            : "Current Setup changed. Explicitly select its current definition, then refresh references.") : "Selected reference is current.";
                    if (IsReadOnly) row.Present(FrozenText(link.SetupId) + "\n\n" + status, live is not null);
                    else row.Present(exact is null ? "Previously selected Playbook content is no longer current or available. Relink or remove it; scenario input is retained.\n" + status :
                        $"{exact.SetupName} · selected Playbook\n{Guidance(exact.Guidance)}\n\n{status}", live is not null);
                }
            }
        }
        catch (OperationCanceledException) { Report("Playbook loading cancelled. Draft links and input are unchanged.", PlanNoticeKind.Info); }
        catch (Exception) { Report("Playbooks could not be loaded. Draft links and input are unchanged; try again.", PlanNoticeKind.Error); }
        finally { End(); }
    }

    internal async Task LinkPlaybookAsync(ScenarioEditor row, CancellationToken ct)
    {
        if (!CanEdit || !_scenarios.Contains(row) || row.Candidate is not { } candidate || !AvailablePlaybooks.Contains(candidate) || _setups is null || _playbooks is null) return;
        using var operation = Begin(ct);
        try
        {
            var live = await Task.Run(() => _setups.GetByIdAsync(candidate.Id, operation.Token), operation.Token);
            var revision = await Task.Run(() => _playbooks.GetLatestAsync(candidate.Id, operation.Token), operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (live is not { IsActive: true } || revision is null || revision.TradingSetupId != candidate.Id)
            { Report("Choose an active Setup with saved Playbook guidance. The previous link and scenario input are retained.", PlanNoticeKind.Info); return; }
            row.SetLink(new(live.Id, revision.Revision, live.UpdatedAtUtc));
            row.Present($"{live.Name} · current Playbook\n{Guidance(revision.Guidance)}\n\nAssociation is not verified adherence.", true);
            Report("Playbook selected for this Draft. Save rechecks its current content; changed references require explicit refresh.", PlanNoticeKind.Warning);
        }
        catch (OperationCanceledException) { Report("Playbook selection cancelled. Previous link and input retained.", PlanNoticeKind.Info); }
        catch (Exception) { Report("Playbook selection failed. Previous link and input retained; try again.", PlanNoticeKind.Error); }
        finally { End(); }
    }
    internal async Task RequestPlaybookAsync(ScenarioEditor row, CancellationToken ct)
    {
        if (IsBusy || _disposed || !_scenarios.Contains(row) || row.Setup is not { } link || PlaybookNavigation is not { } request) return;
        try
        {
            if (!await request(link.SetupId, ct)) Report("Playbook navigation was declined or the source is unavailable. Draft input is retained.", PlanNoticeKind.Error);
        }
        catch (OperationCanceledException) { Report("Navigation cancelled. Draft input is retained.", PlanNoticeKind.Info); }
        catch (Exception) { Report("The linked Playbook could not be opened. Draft input and saved context are retained.", PlanNoticeKind.Error); }
    }
    internal void SetFrozenPlaybook(ScenarioEditor row)
    {
        if (row.Setup is { } link) row.Present(IsReadOnly ? FrozenText(link.SetupId) :
            $"Selected Playbook. Load Playbooks to inspect criteria and current availability.", false);
    }
    private string FrozenText(Guid id)
    {
        var saved = (_historical ?? _plan.LatestReadyRevision)?.References.Setups.SingleOrDefault(s => s.SetupId == id);
        return saved is null ? "Saved Playbook context unavailable." :
            $"{saved.Name} · saved Plan-owned context · read-only\nMarket context\n{saved.MarketContext}\n\nEntry conditions\n{saved.EntryConditions}\n\nConfirmations\n{saved.Confirmations}\n\nInvalidation\n{saved.Invalidation}\n\nExecution guidance\n{saved.ExecutionGuidance}\n\nSaved context, not proof of adherence. Live navigation availability is checked separately.";
    }
    private static string Guidance(PersonalTradingJournal.Domain.Setups.PlaybookGuidance g) =>
        $"Market context\n{g.MarketContext}\n\nEntry conditions\n{g.EntryConditions}\n\nConfirmations\n{g.Confirmations}\n\nInvalidation\n{g.Invalidation}\n\nExecution guidance\n{g.ExecutionGuidance}";
}
