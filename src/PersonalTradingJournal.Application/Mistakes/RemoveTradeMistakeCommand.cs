namespace PersonalTradingJournal.Application.Mistakes;

public sealed record RemoveTradeMistakeCommand(
    Guid TradeId,
    Guid TradeMistakeId);
