namespace PersonalTradingJournal.Application.Setups;

public sealed record TradingSetupListItem(Guid Id, string Name, string? Description,
    bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
