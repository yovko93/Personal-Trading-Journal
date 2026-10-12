namespace PersonalTradingJournal.Domain.Rules;

public enum TradingRuleTargetKind { Trade = 1, NewYorkDay = 2 }
public enum TradingRuleAssessmentOutcome
{
    NotAssessed = 0, // A read-model state only: absence of an assessment, never a stored assessment.
    ConfirmedViolation = 1,
    NoViolation = 2,
    CannotEvaluate = 3,
}
public enum TradingRuleAssessmentSource { Manual = 1, AutomaticCandidate = 2 }
public enum TradingRuleAssessmentBasis { RecordedEvidence = 1, UserEnteredViolation = 2 }
public enum TradingRuleEvidenceSufficiency { Sufficient = 1, Missing = 2, Ambiguous = 3 }
public enum TradingRuleEvidenceKind { Trade = 1, Execution = 2, Journal = 3, PlaybookCapture = 4 }

public sealed record TradingRuleEvidenceReference
{
    public TradingRuleEvidenceReference(TradingRuleEvidenceKind kind, Guid sourceId, long? revision = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (sourceId == Guid.Empty) throw new ArgumentException("A source ID is required.", nameof(sourceId));
        if (revision is < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        Kind = kind;
        SourceId = sourceId;
        Revision = revision;
    }

    public TradingRuleEvidenceKind Kind { get; }
    public Guid SourceId { get; }
    public long? Revision { get; }
}

/// <summary>A Trade identity or an explicit New York calendar date; never inferred from local time.</summary>
public sealed record TradingRuleAssessmentTarget
{
    private TradingRuleAssessmentTarget(TradingRuleTargetKind kind, Guid? tradeId,
        DateOnly? newYorkDate, TradingRuleAccountScope accountScope)
    {
        Kind = kind;
        TradeId = tradeId;
        NewYorkDate = newYorkDate;
        AccountScope = accountScope;
    }

    public TradingRuleTargetKind Kind { get; }
    public Guid? TradeId { get; }
    public DateOnly? NewYorkDate { get; }
    public TradingRuleAccountScope AccountScope { get; }

    public static TradingRuleAssessmentTarget ForTrade(Guid tradeId, Guid accountId)
    {
        if (tradeId == Guid.Empty) throw new ArgumentException("A Trade ID is required.", nameof(tradeId));
        return new(TradingRuleTargetKind.Trade, tradeId, null, TradingRuleAccountScope.ForAccount(accountId));
    }

