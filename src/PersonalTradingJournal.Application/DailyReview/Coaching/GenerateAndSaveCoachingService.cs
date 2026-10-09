namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum SavedCoachingGenerationStatus { Saved, GenerationFailed, InvalidGeneration, StorageFailed, Cancelled }
public sealed record SavedCoachingGenerationResult(SavedCoachingGenerationStatus Status,
    SavedCoachingAnalysis? Analysis, CoachingGenerationStatus? GenerationStatus, string Message, CoachingRequestMetadata? Diagnostics = null)
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
                null, generation.Status, generation.Message, generation.Metadata);
        var diagnostics = generation.Metadata is null ? null : generation.Metadata with { Phase = CoachingGenerationPhase.SnapshotValidation };
        CoachingAnalysisSnapshot snapshot;
        try { snapshot = CoachingAnalysisSnapshot.Create(packet, generation, clock.GetUtcNow(), cancellationToken); }
        catch (OperationCanceledException) { return Cancelled(diagnostics); }
        catch (ArgumentException)
        { return new(SavedCoachingGenerationStatus.InvalidGeneration, null, generation.Status, "The generation cannot be saved as a validated snapshot.", diagnostics); }
        diagnostics = diagnostics is null ? null : diagnostics with { Phase = CoachingGenerationPhase.AtomicSave };
        try
        {
            var saved = await repository.SaveAsync(snapshot, cancellationToken);
            return new(SavedCoachingGenerationStatus.Saved, saved, generation.Status, "Validated review and evidence saved.",
                diagnostics is null ? null : diagnostics with { Phase = CoachingGenerationPhase.Saved });
        }
        catch (OperationCanceledException) { return Cancelled(diagnostics); }
        catch (Exception)
        {
            // Database/provider exception text may contain sensitive payloads. Never echo or log it here.
            return new(SavedCoachingGenerationStatus.StorageFailed, null, generation.Status,
                "The generated review was not confirmed saved. Check local storage/history before explicitly generating again; another request may cost money.", diagnostics);
        }
    }

    private static SavedCoachingGenerationResult Cancelled(CoachingRequestMetadata? diagnostics) => new(SavedCoachingGenerationStatus.Cancelled,
        null, CoachingGenerationStatus.Cancelled, "Saving cancelled. Check history if cancellation coincided with commit; generation may already have been billed.", diagnostics);
}
