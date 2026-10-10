namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum CoachingObservationBasis { CalculatedFact, RecordedTradeFact, UserWrittenJournalObservation }

public sealed record CoachingStatement(string Text, IReadOnlyList<string> SourceIds);

public sealed record CoachingObservation(CoachingObservationBasis Basis, string Text, IReadOnlyList<string> SourceIds);

/// <summary>Model-proposed statements, never authoritative replacements for calculated facts.
/// Every section item (including summary, suggestions and uncertainties) requires packet citations.
/// Empty observation/suggestion lists are valid when there is insufficient evidence.</summary>
public sealed record CoachingResponse(string ContractVersion, string PacketId, CoachingStatement DaySummary,
    IReadOnlyList<CoachingObservation> ExecutionObservations,
    IReadOnlyList<CoachingObservation> BehaviorObservations,
    IReadOnlyList<CoachingStatement> ImprovementSuggestions,
    IReadOnlyList<CoachingStatement> Uncertainties);

public sealed record CoachingResponseValidation(bool IsValid, CoachingResponse? Response, IReadOnlyList<string> Errors);
