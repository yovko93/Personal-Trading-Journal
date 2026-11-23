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
    public ReadOnlyObservableCollection<ScenarioEditor> Scenarios { get; private set; } = null!;
    public PreparationField StandAside { get; private set; } = null!;
    public IRelayCommand AddScenarioCommand { get; private set; } = null!;
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
        ? (_scenarios.Count == 0 ? "Intentional stand-aside plan. Explain the no-trade decision below; no scenario is required."
            : "Stand aside selected. Existing scenarios are retained in this Draft. Remove them explicitly before marking Ready.")
        : "Conditional scenarios and ordinary no-trade conditions may coexist. Scenario text is optional; blank text is unspecified, not completed market evidence.";
    public PreparationField? FirstInvalidField => Fields.FirstOrDefault(f => f.IsInvalid);

    private void InitializeScenarios()
    {
        _intent = _original.Intent;
        StandAside = ScenarioField("Stand Aside — no-trade conditions for this plan", _original.StandAside);
        Scenarios = new(_scenarios);
        foreach (var scenario in _original.Scenarios) _scenarios.Add(new(this, scenario));
        AddScenarioCommand = new RelayCommand(AddScenario, () => CanEdit && _contexts.Count > 0 && _scenarios.Count < TradingPlanContent.MaximumScenarios);
    }
    internal PreparationField ScenarioField(string label, string text, int limit = TradingPlanContent.MaximumTextLength) =>
        new(label, text, limit, false, _culture, () => CanEdit, Changed);
    internal string? SavedScenarioGuidance(TradingPlanSetupLink? link)
    {
        if (!IsReadOnly || link is null) return null;
        var saved = (_historical ?? _plan.LatestReadyRevision)?.References.Setups.SingleOrDefault(s => s.SetupId == link.SetupId);
        return saved is null ? null :
            $"{saved.Name} · saved guidance (revision {saved.Revision})\nMarket context\n{saved.MarketContext}\n\nEntry conditions\n{saved.EntryConditions}\n\nConfirmations\n{saved.Confirmations}\n\nInvalidation\n{saved.Invalidation}\n\nExecution guidance\n{saved.ExecutionGuidance}";
    }

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
        AddScenarioCommand?.NotifyCanExecuteChanged();
        foreach (var row in _scenarios) row.NotifyAccess();
    }
}
