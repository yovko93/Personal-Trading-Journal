using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public sealed record CreateManualTradeCommand(
    Guid TradingAccountId,
    Guid InstrumentId,
    TradeDirection Direction,
    decimal Quantity,
    ManualTradeExecutionInput Entry,
    ManualTradeExecutionInput? Exit);
