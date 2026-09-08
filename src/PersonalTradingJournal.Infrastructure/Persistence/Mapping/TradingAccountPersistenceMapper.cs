using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradingAccountPersistenceMapper
{
    public static TradingAccountRecord ToRecord(TradingAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new TradingAccountRecord
        {
            Id = account.Id,
            Name = account.Name,
            AccountType = account.AccountType,
            ProviderName = account.ProviderName,
            ExternalAccountId = account.ExternalAccountId,
            Currency = account.Currency,
            StartingBalance = account.StartingBalance,
            IsActive = account.IsActive,
            CreatedAtUtc = account.CreatedAtUtc,
            UpdatedAtUtc = account.UpdatedAtUtc,
        };
    }

    public static TradingAccount ToDomain(TradingAccountRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return TradingAccount.Rehydrate(
            record.Id,
            record.Name,
            record.AccountType,
            record.ProviderName,
            record.ExternalAccountId,
            record.Currency,
            record.StartingBalance,
            record.IsActive,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
