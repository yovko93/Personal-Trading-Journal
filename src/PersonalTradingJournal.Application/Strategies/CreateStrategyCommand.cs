namespace PersonalTradingJournal.Application.Strategies;

public sealed record CreateStrategyCommand(
    string Name,
    string? Description);
