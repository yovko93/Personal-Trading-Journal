using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradeExecutionRecord
{
    public Guid Id { get; set; }

    public Guid TradeId { get; set; }

    public int Sequence { get; set; }

    public DateTimeOffset ExecutedAtUtc { get; set; }

    public ExecutionSide Side { get; set; }

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal Commission { get; set; }

    public decimal Fees { get; set; }

    public string? ExternalExecutionId { get; set; }

    public string? ExternalOrderId { get; set; }

    public string? BrokerSymbol { get; set; }
}
