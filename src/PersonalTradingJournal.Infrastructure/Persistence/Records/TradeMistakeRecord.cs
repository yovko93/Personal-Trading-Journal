namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradeMistakeRecord
{
    public Guid Id { get; set; }

    public Guid TradeId { get; set; }

    public Guid TradingMistakeId { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
