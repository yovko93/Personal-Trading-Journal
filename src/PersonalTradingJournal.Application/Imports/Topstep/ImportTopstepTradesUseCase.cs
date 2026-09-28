namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed class ImportTopstepTradesUseCase(
    ITopstepImportStore store, TimeProvider timeProvider, TopstepImportChangeTracker changes)
{
    public async Task<TopstepImportResult> ImportAsync(TopstepImportPreview preview, TopstepPreviewReview review,
        string fileName, Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(review);
        cancellationToken.ThrowIfCancellationRequested();
        // Copy decisions before the first await so later checkbox mutations cannot change this request.
        var decisions = new TopstepPreviewReview(review.SnapshotFingerprint,
            Array.AsReadOnly((review.AcceptedRequirementKeys ?? []).ToArray()));
        if (!preview.MeetsReviewRequirements(decisions))
            return TopstepImportResult.Blocked(TopstepImportConflictCodes.ReviewRequired,
                "Rebuild a valid preview and explicitly acknowledge its warnings and exact Instrument proposals.");
        TopstepImportResult result = await store.ImportAsync(
            new(preview, decisions, fileName, source, timeProvider.GetUtcNow()), cancellationToken);
        // Never reinterpret a completed commit as cancellation, or invoke fallible UI callbacks here.
        changes.Committed(result);
        return result;
    }
}
