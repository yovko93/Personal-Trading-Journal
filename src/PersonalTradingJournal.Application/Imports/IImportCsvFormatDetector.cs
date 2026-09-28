namespace PersonalTradingJournal.Application.Imports;

public enum ImportCsvFormat { Unknown, Tradovate, Topstep }

public sealed record ImportCsvFormatResult(ImportCsvFormat Format, string? Message = null, int? SourceLineNumber = null);

public interface IImportCsvFormatDetector
{
    // Consumes a complete source, leaves it open. Routing validates the header, not row economics.
    Task<ImportCsvFormatResult> DetectAsync(Stream source, CancellationToken cancellationToken = default);
}
