namespace PersonalTradingJournal.Application.Setups;

public sealed record UpdateTradingSetupCommand(
    Guid TradingSetupId,
    string Name,
    string? Description);
