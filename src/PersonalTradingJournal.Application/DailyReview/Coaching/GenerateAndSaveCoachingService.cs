namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum SavedCoachingGenerationStatus { Saved, GenerationFailed, InvalidGeneration, StorageFailed, Cancelled }
public sealed record SavedCoachingGenerationResult(SavedCoachingGenerationStatus Status,
    SavedCoachingAnalysis? Analysis, CoachingGenerationStatus? GenerationStatus, string Message)
{
    public override string ToString() => $"SavedCoachingGenerationResult: {Status}";
}

/// <summary>Explicit-only generate/validate/save orchestration. Storage failure is never generation success.
/// No retries (including storage failures); the response may already have incurred provider charges.</summary>
public sealed class GenerateAndSaveCoachingService(DailyCoachingGenerationService generator,
    ICoachingAnalysisRepository repository, TimeProvider clock)
{
    public async Task<SavedCoachingGenerationResult> GenerateAsync(CoachingEvidencePacket packet,
        CancellationToken cancellationToken = default)
    {
        var generation = await generator.GenerateAsync(packet, cancellationToken);
        if (generation.Status != CoachingGenerationStatus.Success)
            return new(generation.Status == CoachingGenerationStatus.Cancelled
                ? SavedCoachingGenerationStatus.Cancelled : SavedCoachingGenerationStatus.GenerationFailed,
                null, generation.Status, generation.Message);
        CoachingAnalysisSnapshot snapshot;
        try { snapshot = CoachingAnalysisSnapshot.Create(packet, generation, clock.GetUtcNow(), cancellationToken); }
        catch (OperationCanceledException) { return Cancelled(); }
        catch (ArgumentException)
        { return new(SavedCoachingGenerationStatus.InvalidGeneration, null, generation.Status, "The generation cannot be saved as a validated snapshot."); }
        try
        {
            var saved = await repository.SaveAsync(snapshot, cancellationToken);
            return new(SavedCoachingGenerationStatus.Saved, saved, generation.Status, "Validated review and evidence saved.");
        }
        catch (OperationCanceledException) { return Cancelled(); }
        catch (Exception)
        {
            // Database/provider exception text may contain sensitive payloads. Never echo or log it here.
            return new(SavedCoachingGenerationStatus.StorageFailed, null, generation.Status,
                "The generated review was not confirmed saved. Check local storage/history before explicitly generating again; another request may cost money.");
        }
    }

    private static SavedCoachingGenerationResult Cancelled() => new(SavedCoachingGenerationStatus.Cancelled,
        null, CoachingGenerationStatus.Cancelled, "Saving cancelled. Check history if cancellation coincided with commit; generation may already have been billed.");
}
