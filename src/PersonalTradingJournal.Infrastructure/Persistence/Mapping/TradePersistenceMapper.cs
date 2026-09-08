using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradePersistenceMapper
{
    public static TradeRecord ToRecord(Trade trade)
    {
        ArgumentNullException.ThrowIfNull(trade);

        return new TradeRecord
        {
            Id = trade.Id,
            TradingAccountId = trade.TradingAccountId,
            InstrumentId = trade.InstrumentId,
            PricingPointValue = trade.Pricing.PointValue,
            PricingCurrency = trade.Pricing.Currency,
            StrategyId = trade.StrategyId,
            TradingSetupId = trade.TradingSetupId,
            CreatedAtUtc = trade.CreatedAtUtc,
            UpdatedAtUtc = trade.UpdatedAtUtc,
        };
    }
}
