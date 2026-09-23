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

    public DateTimeOffset ImportedAtUtc { get; set; }
}
