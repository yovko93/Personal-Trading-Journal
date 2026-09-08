using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradingAccountRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public TradingAccountType AccountType { get; set; }

    public string? ProviderName { get; set; }

    public string? ExternalAccountId { get; set; }

    public string Currency { get; set; } = null!;

    public decimal? StartingBalance { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
