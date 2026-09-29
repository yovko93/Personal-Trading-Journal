namespace PersonalTradingJournal.Application.Imports;

/// <summary>Shared resource policy, independent of provider, file metadata and seekability.</summary>
public static class CsvImportLimits
{
    public const int SourceBytes = 16 * 1024 * 1024;
    public const int HeaderBytes = 64 * 1024;
    public const int Records = 50_000; // Includes header and blank records.
    public const int FieldsPerRecord = 32;
    public const int FieldCharacters = 4_096; // Decoded UTF-16 code units.
    public const int RecordCharacters = 16_384; // Includes CSV syntax and embedded newlines.

    /// <summary>Reads the complete stream or throws; never returns a truncated import source.</summary>
    public static async Task<MemoryStream> ReadSnapshotAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var limited = new CsvLimitedReadStream(source, SourceBytes);
        var snapshot = new MemoryStream();
        try
        {
            await limited.CopyToAsync(snapshot, 16 * 1024, cancellationToken).ConfigureAwait(false);
            snapshot.Position = 0;
            return snapshot;
        }
        catch
        {
            snapshot.Dispose();
            throw;
        }
    }
}

public sealed class CsvImportLimitException(string dimension, int maximum, int? sourceLine = null)
    : IOException($"CSV exceeds the supported limit: {dimension} ({maximum:N0})" +
        (sourceLine is { } line ? $" at source line {line}" : "") +
        ". Export a smaller date range or remove unnecessary columns/oversized fields, then select the complete CSV again. No truncated prefix will be imported.")
{
    public const string DiagnosticCode = "CSV_LIMIT_EXCEEDED";
    public string Dimension { get; } = dimension;
    public int Maximum { get; } = maximum;
}

/// <summary>
/// Non-owning, nonseekable counted view. Reads at most the budget plus one byte to detect overflow;
/// checks BEFORE returning that byte to a copier/decoder. Does not trust Length or Position.
/// </summary>
public sealed class CsvLimitedReadStream(Stream source, int maximumBytes, bool headerOnly = false) : Stream
{
    private int _consumed;
    public override bool CanRead => source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    private int Requested(int count) => Math.Min(count, Math.Min(headerOnly ? 1 : 16 * 1024, maximumBytes - _consumed + 1));
    private int Count(int read)
    {
        _consumed += read;
        if (_consumed > maximumBytes)
            throw new CsvImportLimitException(headerOnly ? "header bytes including leading blanks" : "source bytes", maximumBytes);
        return read;
    }
    public override int Read(byte[] buffer, int offset, int count) => Count(source.Read(buffer, offset, Requested(count)));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Count(await source.ReadAsync(buffer[..Requested(buffer.Length)], cancellationToken).ConfigureAwait(false));
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
