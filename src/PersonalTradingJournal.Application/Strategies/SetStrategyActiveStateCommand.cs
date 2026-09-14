namespace PersonalTradingJournal.Application.Strategies;

public sealed record SetStrategyActiveStateCommand(
    Guid StrategyId,
    bool IsActive);
