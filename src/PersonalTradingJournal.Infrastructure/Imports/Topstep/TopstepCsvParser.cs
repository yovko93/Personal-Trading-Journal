using System.Globalization;
using System.Text;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Imports.Csv;

namespace PersonalTradingJournal.Infrastructure.Imports.Topstep;

public sealed class TopstepCsvParser : ITopstepCsvParser
{
    private const string TimestampFormat = "MM/dd/yyyy HH:mm:ss zzz";
    private static readonly string[] RequiredHeaders =
    [
        "Id", "ContractName", "EnteredAt", "ExitedAt", "EntryPrice", "ExitPrice",
        "Fees", "PnL", "Size", "Type", "TradeDay", "TradeDuration", "Commissions",
    ];

    public async Task<TopstepCsvParseResult> ParseAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The CSV source stream must be readable.", nameof(source));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string content;
        try
        {
            using var reader = new StreamReader(source,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return Failure(TopstepCsvDiagnosticCodes.InvalidEncoding, "The source is not valid UTF-8 text.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        List<CsvRecord> records = CsvRecordReader.ReadAll(content, cancellationToken)
            .Where(record => !record.IsBlank).ToList();
        if (records.Count == 0)
        {
            return Failure(TopstepCsvDiagnosticCodes.EmptyInput, "Select a non-empty Topstep CSV export.");
        }

        var diagnostics = new List<TopstepCsvDiagnostic>();
        CsvRecord header = records[0];
        Dictionary<string, int>? headers = ValidateHeader(header, diagnostics, cancellationToken);
        int sourceCount = records.Count - 1;
        if (headers is null)
        {
            return new TopstepCsvParseResult([], diagnostics, sourceCount, sourceCount, false);
        }

        if (sourceCount == 0)
        {
            diagnostics.Add(new(TopstepCsvDiagnosticSeverity.Error, TopstepCsvDiagnosticCodes.NoDataRows,
                null, header.StartLineNumber, null, "The Topstep header has no data rows. Select an export containing rows."));
        }

        var rows = new List<TopstepSourceRow>();
        var identities = new Dictionary<string, List<SourceIdentity>>(StringComparer.Ordinal);
        for (int index = 1; index < records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CsvRecord record = records[index];
            if (record.ErrorMessage is not null || record.Fields.Count != header.Fields.Count)
            {
                Error(diagnostics, TopstepCsvDiagnosticCodes.InvalidRowShape, index, record.StartLineNumber,
                    null, record.ErrorMessage ?? $"Expected {header.Fields.Count} fields but found {record.Fields.Count}.");
                continue;
            }

            string[] fields = RequiredHeaders.Select(name => record.Fields[headers[name]].Trim()).ToArray();
            if (fields[0].Length > 0)
            {
                if (!identities.TryGetValue(fields[0], out List<SourceIdentity>? occurrences))
                {
                    occurrences = [];
                    identities.Add(fields[0], occurrences);
                }

                // Include well-shaped rows with invalid values: a conflicting ID must not
                // silently become usable just because one occurrence failed field validation.
                occurrences.Add(new SourceIdentity(index, record.StartLineNumber, fields));
            }

            TopstepSourceRow? row = ParseRow(fields, index, record.StartLineNumber, diagnostics);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        var repeatedRecords = new HashSet<int>();
        foreach (List<SourceIdentity> occurrences in identities.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (occurrences.Count < 2)
            {
                continue;
            }

            bool conflict = false;
            foreach (SourceIdentity occurrence in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                conflict |= !occurrence.Fields.SequenceEqual(occurrences[0].Fields, StringComparer.Ordinal);
            }

            foreach (SourceIdentity occurrence in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                repeatedRecords.Add(occurrence.RecordIndex);
                int otherLine = occurrence == occurrences[0] ? occurrences[1].LineNumber : occurrences[0].LineNumber;
                Error(diagnostics, conflict ? TopstepCsvDiagnosticCodes.ConflictingId : TopstepCsvDiagnosticCodes.DuplicateId,
                    occurrence.RecordIndex, occurrence.LineNumber, "Id",
                    $"This Id also occurs at source line {otherLine}" +
                    (conflict ? " with different reported fields. Resolve the conflicting rows and retry." :
                        ". Remove the duplicate source rows and retry.") + " All occurrences are rejected.");
            }
        }

        rows.RemoveAll(row => repeatedRecords.Contains(row.SourceRecordIndex));
        cancellationToken.ThrowIfCancellationRequested();
        return new TopstepCsvParseResult(rows, diagnostics, sourceCount, sourceCount - rows.Count, true);
    }

    private static Dictionary<string, int>? ValidateHeader(
        CsvRecord header, List<TopstepCsvDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        if (header.ErrorMessage is not null)
        {
            Error(diagnostics, TopstepCsvDiagnosticCodes.InvalidHeader, null, header.StartLineNumber, null, header.ErrorMessage);
            return null;
        }

        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < header.Fields.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = header.Fields[index].Trim();
            bool known = RequiredHeaders.Contains(name, StringComparer.Ordinal);
            if (name.Length == 0 || !headers.TryAdd(name, index))
            {
                Error(diagnostics, name.Length == 0 ? TopstepCsvDiagnosticCodes.InvalidHeader : TopstepCsvDiagnosticCodes.DuplicateHeader,
                    null, header.StartLineNumber, known ? name : null,
                    $"Header column {index + 1} is empty or repeated. Use unique Topstep column names.");
            }
            else if (!known)
            {
                // Do not echo unknown header content; it could be an accidentally selected data row.
                diagnostics.Add(new(TopstepCsvDiagnosticSeverity.Warning, TopstepCsvDiagnosticCodes.AdditionalHeader,
                    null, header.StartLineNumber, null, $"Additional header column {index + 1} is ignored."));
            }
        }

        foreach (string name in RequiredHeaders)
        {
            if (!headers.ContainsKey(name))
            {
                Error(diagnostics, TopstepCsvDiagnosticCodes.MissingHeader, null, header.StartLineNumber, name,
                    $"Required Topstep header '{name}' is missing (names are case-sensitive). Select a Topstep trades export.");
            }
        }

        return diagnostics.Any(d => d.Severity == TopstepCsvDiagnosticSeverity.Error) ? null : headers;
    }

    private static TopstepSourceRow? ParseRow(
        string[] fields, int recordIndex, int lineNumber, List<TopstepCsvDiagnostic> diagnostics)
    {
        int before = diagnostics.Count;
        void Invalid(string code, int column, string message) =>
            Error(diagnostics, code, recordIndex, lineNumber, RequiredHeaders[column], message);

        for (int column = 0; column < fields.Length; column++)
        {
            if (fields[column].Length == 0)
            {
                Invalid(TopstepCsvDiagnosticCodes.RequiredValue, column, $"{RequiredHeaders[column]} is required.");
            }
        }

        decimal Number(int column)
        {
            if (TryParseExactDecimal(fields[column], out decimal value))
            {
                return value;
            }

            if (fields[column].Length > 0)
            {
                Invalid(TopstepCsvDiagnosticCodes.InvalidDecimal, column,
                    "Use an exact System.Decimal value with a dot separator, without currency symbols or digit grouping.");
            }

            return 0m;
        }

        DateTimeOffset? Timestamp(int column)
        {
            if (DateTimeOffset.TryParseExact(fields[column], TimestampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTimeOffset value))
            {
                return value;
            }

            if (fields[column].Length > 0)
            {
                Invalid(TopstepCsvDiagnosticCodes.InvalidTimestamp, column,
                    $"Use {TimestampFormat} with an explicit numeric UTC offset.");
            }

            return null;
        }

        DateTimeOffset? entered = Timestamp(2);
        DateTimeOffset? exited = Timestamp(3);
        decimal entryPrice = Number(4);
        decimal exitPrice = Number(5);
        decimal fees = Number(6);
        decimal pnl = Number(7);
        int beforeSize = diagnostics.Count;
        decimal size = Number(8);
        if (diagnostics.Count == beforeSize && fields[8].Length > 0 && size <= 0m)
        {
            Invalid(TopstepCsvDiagnosticCodes.InvalidSize, 8, "Size must be greater than zero.");
        }

        TopstepTradeType? type = fields[9] switch
        {
            "Long" => TopstepTradeType.Long,
            "Short" => TopstepTradeType.Short,
            _ => null,
        };
        if (type is null && fields[9].Length > 0)
        {
            Invalid(TopstepCsvDiagnosticCodes.UnsupportedType, 9, "Type must be Long or Short (case-sensitive).");
        }

        DateTimeOffset? tradeDay = Timestamp(10);
        bool durationValid = TimeSpan.TryParseExact(fields[11], "c", CultureInfo.InvariantCulture, out TimeSpan duration);
        if ((!durationValid || duration < TimeSpan.Zero) && fields[11].Length > 0)
        {
            Invalid(TopstepCsvDiagnosticCodes.InvalidDuration, 11,
                "TradeDuration must be a non-negative invariant [d.]hh:mm:ss[.fffffff] duration.");
        }

        decimal commissions = Number(12);
        if (entered.HasValue && exited.HasValue && exited.Value < entered.Value)
        {
            Invalid(TopstepCsvDiagnosticCodes.TimestampOrder, 3, "ExitedAt must not precede EnteredAt when compared as UTC instants.");
        }

        if (diagnostics.Count != before)
        {
            return null;
        }

        return new TopstepSourceRow(recordIndex, lineNumber, fields[0], fields[1], entered!.Value, exited!.Value,
            entryPrice, exitPrice, fees, pnl, size, type!.Value, tradeDay!.Value, duration, fields[11], commissions);
    }

    private static bool TryParseExactDecimal(string text, out decimal value)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        // Decimal.TryParse can silently round over-precise values (including a
        // tiny nonzero amount to zero). Reject those instead of changing source facts.
        bool negative = text.StartsWith('-');
        string magnitude = text.TrimStart('+', '-');
        if (magnitude.Contains('.'))
        {
            magnitude = magnitude.TrimEnd('0').TrimEnd('.');
        }

        magnitude = magnitude.TrimStart('0');
        if (magnitude.StartsWith('.'))
        {
            magnitude = "0" + magnitude;
        }

        if (magnitude.Length == 0)
        {
            magnitude = "0";
        }

        string canonical = negative && magnitude != "0" ? "-" + magnitude : magnitude;
        return canonical == value.ToString("0.############################", CultureInfo.InvariantCulture);
    }

    private static void Error(List<TopstepCsvDiagnostic> diagnostics, string code,
        int? recordIndex, int? lineNumber, string? field, string message) =>
        diagnostics.Add(new(TopstepCsvDiagnosticSeverity.Error, code, recordIndex, lineNumber, field, message));

    private static TopstepCsvParseResult Failure(string code, string message) =>
        new([], [new(TopstepCsvDiagnosticSeverity.Error, code, null, null, null, message)], 0, 0, false);

    private sealed record SourceIdentity(int RecordIndex, int LineNumber, string[] Fields);
}
