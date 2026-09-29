namespace PersonalTradingJournal.Application.Imports;

public enum ImportCsvFormat { Unknown, Tradovate, Topstep }

public sealed record ImportCsvFormatResult(ImportCsvFormat Format, string? Message = null, int? SourceLineNumber = null);

public interface IImportCsvFormatDetector
{
    // Consumes only the first nonblank bounded CSV record, leaves the stream open.
    // Throws CsvImportLimitException for resource limits; routing does not validate data rows.
    Task<ImportCsvFormatResult> DetectAsync(Stream source, CancellationToken cancellationToken = default);
}
