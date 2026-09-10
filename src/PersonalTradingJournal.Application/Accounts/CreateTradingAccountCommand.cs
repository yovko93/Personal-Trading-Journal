using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public sealed record CreateTradingAccountCommand(
    string Name,
    TradingAccountType AccountType,
    string? ProviderName,
    string? ExternalAccountId,
    string Currency,
    decimal? StartingBalance);
