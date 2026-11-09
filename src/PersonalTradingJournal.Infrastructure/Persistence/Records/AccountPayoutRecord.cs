namespace PersonalTradingJournal.Infrastructure.Persistence.Records;
public sealed class AccountPayoutRecord
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
