namespace PersonalTradingJournal.Application.Trades;

public sealed record ManualTradeExecutionInput(
    DateTimeOffset ExecutedAtUtc,
    decimal Price,
    decimal Commission,
    decimal Fees);
