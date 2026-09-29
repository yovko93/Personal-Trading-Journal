namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed class ImportTopstepTradesUseCase(
    ITopstepImportStore store, TimeProvider timeProvider, TopstepImportChangeTracker changes)
{
    public async Task<TopstepImportResult> ImportAsync(TopstepImportPreview preview, TopstepImportConfirmation confirmation,
        string fileName, Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(confirmation);
        cancellationToken.ThrowIfCancellationRequested();
        // Copy decisions before the first await so later caller mutations cannot change this request.
        var decisions = new TopstepImportConfirmation(confirmation.SnapshotFingerprint,
            Array.AsReadOnly((confirmation.ApprovedInstrumentSymbols ?? []).ToArray()));
        if (!preview.AcceptsConfirmation(decisions))
            return TopstepImportResult.Blocked(TopstepImportConflictCodes.ReviewRequired,
                "Confirm a valid current preview and approve any displayed Instrument specifications.");
        TopstepImportResult result = await store.ImportAsync(
            new(preview, decisions, fileName, source, timeProvider.GetUtcNow()), cancellationToken);
        // Never reinterpret a completed commit as cancellation, or invoke fallible UI callbacks here.
        changes.Committed(result);
        return result;
    }
}
