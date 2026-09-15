namespace PersonalTradingJournal.Application.Mistakes;

public sealed record AssignTradeMistakeCommand(
    Guid TradeId,
    Guid TradingMistakeId,
    string? Note);
