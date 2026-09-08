namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradeRecord
{
    public Guid Id { get; set; }

    public Guid TradingAccountId { get; set; }

    public Guid InstrumentId { get; set; }

    public decimal PricingPointValue { get; set; }

    public string PricingCurrency { get; set; } = null!;

    public Guid? StrategyId { get; set; }

    public Guid? TradingSetupId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