    public static TradingRuleAssessmentTarget ForNewYorkDay(DateOnly date, TradingRuleAccountScope accountScope)
    {
        if (date == default) throw new ArgumentException("A New York date is required.", nameof(date));
        return new(TradingRuleTargetKind.NewYorkDay, null, date,
            accountScope ?? throw new ArgumentNullException(nameof(accountScope)));
    }
}

/// <summary>
/// Append-only assessment. The full Rule revision is frozen here; a later rule edit cannot change
/// the meaning of this assessment. A new assessment supersedes the old ID instead of mutating it.
/// </summary>
public sealed class TradingRuleAssessment
{
    private TradingRuleAssessment(Guid id, TradingRuleRevision ruleRevision,
        TradingRuleAssessmentTarget target, TradingRuleAssessmentOutcome outcome,
        TradingRuleAssessmentSource source, TradingRuleAssessmentBasis basis,
        TradingRuleEvidenceSufficiency evidenceSufficiency, string explanation,
        IReadOnlyList<TradingRuleEvidenceReference> evidenceReferences,
        Guid? supersedesAssessmentId, DateTimeOffset recordedAtUtc)
    {
        Id = id;
        RuleRevision = ruleRevision;
        Target = target;
        Outcome = outcome;
        Source = source;
        Basis = basis;
        EvidenceSufficiency = evidenceSufficiency;
        Explanation = explanation;
        EvidenceReferences = evidenceReferences;
        SupersedesAssessmentId = supersedesAssessmentId;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid Id { get; }
    public TradingRuleRevision RuleRevision { get; }
    public TradingRuleAssessmentTarget Target { get; }
    public TradingRuleAssessmentOutcome Outcome { get; }
    public TradingRuleAssessmentSource Source { get; }
    public TradingRuleAssessmentBasis Basis { get; }
    public TradingRuleEvidenceSufficiency EvidenceSufficiency { get; }
    public string Explanation { get; }
    public IReadOnlyList<TradingRuleEvidenceReference> EvidenceReferences { get; }
    public Guid? SupersedesAssessmentId { get; }
    public DateTimeOffset RecordedAtUtc { get; }

    public static TradingRuleAssessment Record(Guid id, TradingRuleRevision ruleRevision,
        TradingRuleAssessmentTarget target, TradingRuleAssessmentOutcome outcome,
        TradingRuleAssessmentSource source, TradingRuleAssessmentBasis basis,
        TradingRuleEvidenceSufficiency evidenceSufficiency, string explanation,
        IEnumerable<TradingRuleEvidenceReference> evidenceReferences,
        Guid? supersedesAssessmentId, DateTimeOffset recordedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("An assessment ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(ruleRevision);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(evidenceReferences);
        if (!ruleRevision.IsActive) throw new InvalidOperationException("Archived Rules cannot receive new assessments.");
        if (!ruleRevision.Definition.IsGlobal && ((ruleRevision.Definition.Applicability == TradingRuleApplicability.Trade && target.Kind != TradingRuleTargetKind.Trade) ||
            (ruleRevision.Definition.Applicability == TradingRuleApplicability.NewYorkDay && target.Kind != TradingRuleTargetKind.NewYorkDay)))
            throw new ArgumentException("Rule applicability does not match the assessment target.", nameof(target));
        if (ruleRevision.Definition.AccountScope.Kind == TradingRuleAccountScopeKind.ExactAccount &&
            ruleRevision.Definition.AccountScope != target.AccountScope)
            throw new ArgumentException("An exact-Account Rule requires that exact Account target.", nameof(target));
        if (outcome == TradingRuleAssessmentOutcome.NotAssessed || !Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome), "Not assessed means no assessment record.");
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (!Enum.IsDefined(basis)) throw new ArgumentOutOfRangeException(nameof(basis));
        if (!Enum.IsDefined(evidenceSufficiency)) throw new ArgumentOutOfRangeException(nameof(evidenceSufficiency));
        if (string.IsNullOrWhiteSpace(explanation) || explanation.Length > 4_000)
            throw new ArgumentException("A concise explanation is required (at most 4000 characters).", nameof(explanation));
        if (supersedesAssessmentId == Guid.Empty || supersedesAssessmentId == id)
            throw new ArgumentException("The prior assessment ID must be distinct and non-empty.", nameof(supersedesAssessmentId));
        if (recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Assessment time must be UTC.", nameof(recordedAtUtc));
        if (recordedAtUtc < ruleRevision.RecordedAtUtc)
            throw new ArgumentException("An assessment cannot predate its Rule revision.", nameof(recordedAtUtc));

        TradingRuleEvidenceReference[] references = evidenceReferences.ToArray();
        if (references.Any(reference => reference is null) || references.Length != references.Distinct().Count())
            throw new ArgumentException("Evidence references must be non-null and unique.", nameof(evidenceReferences));
        if (basis == TradingRuleAssessmentBasis.UserEnteredViolation &&
            (outcome != TradingRuleAssessmentOutcome.ConfirmedViolation || source != TradingRuleAssessmentSource.Manual))
            throw new ArgumentException("Only a manual confirmed violation may be based on an explicit user entry.", nameof(basis));
        if (outcome == TradingRuleAssessmentOutcome.CannotEvaluate &&
            (evidenceSufficiency == TradingRuleEvidenceSufficiency.Sufficient ||
             basis != TradingRuleAssessmentBasis.RecordedEvidence))
            throw new ArgumentException("Cannot evaluate requires missing or ambiguous recorded evidence.", nameof(evidenceSufficiency));
        if ((outcome == TradingRuleAssessmentOutcome.NoViolation ||
             (outcome == TradingRuleAssessmentOutcome.ConfirmedViolation && basis == TradingRuleAssessmentBasis.RecordedEvidence)) &&
            (evidenceSufficiency != TradingRuleEvidenceSufficiency.Sufficient || references.Length == 0 ||
             references.All(reference => reference.Kind == TradingRuleEvidenceKind.PlaybookCapture)))
            throw new ArgumentException("A recorded pass or violation requires sufficient, referenced evidence.", nameof(evidenceReferences));

        return new(id, ruleRevision, target, outcome, source, basis, evidenceSufficiency,
            explanation.Trim(), Array.AsReadOnly(references), supersedesAssessmentId, recordedAtUtc);
    }
}
