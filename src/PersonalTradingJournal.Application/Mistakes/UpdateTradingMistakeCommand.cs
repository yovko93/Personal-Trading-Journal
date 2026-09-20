namespace PersonalTradingJournal.Application.Mistakes;

public sealed record UpdateTradingMistakeCommand(
    Guid TradingMistakeId,
    string Name,
    string? Description);
