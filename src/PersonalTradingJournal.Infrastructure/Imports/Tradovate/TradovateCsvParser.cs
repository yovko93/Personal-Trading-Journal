using System.Globalization;
using System.Text;
using PersonalTradingJournal.Application.Imports.Tradovate;

namespace PersonalTradingJournal.Infrastructure.Imports.Tradovate;

public sealed class TradovateCsvParser : ITradovateCsvParser
{
    private const string TimestampFormat = "MM/dd/yyyy HH:mm:ss";

    private static readonly string[] RequiredHeaders =
    [
        "symbol",
        "_priceFormat",
        "_priceFormatType",
        "_tickSize",
        "buyFillId",
        "sellFillId",
        "qty",
        "buyPrice",
        "sellPrice",
        "pnl",
        "boughtTimestamp",
        "soldTimestamp",
        "duration",
    ];

    public async Task<TradovateCsvParseResult> ParseAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The CSV source stream must be readable.", nameof(source));
        }

        string content;
        try
        {
            using var reader = new StreamReader(
                source,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true);
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            return Failure(
                TradovateCsvDiagnosticCodes.InvalidEncoding,
                "The source is not valid UTF-8 text.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CsvRecord> records = CsvRecordReader.ReadAll(
            content,
            cancellationToken);
        List<CsvRecord> nonBlankRecords = records
            .Where(record => !record.IsBlank)
            .ToList();
        if (nonBlankRecords.Count == 0)
        {
            return Failure(
                TradovateCsvDiagnosticCodes.EmptyInput,
                "The CSV source is empty.");
        }

        CsvRecord headerRecord = nonBlankRecords[0];
        List<CsvRecord> dataRecords = nonBlankRecords.Skip(1).ToList();
        var diagnostics = new List<TradovateCsvDiagnostic>();
        Dictionary<string, int>? headerMap = ValidateHeader(
            headerRecord,
            diagnostics);
        if (headerMap is null)
        {
            return new TradovateCsvParseResult(
                [],
                diagnostics,
                dataRecords.Count,
                dataRecords.Count,
                isHeaderUsable: false);
        }

        var rows = new List<TradovateMatchedFillRow>(dataRecords.Count);
        int rejectedRecordCount = 0;
        for (int index = 0; index < dataRecords.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int sourceRecordIndex = index + 1;
            CsvRecord record = dataRecords[index];
            int diagnosticsBefore = diagnostics.Count;

            if (record.ErrorMessage is not null)
            {
                AddError(
                    diagnostics,
                    TradovateCsvDiagnosticCodes.InvalidRowShape,
                    sourceRecordIndex,
                    record.StartLineNumber,
                    fieldName: null,
                    record.ErrorMessage);
            }
            else if (record.Fields.Count != headerRecord.Fields.Count)
            {
                AddError(
                    diagnostics,
                    TradovateCsvDiagnosticCodes.InvalidRowShape,
                    sourceRecordIndex,
                    record.StartLineNumber,
                    fieldName: null,
                    $"Expected {headerRecord.Fields.Count} fields but found {record.Fields.Count}.");
            }
            else
            {
                TradovateMatchedFillRow? row = ParseRow(
                    record,
                    sourceRecordIndex,
                    headerMap,
                    diagnostics);
                if (row is not null)
                {
                    rows.Add(row);
                }
            }

            if (diagnostics.Count > diagnosticsBefore)
            {
                rejectedRecordCount++;
            }
        }

        return new TradovateCsvParseResult(
            rows,
            diagnostics,
            dataRecords.Count,
            rejectedRecordCount,
            isHeaderUsable: true);
    }

    private static Dictionary<string, int>? ValidateHeader(
        CsvRecord header,
        List<TradovateCsvDiagnostic> diagnostics)
    {
        if (header.ErrorMessage is not null)
        {
            AddError(
                diagnostics,
                TradovateCsvDiagnosticCodes.InvalidHeader,
                sourceRecordIndex: null,
                header.StartLineNumber,
                fieldName: null,
                header.ErrorMessage);
            return null;
        }

        string[] normalizedHeaders = header.Fields
            .Select(value => value.Trim())
            .ToArray();
        for (int index = 0; index < normalizedHeaders.Length; index++)
        {
            if (normalizedHeaders[index].Length == 0)
            {
                AddError(
                    diagnostics,
                    TradovateCsvDiagnosticCodes.EmptyHeader,
                    sourceRecordIndex: null,
                    header.StartLineNumber,
                    fieldName: null,
                    $"Header column {index + 1} is empty.");
            }
        }

        foreach (IGrouping<string, string> duplicate in normalizedHeaders
                     .Where(value => value.Length > 0)
                     .GroupBy(value => value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            AddError(
                diagnostics,
                TradovateCsvDiagnosticCodes.DuplicateHeader,
                sourceRecordIndex: null,
                header.StartLineNumber,
                duplicate.Key,
                $"Header '{duplicate.Key}' appears more than once.");
        }

        var knownHeaders = new HashSet<string>(RequiredHeaders, StringComparer.Ordinal);
        var presentHeaders = new HashSet<string>(
            normalizedHeaders,
            StringComparer.Ordinal);
        foreach (string requiredHeader in RequiredHeaders)
        {
            if (!presentHeaders.Contains(requiredHeader))
            {
                AddError(
                    diagnostics,
                    TradovateCsvDiagnosticCodes.MissingHeader,
                    sourceRecordIndex: null,
                    header.StartLineNumber,
                    requiredHeader,
                    $"Required header '{requiredHeader}' is missing.");
            }
        }

        foreach (string additionalHeader in normalizedHeaders
                     .Where(value => value.Length > 0 && !knownHeaders.Contains(value))
                     .Distinct(StringComparer.Ordinal))
        {
            diagnostics.Add(new TradovateCsvDiagnostic(
                TradovateCsvDiagnosticSeverity.Warning,
                TradovateCsvDiagnosticCodes.AdditionalHeader,
                SourceRecordIndex: null,
                header.StartLineNumber,
                additionalHeader,
                $"Additional header '{additionalHeader}' is ignored."));
        }

        if (diagnostics.Any(diagnostic =>
                diagnostic.Severity == TradovateCsvDiagnosticSeverity.Error))
        {
            return null;
        }

        return normalizedHeaders
            .Select((name, index) => (name, index))
            .ToDictionary(pair => pair.name, pair => pair.index, StringComparer.Ordinal);
    }

    private static TradovateMatchedFillRow? ParseRow(
        CsvRecord record,
        int sourceRecordIndex,
        IReadOnlyDictionary<string, int> headerMap,
        List<TradovateCsvDiagnostic> diagnostics)
    {
        string Get(string fieldName) =>
            record.Fields[headerMap[fieldName]].Trim();

        int diagnosticsBefore = diagnostics.Count;
        string brokerSymbol = Get("symbol");
        if (brokerSymbol.Length == 0)
        {
            AddError(
                diagnostics,
                TradovateCsvDiagnosticCodes.MissingSymbol,
                sourceRecordIndex,
                record.StartLineNumber,
                "symbol",
                "A broker contract symbol is required.");
        }

        string buyFillId = Get("buyFillId");
        ValidateFillId(
            buyFillId,
            "buyFillId",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);
        string sellFillId = Get("sellFillId");
        ValidateFillId(
            sellFillId,
            "sellFillId",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);

        decimal tickSize = ParsePositiveDecimal(
            Get("_tickSize"),
            "_tickSize",
            TradovateCsvDiagnosticCodes.InvalidTickSize,
            "Tick size must be a positive invariant decimal.",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);
        decimal quantity = ParsePositiveDecimal(
            Get("qty"),
            "qty",
            TradovateCsvDiagnosticCodes.InvalidQuantity,
            "Matched quantity must be a positive whole number of futures contracts.",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);
        if (quantity > 0m && quantity != decimal.Truncate(quantity))
        {
            AddError(
                diagnostics,
                TradovateCsvDiagnosticCodes.InvalidQuantity,
                sourceRecordIndex,
                record.StartLineNumber,
                "qty",
                "Matched quantity must be a whole number of futures contracts.");
        }

        decimal buyPrice = ParseDecimal(
            Get("buyPrice"),
            "buyPrice",
            TradovateCsvDiagnosticCodes.InvalidPrice,
            "Buy price must be an invariant decimal.",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);
        decimal sellPrice = ParseDecimal(
            Get("sellPrice"),
            "sellPrice",
            TradovateCsvDiagnosticCodes.InvalidPrice,
            "Sell price must be an invariant decimal.",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);

        decimal sourceReportedPnL = 0m;
        if (!TryParseSourcePnL(Get("pnl"), out sourceReportedPnL))
        {
            AddError(
                diagnostics,
                TradovateCsvDiagnosticCodes.InvalidPnL,
                sourceRecordIndex,
                record.StartLineNumber,
                "pnl",
                "Source-reported P&L must use supported invariant dollar notation.");
        }

        DateTime boughtTimestamp = ParseTimestamp(
            Get("boughtTimestamp"),
            "boughtTimestamp",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);
        DateTime soldTimestamp = ParseTimestamp(
            Get("soldTimestamp"),
            "soldTimestamp",
            sourceRecordIndex,
            record.StartLineNumber,
            diagnostics);

        if (diagnostics.Count != diagnosticsBefore)
        {
            return null;
        }

        return new TradovateMatchedFillRow(
            sourceRecordIndex,
            record.StartLineNumber,
            brokerSymbol,
            Get("_priceFormat"),
            Get("_priceFormatType"),
            tickSize,
            buyFillId,
            sellFillId,
            quantity,
            buyPrice,
            sellPrice,
            sourceReportedPnL,
            boughtTimestamp,
            soldTimestamp,
            Get("duration"));
    }

    private static void ValidateFillId(
        string value,
        string fieldName,
        int sourceRecordIndex,
        int sourceLineNumber,
        List<TradovateCsvDiagnostic> diagnostics)
    {
        if (value.Length == 0)
        {
            AddError(
                diagnostics,
                TradovateCsvDiagnosticCodes.MissingFillId,
                sourceRecordIndex,
                sourceLineNumber,
                fieldName,
                $"{fieldName} is required.");
        }
    }

    private static decimal ParsePositiveDecimal(
        string value,
        string fieldName,
        string diagnosticCode,
        string message,
        int sourceRecordIndex,
        int sourceLineNumber,
        List<TradovateCsvDiagnostic> diagnostics)
    {
        decimal parsed = ParseDecimal(
            value,
            fieldName,
            diagnosticCode,
            message,
            sourceRecordIndex,
            sourceLineNumber,
            diagnostics);
        if (parsed <= 0m && TryParseInvariantDecimal(value, out _))
        {
            AddError(
                diagnostics,
                diagnosticCode,
                sourceRecordIndex,
                sourceLineNumber,
                fieldName,
                message);
        }

        return parsed;
    }

    private static decimal ParseDecimal(
        string value,
        string fieldName,
        string diagnosticCode,
        string message,
        int sourceRecordIndex,
        int sourceLineNumber,
        List<TradovateCsvDiagnostic> diagnostics)
    {
        if (TryParseInvariantDecimal(value, out decimal parsed))
        {
            return parsed;
        }

        AddError(
            diagnostics,
            diagnosticCode,
            sourceRecordIndex,
            sourceLineNumber,
            fieldName,
            message);
        return 0m;
    }

    private static bool TryParseInvariantDecimal(string value, out decimal parsed) =>
        decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out parsed);

    private static DateTime ParseTimestamp(
        string value,
        string fieldName,
        int sourceRecordIndex,
        int sourceLineNumber,
        List<TradovateCsvDiagnostic> diagnostics)
    {
        if (DateTime.TryParseExact(
                value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        }

        AddError(
            diagnostics,
            TradovateCsvDiagnosticCodes.InvalidTimestamp,
            sourceRecordIndex,
            sourceLineNumber,
            fieldName,
            $"Timestamp must use the invariant format {TimestampFormat}.");
        return default;
    }

    private static bool TryParseSourcePnL(string value, out decimal parsed)
    {
        parsed = 0m;
        if (!value.StartsWith('$'))
        {
            return false;
        }

        string numericText = value[1..];
        bool isNegative = false;
        if (numericText.StartsWith('(') && numericText.EndsWith(')'))
        {
            isNegative = true;
            numericText = numericText[1..^1];
        }
        else if (numericText.StartsWith('-'))
        {
            isNegative = true;
            numericText = numericText[1..];
        }

        if (!HasValidInvariantCurrencyNumberSyntax(numericText) ||
            !decimal.TryParse(
                numericText,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out decimal magnitude))
        {
            return false;
        }

        parsed = isNegative ? -magnitude : magnitude;
        return true;
    }

    private static bool HasValidInvariantCurrencyNumberSyntax(string value)
    {
        string[] decimalParts = value.Split('.');
        if (decimalParts.Length > 2 ||
            decimalParts[0].Length == 0 ||
            (decimalParts.Length == 2 &&
             (decimalParts[1].Length == 0 || !decimalParts[1].All(char.IsAsciiDigit))))
        {
            return false;
        }

        string[] integerGroups = decimalParts[0].Split(',');
        if (integerGroups.Length == 1)
        {
            return integerGroups[0].All(char.IsAsciiDigit);
        }

        return integerGroups[0].Length is >= 1 and <= 3 &&
               integerGroups[0].All(char.IsAsciiDigit) &&
               integerGroups.Skip(1).All(group =>
                   group.Length == 3 && group.All(char.IsAsciiDigit));
    }

    private static TradovateCsvParseResult Failure(string code, string message) =>
        new(
            [],
            [new TradovateCsvDiagnostic(
                TradovateCsvDiagnosticSeverity.Error,
                code,
                SourceRecordIndex: null,
                SourceLineNumber: null,
                FieldName: null,
                message)],
            sourceRecordCount: 0,
            rejectedRecordCount: 0,
            isHeaderUsable: false);

    private static void AddError(
        List<TradovateCsvDiagnostic> diagnostics,
        string code,
        int? sourceRecordIndex,
        int? sourceLineNumber,
        string? fieldName,
        string message)
    {
        diagnostics.Add(new TradovateCsvDiagnostic(
            TradovateCsvDiagnosticSeverity.Error,
            code,
            sourceRecordIndex,
            sourceLineNumber,
            fieldName,
            message));
    }

    private sealed record CsvRecord(
        IReadOnlyList<string> Fields,
        int StartLineNumber,
        bool IsBlank,
        string? ErrorMessage);

    private static class CsvRecordReader
    {
        public static IReadOnlyList<CsvRecord> ReadAll(
            string content,
            CancellationToken cancellationToken)
        {
            var records = new List<CsvRecord>();
            var fields = new List<string>();
            var field = new StringBuilder();
            FieldState state = FieldState.Start;
            int lineNumber = 1;
            int recordStartLine = 1;
            bool recordStarted = false;
            bool hasCsvSyntax = false;
            bool hasNonWhitespace = false;
            string? errorMessage = null;

            for (int index = 0; index < content.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                char character = content[index];
                if (character is '\r' or '\n')
                {
                    bool isCrLf = character == '\r' &&
                                  index + 1 < content.Length &&
                                  content[index + 1] == '\n';
                    if (state == FieldState.Quoted)
                    {
                        field.Append(character);
                        if (isCrLf)
                        {
                            field.Append('\n');
                            index++;
                        }

                        lineNumber++;
                        continue;
                    }

                    EndRecord();
                    if (isCrLf)
                    {
                        index++;
                    }

                    lineNumber++;
                    recordStartLine = lineNumber;
                    continue;
                }

                recordStarted = true;
                switch (state)
                {
                    case FieldState.Start:
                        if (character == ',')
                        {
                            fields.Add(string.Empty);
                            hasCsvSyntax = true;
                        }
                        else if (character == '"')
                        {
                            state = FieldState.Quoted;
                            hasCsvSyntax = true;
                        }
                        else
                        {
                            field.Append(character);
                            hasNonWhitespace |= !char.IsWhiteSpace(character);
                            state = FieldState.Unquoted;
                        }

                        break;

                    case FieldState.Unquoted:
                        if (character == ',')
                        {
                            fields.Add(field.ToString());
                            field.Clear();
                            state = FieldState.Start;
                            hasCsvSyntax = true;
                        }
                        else
                        {
                            if (character == '"')
                            {
                                errorMessage ??= "An unexpected quote was found in an unquoted field.";
                                hasCsvSyntax = true;
                            }

                            field.Append(character);
                            hasNonWhitespace |= !char.IsWhiteSpace(character);
                        }

                        break;

                    case FieldState.Quoted:
                        if (character == '"')
                        {
                            state = FieldState.AfterQuoted;
                        }
                        else
                        {
                            field.Append(character);
                            hasNonWhitespace |= !char.IsWhiteSpace(character);
                        }

                        break;

                    case FieldState.AfterQuoted:
                        if (character == '"')
                        {
                            field.Append('"');
                            hasNonWhitespace = true;
                            state = FieldState.Quoted;
                        }
                        else if (character == ',')
                        {
                            fields.Add(field.ToString());
                            field.Clear();
                            state = FieldState.Start;
                        }
                        else
                        {
                            errorMessage ??= "Unexpected content followed a closing quote.";
                            field.Append(character);
                            hasNonWhitespace |= !char.IsWhiteSpace(character);
                            state = FieldState.Unquoted;
                        }

                        break;

                    default:
                        throw new InvalidOperationException("Unsupported CSV parser state.");
                }
            }

            if (state == FieldState.Quoted)
            {
                errorMessage ??= "A quoted field was not closed.";
            }

            if (recordStarted || fields.Count > 0 || field.Length > 0)
            {
                EndRecord();
            }

            return records;

            void EndRecord()
            {
                fields.Add(field.ToString());
                bool isBlank = !hasCsvSyntax && !hasNonWhitespace;
                records.Add(new CsvRecord(
                    fields.ToArray(),
                    recordStartLine,
                    isBlank,
                    errorMessage));

                fields.Clear();
                field.Clear();
                state = FieldState.Start;
                recordStarted = false;
                hasCsvSyntax = false;
                hasNonWhitespace = false;
                errorMessage = null;
            }
        }

        private enum FieldState
        {
            Start,
            Unquoted,
            Quoted,
            AfterQuoted,
        }
    }
}
