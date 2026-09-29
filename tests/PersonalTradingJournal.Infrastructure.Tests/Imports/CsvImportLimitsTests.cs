using System.Text;
using PersonalTradingJournal.Application.Imports;
using PersonalTradingJournal.Infrastructure.Imports.Csv;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports;

public sealed class CsvImportLimitsTests
{
    private const string TopstepRow = "SYNTH-1,MNQZ6,07/10/2026 17:00:00 +03:00,07/10/2026 17:01:00 +03:00,20000,20001,0.72,2,1,Long,07/10/2026 00:00:00 -05:00,00:01:00,0.50";
    private const string TradovateRow = Tradovate.TradovateCsvFixtures.LongLikeRow;
    private static string Header(bool topstep) => topstep ? ImportCsvFormatDetectorTests.TopstepHeader : ImportCsvFormatDetectorTests.TradovateHeader;
    private static string Row(bool topstep) => topstep ? TopstepRow : TradovateRow;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidBomSourceCanBeDetectedAndParsedWithoutSeeking(bool topstep)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\uFEFF\n" + Header(topstep) + "\n" + Row(topstep));
        using var input = new MemoryStream(bytes);
        using var nonseekable = new CsvLimitedReadStream(input, CsvImportLimits.SourceBytes);
        Assert.False(nonseekable.CanSeek);
        var format = await new ImportCsvFormatDetector().DetectAsync(nonseekable);
        Assert.Equal(topstep ? ImportCsvFormat.Topstep : ImportCsvFormat.Tradovate, format.Format);
        Assert.True(input.Position < bytes.Length);
        // The parser receives a separate complete nonseekable stream; detection never owns/reopens it.
        using var complete = new MemoryStream(bytes);
        using var completeNonseekable = new CsvLimitedReadStream(complete, CsvImportLimits.SourceBytes);
        Assert.Equal(1, (await Parse(topstep, completeNonseekable)).Accepted);
        Assert.True(complete.CanRead);
    }

    [Theory]
    [InlineData("fields")]
    [InlineData("field")]
    [InlineData("records")]
    public async Task HeaderStructuralLimitIsNotReportedAsUnknownSchema(string dimension)
    {
        string input = dimension switch
        {
            "fields" => new string(',', CsvImportLimits.FieldsPerRecord),
            "field" => "\"" + new string('x', CsvImportLimits.FieldCharacters + 1),
            _ => new string('\n', CsvImportLimits.Records) + ImportCsvFormatDetectorTests.TopstepHeader,
        };
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(input));
        var error = await Assert.ThrowsAsync<CsvImportLimitException>(() => new ImportCsvFormatDetector().DetectAsync(source));
        Assert.Contains("CSV exceeds the supported limit", error.Message);
        Assert.DoesNotContain(input, error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CountedSnapshotAcceptsExactByteLimitAndRejectsNextByteWithoutSeeking(int over)
    {
        using var source = new GeneratedStream(CsvImportLimits.SourceBytes + over, (byte)'x');
        if (over == 0)
        {
            using var snapshot = await CsvImportLimits.ReadSnapshotAsync(source);
            Assert.Equal(CsvImportLimits.SourceBytes, snapshot.Length);
            Assert.Equal(0, snapshot.Position);
        }
        else
        {
            var error = await Assert.ThrowsAsync<CsvImportLimitException>(() => CsvImportLimits.ReadSnapshotAsync(source));
            Assert.Equal("source bytes", error.Dimension);
        }
        Assert.Equal(CsvImportLimits.SourceBytes + over, source.BytesRead);
        Assert.True(source.CanRead); // Caller retains ownership even on failure.
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task CompleteParsersEnforceByteLimitIncludingBlankInput(bool topstep, int over)
    {
        // Short blank records keep all other limits below their ceilings (no giant input string).
        using var source = new GeneratedStream(CsvImportLimits.SourceBytes + over, (byte)' ', newlineEvery: 1024);
        var result = await Parse(topstep, source);
        Assert.Equal(over == 1, result.Codes.Contains(CsvImportLimitException.DiagnosticCode));
        Assert.Equal(0, result.Accepted);
        Assert.Equal(CsvImportLimits.SourceBytes + over, source.BytesRead);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task FieldCountIncludesEmptyFieldsAndRejectsCommaDenseRows(bool topstep, int over)
    {
        int extras = CsvImportLimits.FieldsPerRecord - 13 + over;
        string csv = Header(topstep) + string.Concat(Enumerable.Range(0, extras).Select(i => $",Extra{i}")) + "\n" +
            Row(topstep) + new string(',', extras);
        var result = await Parse(topstep, csv);
        Assert.Equal(over == 1, result.Codes.Contains(CsvImportLimitException.DiagnosticCode));
        Assert.Equal(over == 0 ? 1 : 0, result.Accepted);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task QuotedMultilineFieldHasExactDecodedCharacterBound(bool topstep, int over)
    {
        string field = new string('x', CsvImportLimits.FieldCharacters - 3 + over) + "\r\ny";
        var result = await Parse(topstep, Header(topstep) + ",Comment\n" + Row(topstep) + ",\"" + field + "\"");
        Assert.Equal(over == 1, result.Codes.Contains(CsvImportLimitException.DiagnosticCode));
        Assert.Equal(over == 0 ? 1 : 0, result.Accepted);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task RawRecordLengthIncludesDelimitersAndQuotes(bool topstep, int over)
    {
        string row = Row(topstep) + string.Concat(Enumerable.Repeat(",\"" + new string('x', CsvImportLimits.FieldCharacters) + "\"", 3)) + ",";
        row += new string('y', CsvImportLimits.RecordCharacters - row.Length + over);
        Assert.Equal(CsvImportLimits.RecordCharacters + over, row.Length);
        var result = await Parse(topstep, Header(topstep) + ",A,B,C,D\n" + row);
        Assert.Equal(over == 1, result.Codes.Contains(CsvImportLimitException.DiagnosticCode));
        Assert.Equal(over == 0 ? 1 : 0, result.Accepted);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task BlankRecordsAndHeaderCountTowardRecordLimit(bool topstep, int over)
    {
        string csv = Header(topstep) + "\n" + new string('\n', CsvImportLimits.Records - 2 + over) + Row(topstep);
        var result = await Parse(topstep, csv);
        Assert.Equal(over == 1, result.Codes.Contains(CsvImportLimitException.DiagnosticCode));
        Assert.Equal(over == 0 ? 1 : 0, result.Accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommaBombStopsBeforeMaterializingTheSource(bool topstep)
    {
        using var source = new GeneratedStream(1_000_000_000, (byte)',');
        var result = await Parse(topstep, source);
        Assert.Contains(CsvImportLimitException.DiagnosticCode, result.Codes);
        Assert.InRange(source.BytesRead, 1, 4096); // At most one decoder buffer, not a billion-character string/list.
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task HeaderBudgetIncludesLeadingBlanksAndDoesNotReadDataRows(int over)
    {
        string header = ImportCsvFormatDetectorTests.TopstepHeader + "\n";
        int blankBytes = CsvImportLimits.HeaderBytes - Encoding.UTF8.GetByteCount(header) + over;
        string blanks = string.Concat(Enumerable.Repeat(new string(' ', 1023) + "\n", blankBytes / 1024));
        int remaining = blankBytes % 1024;
        if (remaining > 0) blanks += new string(' ', remaining - 1) + "\n";
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(blanks + header + "data must not be read"));
        if (over == 0)
            Assert.Equal(ImportCsvFormat.Topstep, (await new ImportCsvFormatDetector().DetectAsync(source)).Format);
        else
            await Assert.ThrowsAsync<CsvImportLimitException>(() => new ImportCsvFormatDetector().DetectAsync(source));
        Assert.Equal(CsvImportLimits.HeaderBytes + over, source.Position);
    }

    [Fact]
    public async Task DetectorDoesNotDecodeInvalidUtf8InDataButParsersRejectIt()
    {
        byte[] header = Encoding.UTF8.GetBytes(ImportCsvFormatDetectorTests.TopstepHeader + "\n");
        using var source = new MemoryStream([.. header, 0xff]);
        Assert.Equal(ImportCsvFormat.Topstep, (await new ImportCsvFormatDetector().DetectAsync(source)).Format);
        Assert.Equal(header.Length, source.Position);
        foreach (bool topstep in new[] { false, true })
        {
            source.Position = 0;
            var result = await Parse(topstep, source);
            Assert.Contains("INVALID_ENCODING", result.Codes);
            Assert.Equal(0, result.Accepted);
        }
        using var invalidHeader = new MemoryStream([0xff]);
        Assert.Equal(ImportCsvFormat.Unknown, (await new ImportCsvFormatDetector().DetectAsync(invalidHeader)).Format);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("header")]
    [InlineData("topstep")]
    [InlineData("tradovate")]
    public async Task CancellationDuringNonseekableReadPropagates(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        using var source = new GeneratedStream(CsvImportLimits.SourceBytes * 2L, (byte)' ', cancel: cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (operation == "snapshot") { using var snapshot = await CsvImportLimits.ReadSnapshotAsync(source, cancellation.Token); }
            else if (operation == "header") await new ImportCsvFormatDetector().DetectAsync(source, cancellation.Token);
            else await Parse(operation == "topstep", source, cancellation.Token);
        });
        Assert.True(source.CanRead);
        Assert.InRange(source.BytesRead, 1, 16 * 1024);
    }

    private static async Task<(int Accepted, string[] Codes)> Parse(bool topstep, string csv)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await Parse(topstep, source);
    }
    private static async Task<(int Accepted, string[] Codes)> Parse(bool topstep, Stream source, CancellationToken token = default)
    {
        if (topstep)
        {
            var result = await new TopstepCsvParser().ParseAsync(source, token);
            return (result.ValidRecordCount, result.Diagnostics.Select(d => d.Code).ToArray());
        }
        var tradovate = await new TradovateCsvParser().ParseAsync(source, token);
        return (tradovate.ValidRecordCount, tradovate.Diagnostics.Select(d => d.Code).ToArray());
    }

    private sealed class GeneratedStream(long length, byte value, int newlineEvery = 0, CancellationTokenSource? cancel = null) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Fill(buffer.AsSpan(offset, count));
        private int Fill(Span<byte> buffer)
        {
            int count = (int)Math.Min(buffer.Length, length - BytesRead);
            for (int i = 0; i < count; i++) buffer[i] = newlineEvery > 0 && (BytesRead + i + 1) % newlineEvery == 0 ? (byte)'\n' : value;
            BytesRead += count;
            cancel?.Cancel();
            return count;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Fill(buffer.Span));
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
