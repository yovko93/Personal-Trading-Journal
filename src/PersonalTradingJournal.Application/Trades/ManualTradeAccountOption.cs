using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Trades;

public sealed record ManualTradeAccountOption(
    Guid Id,
    string Name,
    TradingAccountType AccountType,
    string? ProviderName,
    string? ExternalAccountId,
    string Currency,
    bool IsActive);
