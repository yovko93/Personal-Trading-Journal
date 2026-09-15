namespace PersonalTradingJournal.Application.Setups;

public sealed record SetTradingSetupActiveStateCommand(Guid TradingSetupId, bool IsActive);
