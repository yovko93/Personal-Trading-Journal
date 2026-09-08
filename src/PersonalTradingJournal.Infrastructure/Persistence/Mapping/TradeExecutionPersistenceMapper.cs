using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradeExecutionPersistenceMapper
{
    public static TradeExecutionRecord ToRecord(TradeExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        return new TradeExecutionRecord
        {
            Id = execution.Id,
            TradeId = execution.TradeId,
            Sequence = execution.Sequence,
            ExecutedAtUtc = execution.ExecutedAtUtc,
            Side = execution.Side,
            Quantity = execution.Quantity,
            Price = execution.Price,
            Commission = execution.Commission,
            Fees = execution.Fees,
            ExternalExecutionId = execution.ExternalExecutionId,
            ExternalOrderId = execution.ExternalOrderId,
            BrokerSymbol = execution.BrokerSymbol,
        };
    }

    public static TradeExecution ToDomain(TradeExecutionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return TradeExecution.Rehydrate(
            record.Id,
            record.TradeId,
            record.Sequence,
            record.ExecutedAtUtc,
            record.Side,
            record.Quantity,
            record.Price,
            record.Commission,
            record.Fees,
            record.ExternalExecutionId,
            record.ExternalOrderId,
            record.BrokerSymbol);
    }
}
