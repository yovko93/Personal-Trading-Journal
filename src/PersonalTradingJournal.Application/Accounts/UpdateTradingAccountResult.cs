namespace PersonalTradingJournal.Application.Accounts;

public sealed record UpdateTradingAccountResult(
    TradingAccountDetails Account,
    bool WasChanged);
