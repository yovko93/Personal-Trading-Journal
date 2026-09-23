using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Persistence.Sorting;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradeBrowsePersistenceMapper
{
    public const int CurrentProjectionVersion = 2;

    public static TradeBrowseRecord ToRecord(Trade trade)
    {
        ArgumentNullException.ThrowIfNull(trade);

        var record = new TradeBrowseRecord();
        Apply(record, trade);
        return record;
    }

    public static void Apply(TradeBrowseRecord record, Trade trade)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(trade);

        record.TradeId = trade.Id;
        record.ProjectionVersion = CurrentProjectionVersion;
        record.OpenedAtUtc = trade.OpenedAtUtc;
        record.ClosedAtUtc = trade.ClosedAtUtc;
        record.Direction = trade.Direction;
        record.Status = trade.Status;
        record.OpenQuantity = trade.OpenQuantity;
        record.OpenQuantitySortKey = DecimalSortKey.Encode(trade.OpenQuantity);
        record.AverageEntryPrice = trade.AverageEntryPrice;
        record.AverageEntryPriceSortKey = DecimalSortKey.Encode(
            trade.AverageEntryPrice);
        record.AverageExitPrice = trade.AverageExitPrice;
        record.TotalCosts = trade.TotalCosts;
        record.GrossPnL = trade.GrossPnL;
        record.NetPnL = trade.NetPnL;
        record.NetPnLSortKey = trade.NetPnL.HasValue
            ? DecimalSortKey.Encode(trade.NetPnL.Value)
            : null;
    }
}
