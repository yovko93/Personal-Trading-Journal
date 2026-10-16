using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Application.PeriodReviews.Coaching;

public sealed record SavedPeriodGenerationResult(SavedCoachingGenerationStatus Status, SavedPeriodReview? Review,
    PeriodCoachingStatus GenerationStatus, string Message, CoachingRequestMetadata? Diagnostics = null)
{
    public override string ToString() => $"SavedPeriodGenerationResult: {Status}";
}

/// <summary>One explicit generation and one atomic save. No automatic or storage retry.</summary>
public sealed class GenerateAndSavePeriodReviewService(PeriodCoachingGenerationService generator,
    IPeriodReviewRepository repository, TimeProvider clock)
{
    public async Task<SavedPeriodGenerationResult> GenerateAsync(PeriodReviewQuery query, CancellationToken cancellationToken = default)
    {
        var generation = await generator.GenerateAsync(query, cancellationToken);
        if (generation.Status != PeriodCoachingStatus.Success)
            return new(generation.Status == PeriodCoachingStatus.Cancelled ? SavedCoachingGenerationStatus.Cancelled : SavedCoachingGenerationStatus.GenerationFailed,
                null, generation.Status, generation.Message, generation.Metadata);
        var diagnostics = generation.Metadata is null ? null : generation.Metadata with { Phase = CoachingGenerationPhase.SnapshotValidation };
        PeriodReviewSnapshot snapshot;
        try { snapshot = PeriodReviewSnapshot.Create(generation, clock.GetUtcNow(), cancellationToken); }
        catch (OperationCanceledException) { return Cancelled(diagnostics); }
        catch (ArgumentException) { return new(SavedCoachingGenerationStatus.InvalidGeneration, null, generation.Status,
            "The period review cannot be saved as a validated snapshot.", diagnostics); }
        diagnostics = diagnostics is null ? null : diagnostics with { Phase = CoachingGenerationPhase.AtomicSave };
        try
        {
            var saved = await repository.SaveAsync(snapshot, cancellationToken);
            return new(SavedCoachingGenerationStatus.Saved, saved, generation.Status, "Validated period review and exact evidence saved.",
                diagnostics is null ? null : diagnostics with { Phase = CoachingGenerationPhase.Saved });
        }
        catch (OperationCanceledException) { return Cancelled(diagnostics); }
        catch (Exception)
        {
            return new(SavedCoachingGenerationStatus.StorageFailed, null, generation.Status,
                "The period review was not confirmed saved. Check storage/history before explicitly generating again; another request may cost money.", diagnostics);
        }
    }
    private static SavedPeriodGenerationResult Cancelled(CoachingRequestMetadata? diagnostics) => new(SavedCoachingGenerationStatus.Cancelled,
        null, PeriodCoachingStatus.Cancelled, "Period review saving cancelled before commit. Generation may already have been billed.", diagnostics);
}
