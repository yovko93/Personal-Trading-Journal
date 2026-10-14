namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradingRulePositionRecord
{
    public Guid RuleId { get; set; }
    public int InitialRevision { get; set; } = 1;
    public long Position { get; set; }
}

public sealed class TradingRuleCatalogStateRecord
{
    public int Id { get; set; } = 1;
    public long Version { get; set; }
}
