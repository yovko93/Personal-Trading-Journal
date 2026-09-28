using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovateImportAccountSnapshot(
    Guid TradingAccountId,
    string Name,
    TradingAccountType AccountType,
    string? ProviderName,
    string? ExternalAccountId,
    string Currency,
    bool IsActive);
