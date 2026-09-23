namespace PersonalTradingJournal.Application.Imports.Tradovate;

public interface ITradovateImportStore
{
    Task<TradovateImportResult> ImportAsync(
        TradovateImportRequest request,
        CancellationToken cancellationToken = default);
}
