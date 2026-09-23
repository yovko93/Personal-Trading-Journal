namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Re-prepares an approved reconstruction against current read data immediately
/// before delegating the atomic persistence boundary.
/// </summary>
public sealed class ImportTradovateTradesUseCase
{
    private readonly TradovateImportPreparationService _preparationService;
    private readonly ITradovateImportStore _store;
    private readonly TimeProvider _timeProvider;

    public ImportTradovateTradesUseCase(
        TradovateImportPreparationService preparationService,
        ITradovateImportStore store,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(preparationService);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _preparationService = preparationService;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<TradovateImportResult> ImportAsync(
        TradovateExecutionReconstructionResult reconstruction,
        TradovateInstrumentResolutionResult instrumentResolution,
        Guid selectedTradingAccountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reconstruction);
        ArgumentNullException.ThrowIfNull(instrumentResolution);

        TradovateImportPreparationResult preparation =
            await _preparationService.PrepareAsync(
                reconstruction,
                instrumentResolution,
                selectedTradingAccountId,
                cancellationToken);

        if (!preparation.IsReadyForPreview)
        {
            TradovateImportPreparationDiagnostic? error = preparation.Diagnostics
                .FirstOrDefault(item =>
                    item.Severity == TradovateReconstructionDiagnosticSeverity.Error);
            return TradovateImportResult.Blocked(
                error?.Code ?? TradovateImportConflictCodes.PreparationBlocked,
                error?.Message ?? "Tradovate import preparation is not ready.");
        }

        DateTimeOffset importedAtUtc = _timeProvider.GetUtcNow();
        return await _store.ImportAsync(
            new TradovateImportRequest(preparation, importedAtUtc),
            cancellationToken);
    }
}
