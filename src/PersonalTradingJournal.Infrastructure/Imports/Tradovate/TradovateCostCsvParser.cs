using System.Globalization;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Imports.Csv;

namespace PersonalTradingJournal.Infrastructure.Imports.Tradovate;

public sealed class TradovateCostCsvParser : ITradovateCostCsvParser
{
    public async Task<IReadOnlyList<TradovateCostFill>> ParseFillsAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var rows = await ReadAsync(source, ["_id", "Fill ID", "Account", "Contract", "B/S", "Quantity", "Price",
            "Timestamp", "_timestamp", "_tradeDate", "commission", "_active"], cancellationToken);
        return rows.Select(r =>
        {
            if (r["_id"] != r["Fill ID"] || r["_active"] != "true") throw Invalid();
            ExecutionSide side = r["B/S"] switch { "Buy" => ExecutionSide.Buy, "Sell" => ExecutionSide.Sell, _ => throw Invalid() };
            if (!DateTimeOffset.TryParseExact(r["_timestamp"], ["yyyy-MM-dd HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd HH:mm:ss'Z'"],
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)) throw Invalid();
            return new TradovateCostFill(r["Fill ID"], r["Account"], r["Contract"], side, Number(r["Quantity"]),
                Number(r["Price"]), Timestamp(r["Timestamp"]), utc, Date(r["_tradeDate"]), Number(r["commission"]));
        }).ToArray();
    }

    public async Task<IReadOnlyList<TradovateCashEntry>> ParseCashAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var rows = await ReadAsync(source, ["Account", "Transaction ID", "Timestamp", "Date", "Delta", "Amount",
            "Cash Change Type", "Currency", "Contract"], cancellationToken);
        return rows.Select(r => new TradovateCashEntry(r["Transaction ID"], r["Account"], r["Currency"], r["Contract"],
            Timestamp(r["Timestamp"]), Date(r["Date"]), r["Cash Change Type"], Number(r["Delta"]), Number(r["Amount"]))).ToArray();
    }

    private static async Task<IReadOnlyList<Dictionary<string, string>>> ReadAsync(Stream source, string[] required, CancellationToken token)
    {
        var records = await CsvRecordReader.ReadNonBlankAsync(source, token);
        if (records.Count < 2 || records.Any(r => r.ErrorMessage is not null)) throw Invalid();
        var header = records[0].Fields;
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Count || header.Any(string.IsNullOrWhiteSpace) ||
            required.Any(h => !header.Contains(h))) throw Invalid();
        var result = new List<Dictionary<string, string>>();
        foreach (var record in records.Skip(1))
        {
            token.ThrowIfCancellationRequested();
            if (record.Fields.Count != header.Count) throw Invalid();
            var values = header.Select((h, i) => (h, Value: record.Fields[i].Trim())).ToDictionary(v => v.h, v => v.Value, StringComparer.Ordinal);
            if (required.Any(h => values[h].Length == 0)) throw Invalid();
            result.Add(values);
        }
        return result;
    }

    private static decimal Number(string text) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : throw Invalid();
    private static DateTime Timestamp(string text) => DateTime.TryParseExact(text, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var value) ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified) : throw Invalid();
    private static DateOnly Date(string text) => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var value) ? value : throw Invalid();
    private static TradovateCostSourceException Invalid() => new("Invalid or unsupported Fills/Cash History CSV. Export complete unmodified sources with the expected headers, dates and numeric fields.");
}
