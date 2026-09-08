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

    public static Trade ToDomain(
        TradeRecord record,
        IEnumerable<TradeExecutionRecord> executionRecords)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(executionRecords);

        var pricing = new TradePricingSnapshot(
            record.PricingPointValue,
            record.PricingCurrency);

        IEnumerable<TradeExecution> executions = executionRecords
            .Select(TradeExecutionPersistenceMapper.ToDomain);

        return Trade.Rehydrate(
            record.Id,
            record.TradingAccountId,
            record.InstrumentId,
            pricing,
            record.StrategyId,
            record.TradingSetupId,
            executions,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
