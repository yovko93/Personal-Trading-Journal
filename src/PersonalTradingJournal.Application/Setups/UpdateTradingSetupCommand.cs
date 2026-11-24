namespace PersonalTradingJournal.Application.Setups;

public sealed record UpdateTradingSetupCommand(
    Guid TradingSetupId,
    string Name,
    string? Description,
    SetupImageChanges? Images = null);

public sealed record SetupImageChanges(IReadOnlyList<NewSetupExample> Added, IReadOnlyList<SetupImageEdit> Edited);
public sealed record SetupImageEdit(Guid Id, string? OriginalTimeframe, string? Timeframe, bool Remove);
