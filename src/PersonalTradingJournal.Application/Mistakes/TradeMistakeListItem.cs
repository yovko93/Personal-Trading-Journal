namespace PersonalTradingJournal.Application.Mistakes;

public sealed record TradeMistakeListItem(
    Guid Id,
    Guid TradeId,
    Guid TradingMistakeId,
    string TradingMistakeName,
    bool IsTradingMistakeActive,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
