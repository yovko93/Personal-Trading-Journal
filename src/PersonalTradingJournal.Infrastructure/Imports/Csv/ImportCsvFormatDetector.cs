using System.Text;
using PersonalTradingJournal.Application.Imports;

namespace PersonalTradingJournal.Infrastructure.Imports.Csv;

public sealed class ImportCsvFormatDetector : IImportCsvFormatDetector
{
    private static readonly string[] Topstep = ["Id", "ContractName", "EnteredAt", "ExitedAt", "EntryPrice", "ExitPrice", "Fees", "PnL", "Size", "Type", "TradeDay", "TradeDuration", "Commissions"];
    private static readonly string[] Tradovate = ["symbol", "_priceFormat", "_priceFormatType", "_tickSize", "buyFillId", "sellFillId", "qty", "buyPrice", "sellPrice", "pnl", "boughtTimestamp", "soldTimestamp", "duration"];

    public async Task<ImportCsvFormatResult> DetectAsync(Stream source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            CsvRecord? header = null;
            await foreach (CsvRecord record in CsvRecordReader.ReadAsync(source, headerOnly: true, cancellationToken).ConfigureAwait(false))
            {
                if (record.IsBlank) continue;
                header = record;
                break;
            }
            if (header is null) return Unknown(null);
            string[] fields = header.Fields.Select(f => f.Trim()).ToArray();
            var names = fields.ToHashSet(StringComparer.Ordinal);
            if (header.ErrorMessage is not null || fields.Any(string.IsNullOrEmpty) || names.Count != fields.Length)
                return Unknown(header.StartLineNumber);
            bool topstep = Topstep.All(names.Contains) && !Tradovate.Any(names.Contains);
            bool tradovate = Tradovate.All(names.Contains) && !Topstep.Any(names.Contains);
            return topstep ? new(ImportCsvFormat.Topstep) : tradovate ? new(ImportCsvFormat.Tradovate) : Unknown(header.StartLineNumber);
        }
        catch (DecoderFallbackException) { return Unknown(null); }
    }

    private static ImportCsvFormatResult Unknown(int? line) => new(ImportCsvFormat.Unknown,
        "Unsupported, incomplete or mixed CSV header. Select a Topstep closed-trades export or a Tradovate matched-fills export with its original complete header.", line);
}
