namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class DailyTradingPrincipleRecord
{
    public Guid Id { get; set; }
    public string Text { get; set; } = "";
    public long Position { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class DailyTradingPrincipleStateRecord
{
    public int Id { get; set; }
    public long Version { get; set; }
}
