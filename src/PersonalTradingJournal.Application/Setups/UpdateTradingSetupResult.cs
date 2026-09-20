namespace PersonalTradingJournal.Application.Setups;

public sealed record UpdateTradingSetupResult(
    TradingSetupDetails TradingSetup,
    bool WasChanged);
