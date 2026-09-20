using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradeBrowseRecord
{
    public Guid TradeId { get; set; }

    public int ProjectionVersion { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public TradeDirection Direction { get; set; }

    public TradeStatus Status { get; set; }

    public decimal OpenQuantity { get; set; }

    public string OpenQuantitySortKey { get; set; } = null!;

    public decimal AverageEntryPrice { get; set; }

    public string AverageEntryPriceSortKey { get; set; } = null!;

    public decimal? AverageExitPrice { get; set; }

    public decimal TotalCosts { get; set; }

    public decimal? GrossPnL { get; set; }

    public decimal? NetPnL { get; set; }

    public string? NetPnLSortKey { get; set; }
}
