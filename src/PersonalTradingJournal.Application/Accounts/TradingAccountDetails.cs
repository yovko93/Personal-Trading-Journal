using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public sealed record TradingAccountDetails(
    Guid Id,
    string Name,
    TradingAccountType AccountType,
    string? ProviderName,
    string? ExternalAccountId,
    string Currency,
    decimal? StartingBalance,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    internal static TradingAccountDetails FromDomain(TradingAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new TradingAccountDetails(
            account.Id,
            account.Name,
            account.AccountType,
            account.ProviderName,
            account.ExternalAccountId,
            account.Currency,
            account.StartingBalance,
            account.IsActive,
            account.CreatedAtUtc,
            account.UpdatedAtUtc);
    }
}
