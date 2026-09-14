namespace PersonalTradingJournal.Application.Strategies;

public sealed record StrategyListItem(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
