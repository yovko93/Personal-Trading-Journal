using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public sealed record UpdateTradeCommand(
    Guid TradeId,
    Guid TradingAccountId,
    Guid InstrumentId,
    Guid? TradingSetupId,
    TradeDirection Direction,
    decimal Quantity,
    UpdateTradeExecutionInput Entry,
    UpdateTradeExecutionInput? Exit);
