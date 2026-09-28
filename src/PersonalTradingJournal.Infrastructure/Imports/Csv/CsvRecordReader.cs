using System.Text;

namespace PersonalTradingJournal.Infrastructure.Imports.Csv;

internal sealed record CsvRecord(
    IReadOnlyList<string> Fields,
    int StartLineNumber,
    bool IsBlank,
    string? ErrorMessage);

internal static class CsvRecordReader
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

