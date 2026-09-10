using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public sealed record AccountListItem(
    Guid Id,
    string Name,
    TradingAccountType AccountType,
    string? ProviderName,
    string? ExternalAccountId,
    string Currency,
    decimal? StartingBalance,
    bool IsActive);
