using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Rules;

public sealed class WriteTradingRuleUseCase(ITradingRuleStore rules, TimeProvider clock)
{
    private readonly ITradingRuleStore _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<TradingRuleWriteResult<TradingRuleRevision>> ExecuteAsync(
        WriteTradingRuleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RuleId == Guid.Empty || command.ExpectedRevision < 0 || !Enum.IsDefined(command.Action))
            throw new ArgumentException("A Rule ID, non-negative expected revision, and action are required.", nameof(command));
        if ((command.Action is TradingRuleAuthoringAction.Create or TradingRuleAuthoringAction.Revise) !=
            (command.Definition is not null))
            throw new ArgumentException("A definition is required only for Create or Revise.", nameof(command));
        cancellationToken.ThrowIfCancellationRequested();
        TradingRuleRevision? current = await _rules.GetLatestAsync(command.RuleId, cancellationToken);
        // Legacy scoped definitions are retained for reading, never silently promoted or reopened.
        if (command.Definition is { IsGlobal: false } || current is { Definition.IsGlobal: false } ||
            command.Action is TradingRuleAuthoringAction.Archive or TradingRuleAuthoringAction.Reopen)
            return new(TradingRuleWriteStatus.InvalidState, null);
        if (current is null && command.Action != TradingRuleAuthoringAction.Create)
            return new(TradingRuleWriteStatus.MissingRule, null);
        if ((current?.Revision ?? 0) != command.ExpectedRevision ||
            (current is not null && command.Action == TradingRuleAuthoringAction.Create))
            return new(TradingRuleWriteStatus.Conflict, null);
        if ((command.Action == TradingRuleAuthoringAction.Revise && !current!.IsActive) ||
            (command.Action == TradingRuleAuthoringAction.Delete && !current!.IsActive) ||
            (command.Action == TradingRuleAuthoringAction.Reopen && current!.IsActive))
            return new(TradingRuleWriteStatus.InvalidState, null);
        DateTimeOffset now = _clock.GetUtcNow();
        TradingRuleRevision candidate = command.Action switch
        {
            TradingRuleAuthoringAction.Create => TradingRuleRevision.Create(command.RuleId, command.Definition!, now),
            TradingRuleAuthoringAction.Revise => current!.Revise(command.ExpectedRevision, command.Definition!, now),
            TradingRuleAuthoringAction.Archive => current!.Archive(command.ExpectedRevision, now),
            TradingRuleAuthoringAction.Reopen => current!.Reopen(command.ExpectedRevision, now),
            TradingRuleAuthoringAction.Delete => current!.Archive(command.ExpectedRevision, now),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
        cancellationToken.ThrowIfCancellationRequested();
        return await _rules.TryAppendAsync(candidate, command.ExpectedRevision, cancellationToken)
            ? new(TradingRuleWriteStatus.Saved, candidate)
            : new(TradingRuleWriteStatus.Conflict, null);
    }
}

public sealed class RecordTradingRuleAssessmentUseCase(
    ITradingRuleStore rules, ITradingRuleAssessmentStore assessments, TimeProvider clock)
{
    private readonly ITradingRuleStore _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    private readonly ITradingRuleAssessmentStore _assessments = assessments ?? throw new ArgumentNullException(nameof(assessments));
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<TradingRuleWriteResult<TradingRuleAssessment>> ExecuteAsync(
        RecordTradingRuleAssessmentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RuleId == Guid.Empty || command.ExpectedRuleRevision < 1 ||
            command.Target is null || command.EvidenceReferences is null)
            throw new ArgumentException("A Rule revision, target, and evidence collection are required.", nameof(command));
        // Missing/ambiguous facts may be recorded as CannotEvaluate, but cannot support a pass.
        // A manual, explicitly entered violation is independently meaningful evidence.
        bool needsRecordedEvidence = command.Outcome == TradingRuleAssessmentOutcome.NoViolation ||
            (command.Outcome == TradingRuleAssessmentOutcome.ConfirmedViolation &&
             command.Basis == TradingRuleAssessmentBasis.RecordedEvidence);
        if (needsRecordedEvidence &&
            (command.EvidenceSufficiency != TradingRuleEvidenceSufficiency.Sufficient ||
             command.EvidenceReferences.Count == 0 ||
             command.EvidenceReferences.All(reference => reference.Kind == TradingRuleEvidenceKind.PlaybookCapture)))
            return new(TradingRuleWriteStatus.MissingEvidence, null);

        cancellationToken.ThrowIfCancellationRequested();
        TradingRuleRevision? rule = await _rules.GetLatestAsync(command.RuleId, cancellationToken);
        if (rule is null) return new(TradingRuleWriteStatus.MissingRule, null);
        if (rule.Revision != command.ExpectedRuleRevision)
            return new(TradingRuleWriteStatus.Conflict, null);
        if (!rule.IsActive || !rule.Definition.IsGlobal) return new(TradingRuleWriteStatus.InvalidState, null);
        TradingRuleAssessment? latest = await _assessments.GetLatestAsync(rule.RuleId, command.Target, cancellationToken);
        if (latest?.Id != command.ExpectedLatestAssessmentId)
            return new(TradingRuleWriteStatus.Conflict, null);
        TradingRuleAssessment candidate = TradingRuleAssessment.Record(Guid.NewGuid(), rule, command.Target,
            command.Outcome, command.Source, command.Basis, command.EvidenceSufficiency,
            command.Explanation, command.EvidenceReferences, latest?.Id, _clock.GetUtcNow());
        cancellationToken.ThrowIfCancellationRequested();
        return await _assessments.TryAppendAsync(candidate, command.ExpectedRuleRevision,
            command.ExpectedLatestAssessmentId, cancellationToken)
            ? new(TradingRuleWriteStatus.Saved, candidate)
            : new(TradingRuleWriteStatus.Conflict, null);
    }
}
