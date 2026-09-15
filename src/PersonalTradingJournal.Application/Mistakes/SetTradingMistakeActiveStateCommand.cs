namespace PersonalTradingJournal.Application.Mistakes;

public sealed record SetTradingMistakeActiveStateCommand(Guid TradingMistakeId, bool IsActive);
