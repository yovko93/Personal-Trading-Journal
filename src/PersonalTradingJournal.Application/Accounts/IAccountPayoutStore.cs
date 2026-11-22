namespace PersonalTradingJournal.Application.Accounts;
public sealed record AccountPayout(Guid Id, Guid AccountId, decimal Amount, string Currency, DateTimeOffset CreatedAtUtc);
public interface IAccountPayoutStore
{
    Task<IReadOnlyList<AccountPayout>> GetAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task AddAsync(Guid accountId, string currency, decimal amount, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid accountId, Guid payoutId, CancellationToken cancellationToken = default);
}
