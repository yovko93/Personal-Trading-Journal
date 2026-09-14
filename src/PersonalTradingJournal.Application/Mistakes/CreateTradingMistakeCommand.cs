namespace PersonalTradingJournal.Application.Mistakes;

public sealed record CreateTradingMistakeCommand(string Name, string? Description);
