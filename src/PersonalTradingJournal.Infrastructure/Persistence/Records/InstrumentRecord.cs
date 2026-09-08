using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class InstrumentRecord
{
    public Guid Id { get; set; }

    public string Symbol { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public AssetClass AssetClass { get; set; }

    public string? Exchange { get; set; }

    public string Currency { get; set; } = null!;

    public decimal TickSize { get; set; }

    public decimal TickValue { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
