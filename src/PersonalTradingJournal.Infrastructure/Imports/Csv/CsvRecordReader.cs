using System.Runtime.CompilerServices;
using System.Text;
using PersonalTradingJournal.Application.Imports;

namespace PersonalTradingJournal.Infrastructure.Imports.Csv;

internal sealed record CsvRecord(
    IReadOnlyList<string> Fields,
    int StartLineNumber,
    bool IsBlank,
    string? ErrorMessage);

internal static class CsvRecordReader
{
    public static async IAsyncEnumerable<CsvRecord> ReadAsync(
        Stream source,
        bool headerOnly = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var limited = new CsvLimitedReadStream(source,
            headerOnly ? CsvImportLimits.HeaderBytes : CsvImportLimits.SourceBytes, headerOnly);
        // Strict UTF-8 only; strip its decoded BOM explicitly, without enabling UTF-16 auto-detection.
        using var reader = new StreamReader(limited, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false, bufferSize: 4096);
        var fields = new List<string>();
        var field = new StringBuilder();
        var characterBuffer = new char[1];
        FieldState state = FieldState.Start;
        int lineNumber = 1, recordStartLine = 1, recordLength = 0, recordCount = 0;
        bool recordStarted = false, hasCsvSyntax = false, hasNonWhitespace = false;
        bool firstCharacter = true, previousCr = false;
        string? errorMessage = null;

        while (await reader.ReadAsync(characterBuffer.AsMemory(), cancellationToken).ConfigureAwait(false) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            char character = characterBuffer[0];
            if (firstCharacter)
            {
                firstCharacter = false;
                if (character == '\uFEFF') continue;
            }
            if (previousCr && character == '\n')
            {
                previousCr = false;
                if (state == FieldState.Quoted) { CountCharacter(); Append(character); }
                continue;
            }
            previousCr = character == '\r';
            if (character is '\r' or '\n')
            {
                if (state == FieldState.Quoted)
                {
                    CountCharacter();
                    Append(character);
                    lineNumber++;
                    continue;
                }

                CsvRecord record = EndRecord();
                lineNumber++;
                recordStartLine = lineNumber;
                yield return record;
                if (headerOnly && !record.IsBlank) yield break;
                continue;
            }

            CountCharacter();
            recordStarted = true;
            switch (state)
            {
                case FieldState.Start:
                    if (character == ',') { EndField(separator: true); hasCsvSyntax = true; }
                    else if (character == '"') { state = FieldState.Quoted; hasCsvSyntax = true; }
                    else { Append(character); hasNonWhitespace |= !char.IsWhiteSpace(character); state = FieldState.Unquoted; }
                    break;
                case FieldState.Unquoted:
                    if (character == ',')
                    {
                        EndField(separator: true);
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
                        Append(character);
                        hasNonWhitespace |= !char.IsWhiteSpace(character);
                    }
                    break;
                case FieldState.Quoted:
                    if (character == '"') state = FieldState.AfterQuoted;
                    else { Append(character); hasNonWhitespace |= !char.IsWhiteSpace(character); }
                    break;
                case FieldState.AfterQuoted:
                    if (character == '"') { Append('"'); hasNonWhitespace = true; state = FieldState.Quoted; }
                    else if (character == ',') { EndField(separator: true); state = FieldState.Start; }
                    else
                    {
                        errorMessage ??= "Unexpected content followed a closing quote.";
                        Append(character);
                        hasNonWhitespace |= !char.IsWhiteSpace(character);
                        state = FieldState.Unquoted;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported CSV parser state.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (state == FieldState.Quoted) errorMessage ??= "A quoted field was not closed.";
        if (recordStarted || fields.Count > 0 || field.Length > 0) yield return EndRecord();

        void CountCharacter()
        {
            if (++recordLength > CsvImportLimits.RecordCharacters)
                throw new CsvImportLimitException("characters per CSV record", CsvImportLimits.RecordCharacters, recordStartLine);
        }
        void Append(char character)
        {
            if (field.Length == CsvImportLimits.FieldCharacters)
                throw new CsvImportLimitException("characters per field", CsvImportLimits.FieldCharacters, recordStartLine);
            field.Append(character);
        }
        void EndField(bool separator = false)
        {
            // A separator promises another field, including a trailing empty field.
            if (fields.Count >= CsvImportLimits.FieldsPerRecord - (separator ? 1 : 0))
                throw new CsvImportLimitException("fields per CSV record", CsvImportLimits.FieldsPerRecord, recordStartLine);
            fields.Add(field.ToString());
            field.Clear();
        }
        CsvRecord EndRecord()
        {
            if (++recordCount > CsvImportLimits.Records)
                throw new CsvImportLimitException("CSV records including header and blanks", CsvImportLimits.Records, recordStartLine);
            EndField();
            var record = new CsvRecord(fields.ToArray(), recordStartLine, !hasCsvSyntax && !hasNonWhitespace, errorMessage);
            fields.Clear();
            field.Clear();
            state = FieldState.Start;
            recordStarted = false;
            hasCsvSyntax = false;
            hasNonWhitespace = false;
            errorMessage = null;
            recordLength = 0;
            return record;
        }
    }

    public static async Task<List<CsvRecord>> ReadNonBlankAsync(Stream source, CancellationToken cancellationToken)
    {
        var records = new List<CsvRecord>();
        await foreach (CsvRecord record in ReadAsync(source, cancellationToken: cancellationToken).ConfigureAwait(false))
            if (!record.IsBlank) records.Add(record);
        return records;
    }

    private enum FieldState { Start, Unquoted, Quoted, AfterQuoted }
}

