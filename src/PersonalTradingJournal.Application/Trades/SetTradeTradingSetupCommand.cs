namespace PersonalTradingJournal.Application.Trades;

public sealed record SetTradeTradingSetupCommand(
    Guid TradeId,
    Guid? TradingSetupId);
