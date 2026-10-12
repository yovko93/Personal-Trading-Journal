namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradingRuleRevisionRecord
{
    public Guid RuleId { get; set; }
    public int Revision { get; set; }
    public int ContractVersion { get; set; }
    public string Name { get; set; } = null!;
    public string Criterion { get; set; } = null!;
    public string RequiredEvidence { get; set; } = null!;
    public int Applicability { get; set; }
    public int ScopeKind { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountNameAtRevision { get; set; }
    public int EvaluationMode { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}

public sealed class TradingRuleAssessmentRecord
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public int RuleRevision { get; set; }
    public int Sequence { get; set; }
    public string TargetKey { get; set; } = null!;
    public int TargetKind { get; set; }
    public Guid? TradeId { get; set; }
    public DateOnly? NewYorkDate { get; set; }
    public int ScopeKind { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountNameAtAssessment { get; set; }
    public string? TargetFingerprint { get; set; }
    public int Outcome { get; set; }
    public int Source { get; set; }
    public int Basis { get; set; }
    public int EvidenceSufficiency { get; set; }
    public string Explanation { get; set; } = null!;
    public string EvidenceJson { get; set; } = null!;
    public Guid? SupersedesAssessmentId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
