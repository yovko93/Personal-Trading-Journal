using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public sealed record CreateManualTradeCommand(
    Guid TradingAccountId,
    Guid InstrumentId,
    Guid? TradingSetupId,
    TradeDirection Direction,
    decimal Quantity,
    ManualTradeExecutionInput Entry,
    ManualTradeExecutionInput? Exit);
