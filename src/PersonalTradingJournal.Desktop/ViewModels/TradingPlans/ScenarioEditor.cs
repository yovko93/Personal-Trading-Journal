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
    private TradingPlanSetupLink? _setup;
    private TradingSetupListItem? _candidate;
    private string _playbookText = "No Playbook linked. A link is optional and does not prove adherence.";
    private bool _canNavigate;

    internal ScenarioEditor(SessionPreparationViewModel owner, TradingPlanScenario content)
    {
        _owner = owner; Id = content.Id; _instrumentId = content.InstrumentId;
        _role = content.Role; _direction = content.Direction; _setup = content.Setup;
        Name = owner.ScenarioField("Scenario name", content.Name, 120);
        Condition = owner.ScenarioField("IF — observable condition", content.Condition);
        Response = owner.ScenarioField("THEN — intended response", content.Response);
        Invalidation = owner.ScenarioField("INVALID IF — cancellation condition", content.Invalidation);
        RemoveCommand = new RelayCommand(() => owner.RemoveScenario(this), () => owner.CanEdit);
        MoveUpCommand = new RelayCommand(() => owner.MoveScenario(this, -1), () => owner.CanMoveScenario(this, -1));
        MoveDownCommand = new RelayCommand(() => owner.MoveScenario(this, 1), () => owner.CanMoveScenario(this, 1));
        LinkCommand = new AsyncRelayCommand(ct => owner.LinkPlaybookAsync(this, ct),
            () => owner.CanEdit && Candidate is not null && owner.AvailablePlaybooks.Contains(Candidate));
        UnlinkCommand = new RelayCommand(() => { if (owner.CanEdit) SetLink(null); }, () => owner.CanEdit && Setup is not null);
        OpenPlaybookCommand = new AsyncRelayCommand(ct => owner.RequestPlaybookAsync(this, ct),
            () => !owner.IsBusy && _canNavigate && owner.PlaybookNavigation is not null);
        owner.SetFrozenPlaybook(this);
    }
    public Guid Id { get; }
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
        set { if (_owner.CanEdit && Enum.IsDefined(value) && SetProperty(ref _role, value)) _owner.ScenarioChanged(); }
    }
    public TradeDirection? Direction
    {
        get => _direction;
        set
        {
            if (_owner.CanEdit && (!value.HasValue || Enum.IsDefined(value.Value)) && SetProperty(ref _direction, value))
            { OnPropertyChanged(nameof(Heading)); _owner.ScenarioChanged(); }
        }
    }
    public string Heading => Direction is { } d ? $"{d} Scenario" : "Scenario · direction not selected";
    public TradingPlanSetupLink? Setup => _setup;
    public TradingSetupListItem? Candidate
    {
        get => _candidate;
        set { if (_owner.CanEdit && (value is null || _owner.AvailablePlaybooks.Contains(value)) && SetProperty(ref _candidate, value)) LinkCommand.NotifyCanExecuteChanged(); }
    }
    public string PlaybookText => _playbookText;
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IAsyncRelayCommand LinkCommand { get; }
    public IRelayCommand UnlinkCommand { get; }
    public IAsyncRelayCommand OpenPlaybookCommand { get; }
    internal IEnumerable<PreparationField> Fields => [Name, Condition, Response, Invalidation];
    internal object RawState => new { Id, InstrumentId, Name = Name.Text, Role, Direction, Condition = Condition.Text,
        Response = Response.Text, Invalidation = Invalidation.Text, Setup };
    internal TradingPlanScenario Build() => new(Id, InstrumentId, Name.Text, Role, Direction, Condition.Text, Response.Text, Invalidation.Text, Setup);
    internal void SetLink(TradingPlanSetupLink? value)
    {
        _setup = value; OnPropertyChanged(nameof(Setup));
        if (value is null) Present("No Playbook linked. A link is optional and does not prove adherence.", false);
        NotifyAccess(); _owner.ScenarioChanged();
    }
    internal void Present(string text, bool canNavigate)
    { _playbookText = text; _canNavigate = canNavigate; OnPropertyChanged(nameof(PlaybookText)); OpenPlaybookCommand.NotifyCanExecuteChanged(); }
    internal void NotifyAccess()
    {
        MoveUpCommand.NotifyCanExecuteChanged(); MoveDownCommand.NotifyCanExecuteChanged(); RemoveCommand.NotifyCanExecuteChanged();
        LinkCommand.NotifyCanExecuteChanged(); UnlinkCommand.NotifyCanExecuteChanged(); OpenPlaybookCommand.NotifyCanExecuteChanged();
    }
}
