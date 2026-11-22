namespace PersonalTradingJournal.Application.Imports.Tradovate;

public interface ITradovateImportStore
{
    Task<string?> FindVerifiedImportConflictAsync(
        TradovateImportPreparationResult preparation,
        CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    Task<TradovateImportResult> ImportAsync(
        TradovateImportRequest request,
        CancellationToken cancellationToken = default);
}
