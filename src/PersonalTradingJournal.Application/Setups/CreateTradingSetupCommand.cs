namespace PersonalTradingJournal.Application.Setups;

public sealed record CreateTradingSetupCommand(string Name, string? Description, IReadOnlyList<NewSetupExample>? Examples = null);

public sealed record NewSetupExample(string FileName, byte[] Bytes, string? Timeframe);
