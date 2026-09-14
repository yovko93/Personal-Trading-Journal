namespace PersonalTradingJournal.Application.Mistakes;

public sealed record TradingMistakeListItem(Guid Id, string Name, string? Description,
    bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
