using PersonalTradingJournal.Application.Imports;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

// Existing parser unit doubles do not consume real CSV; routing is independently exercised with real headers.
internal sealed class TradovateOnlyFormatDetector : IImportCsvFormatDetector
{
    public Task<ImportCsvFormatResult> DetectAsync(Stream source, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportCsvFormatResult(ImportCsvFormat.Tradovate));
}
