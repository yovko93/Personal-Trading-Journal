using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradovateImportedExecutionRecord
{
    public Guid TradeExecutionId { get; set; }

    public Guid TradeId { get; set; }

    public Guid TradingAccountIdAtImport { get; set; }

    public string BrokerSymbol { get; set; } = null!;

    public ExecutionSide Side { get; set; }

    public string ExternalExecutionId { get; set; } = null!;

    public int AllocationIndex { get; set; }

    // Null only for legacy identity-only ledger rows. Never backfill from mutable Trade data.
    public decimal? SourceFillQuantity { get; set; }

    public decimal? AllocatedQuantity { get; set; }

    public decimal? SourceFillPrice { get; set; }

    public DateTimeOffset? SourceFillExecutedAtUtc { get; set; }

    public DateTimeOffset ImportedAtUtc { get; set; }
}
