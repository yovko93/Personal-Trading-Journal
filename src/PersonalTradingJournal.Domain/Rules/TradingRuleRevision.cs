namespace PersonalTradingJournal.Domain.Rules;

/// <summary>One immutable, numbered state of a stable Trading Rule identity.</summary>
public sealed class TradingRuleRevision
{
    private TradingRuleRevision(Guid ruleId, int revision, TradingRuleDefinition definition,
        bool isActive, DateTimeOffset recordedAtUtc)
    {
        if (ruleId == Guid.Empty) throw new ArgumentException("A Rule ID is required.", nameof(ruleId));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Revision time must be UTC.", nameof(recordedAtUtc));
        RuleId = ruleId;
        Revision = revision;
        IsActive = isActive;
        RecordedAtUtc = recordedAtUtc;
    }

    public const int ContractVersion = 1;
    public Guid RuleId { get; }
    public int Revision { get; }
    public TradingRuleDefinition Definition { get; }
    public bool IsActive { get; }
    public DateTimeOffset RecordedAtUtc { get; }

    public static TradingRuleRevision Create(Guid ruleId, TradingRuleDefinition definition, DateTimeOffset recordedAtUtc) =>
        new(ruleId, 1, definition, true, recordedAtUtc);

    /// <summary>For later persistence readers; never consults the current revision.</summary>
    public static TradingRuleRevision Rehydrate(Guid ruleId, int revision, TradingRuleDefinition definition,
        bool isActive, DateTimeOffset recordedAtUtc) => new(ruleId, revision, definition, isActive, recordedAtUtc);

    public TradingRuleRevision Revise(int expectedRevision, TradingRuleDefinition definition, DateTimeOffset recordedAtUtc)
    {
        if (!IsActive) throw new InvalidOperationException("Reopen the Rule before editing it.");
        return Next(expectedRevision, definition, true, recordedAtUtc);
    }

    public TradingRuleRevision Archive(int expectedRevision, DateTimeOffset recordedAtUtc)
    {
        if (!IsActive) throw new InvalidOperationException("The Rule is already archived.");
        return Next(expectedRevision, Definition, false, recordedAtUtc);
    }

    public TradingRuleRevision Reopen(int expectedRevision, DateTimeOffset recordedAtUtc)
    {
        if (IsActive) throw new InvalidOperationException("The Rule is already active.");
        return Next(expectedRevision, Definition, true, recordedAtUtc);
    }

    private TradingRuleRevision Next(int expectedRevision, TradingRuleDefinition definition,
        bool isActive, DateTimeOffset recordedAtUtc)
    {
        if (expectedRevision != Revision)
            throw new InvalidOperationException("The Rule revision changed; reload before editing.");
        if (recordedAtUtc < RecordedAtUtc)
            throw new ArgumentException("Revision time cannot move backwards.", nameof(recordedAtUtc));
        return new TradingRuleRevision(RuleId, checked(Revision + 1), definition, isActive, recordedAtUtc);
    }
}
