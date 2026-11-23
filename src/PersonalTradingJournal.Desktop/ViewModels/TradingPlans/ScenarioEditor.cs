using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

/// <summary>Transient fields owned by the same session as preparation, not another Draft.</summary>
public sealed class ScenarioEditor : ObservableObject
{
    private readonly SessionPreparationViewModel _owner;
    private Guid _instrumentId;
    private TradingPlanScenarioRole _role;
    private TradeDirection? _direction;
    internal ScenarioEditor(SessionPreparationViewModel owner, TradingPlanScenario content)
    {
        SavedGuidance = owner.SavedScenarioGuidance(content.Setup);
        _owner = owner; Id = content.Id; _instrumentId = content.InstrumentId;
        _role = content.Role; _direction = content.Direction;
        Name = owner.ScenarioField("Scenario name", content.Name, 120);
        Condition = owner.ScenarioField("IF — observable condition", content.Condition);
        Response = owner.ScenarioField("THEN — intended response", content.Response);
        Invalidation = owner.ScenarioField("INVALID IF — cancellation condition", content.Invalidation);
        RemoveCommand = new RelayCommand(() => owner.RemoveScenario(this), () => owner.CanEdit);
        ToggleRoleCommand = new RelayCommand(() => Role = Role == TradingPlanScenarioRole.Primary ? TradingPlanScenarioRole.Alternative : TradingPlanScenarioRole.Primary, () => owner.CanEdit);
        ToggleDirectionCommand = new RelayCommand(() => Direction = Direction == TradeDirection.Long ? TradeDirection.Short : TradeDirection.Long, () => owner.CanEdit);
        MoveUpCommand = new RelayCommand(() => owner.MoveScenario(this, -1), () => owner.CanMoveScenario(this, -1));
        MoveDownCommand = new RelayCommand(() => owner.MoveScenario(this, 1), () => owner.CanMoveScenario(this, 1));
    }
    public Guid Id { get; }
    public string? SavedGuidance { get; }
    public bool HasSavedGuidance => SavedGuidance is not null;
    public SessionPreparationViewModel Owner => _owner;
    public PreparationField Name { get; }
    public PreparationField Condition { get; }
    public PreparationField Response { get; }
    public PreparationField Invalidation { get; }
    public Guid InstrumentId
    {
        get => _instrumentId;
        set { if (_owner.CanEdit && _owner.Contexts.Any(c => c.InstrumentId == value) && SetProperty(ref _instrumentId, value)) _owner.ScenarioChanged(); }
    }
    public TradingPlanScenarioRole Role
    {
        get => _role;
        set { if (_owner.CanEdit && Enum.IsDefined(value) && SetProperty(ref _role, value)) { OnPropertyChanged(nameof(RoleAction)); _owner.ScenarioChanged(); } }
    }
    public TradeDirection? Direction
    {
        get => _direction;
        set
        {
            if (_owner.CanEdit && (!value.HasValue || Enum.IsDefined(value.Value)) && SetProperty(ref _direction, value))
            { OnPropertyChanged(nameof(Heading)); OnPropertyChanged(nameof(DirectionLabel)); OnPropertyChanged(nameof(DirectionAction)); _owner.ScenarioChanged(); }
        }
    }
    public string Heading => Direction is { } d ? $"{d} Scenario" : "Scenario · direction not selected";
    public string RoleAction => $"{Role}. Switch to {(Role == TradingPlanScenarioRole.Primary ? "Alternative" : "Primary")}.";
    public string DirectionLabel => Direction switch { TradeDirection.Long => "↑ Long", TradeDirection.Short => "↓ Short", _ => "Choose direction" };
    public string DirectionAction => $"{DirectionLabel}. Switch to {(Direction == TradeDirection.Long ? "Short" : "Long")}.";
    public IRelayCommand ToggleRoleCommand { get; }
    public IRelayCommand ToggleDirectionCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    internal IEnumerable<PreparationField> Fields => [Name, Condition, Response, Invalidation];
    internal object RawState => new { Id, InstrumentId, Name = Name.Text, Role, Direction, Condition = Condition.Text,
        Response = Response.Text, Invalidation = Invalidation.Text };
    internal TradingPlanScenario Build() => new(Id, InstrumentId, Name.Text, Role, Direction, Condition.Text, Response.Text, Invalidation.Text);
    internal void NotifyAccess()
    {
        ToggleRoleCommand.NotifyCanExecuteChanged(); ToggleDirectionCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged(); MoveDownCommand.NotifyCanExecuteChanged(); RemoveCommand.NotifyCanExecuteChanged();
    }
}
