using System.Globalization;
using System.Text;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Topstep;

public sealed class TopstepCsvParserTests
{
    private readonly TopstepCsvParser _parser = new();

    [Fact]
    public async Task PreservesExactReportedFieldsAndOneRowPerSourceWithoutGrouping()
    {
        string other = TopstepCsvFixtures.Replace(0, "000SYNTH02");
        other = TopstepCsvFixtures.Replace(9, "Short", other);
        TopstepCsvParseResult result = await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row, other));

        Assert.True(result.IsHeaderUsable);
        Assert.True(result.IsCompleteInputValid);
        Assert.Equal(2, result.SourceRecordCount);
        Assert.Equal(2, result.ValidRecordCount);
        Assert.Equal(0, result.RejectedRecordCount);
        Assert.Empty(result.Diagnostics);
        TopstepSourceRow row = result.Rows[0];
        Assert.Equal("000SYNTH01", row.Id);
        Assert.Equal("MNQZ6", row.ContractName);
        Assert.Equal(20000.125m, row.EntryPrice);
        Assert.Equal(20001.375m, row.ExitPrice);
        Assert.Equal(1.44m, row.SourceReportedFees);
        Assert.Equal(1m, row.SourceReportedCommissions);
        Assert.Equal(5m, row.SourceReportedPnL);
        Assert.Equal(2m, row.Size);
        Assert.Equal(TopstepTradeType.Long, row.Type);
        Assert.Equal(TopstepTradeType.Short, result.Rows[1].Type);
        Assert.Equal([1, 2], result.Rows.Select(r => r.SourceRecordIndex));
        Assert.Equal([2, 3], result.Rows.Select(r => r.SourceLineNumber));
    }

    [Fact]
    public async Task PreservesSourceOffsetsBrokerDateAndSubsecondDurationWithoutDstReinterpretation()
    {
        TopstepSourceRow row = Assert.Single((await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row))).Rows);

        Assert.Equal(TimeSpan.FromHours(3), row.SourceEnteredAt.Offset);
        Assert.Equal(TimeSpan.FromHours(3), row.SourceExitedAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 7, 10, 14, 0, 0, TimeSpan.Zero), row.EnteredAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 10, 14, 0, 2, TimeSpan.Zero), row.ExitedAtUtc);
        Assert.Equal(TimeSpan.Zero, row.EnteredAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, row.ExitedAtUtc.Offset);
        Assert.Equal(new DateOnly(2026, 7, 10), row.BrokerTradingDate);
        Assert.Equal(TimeSpan.FromHours(-5), row.SourceTradeDay.Offset);
        Assert.Equal(0, row.SourceTradeDay.Hour);
        Assert.Equal("00:00:01.1234567", row.SourceDurationText);
        Assert.Equal(11234567, row.SourceReportedDuration.Ticks);
        Assert.NotEqual(row.ExitedAtUtc - row.EnteredAtUtc, row.SourceReportedDuration);
    }

    [Theory]
    [InlineData("07/10/2026 16:00:00 +01:00", true)]
    [InlineData("07/10/2026 18:00:00 +05:00", false)]
    [InlineData("07/10/2026 14:00:00 +00:00", true)]
    public async Task ValidatesOrderUsingInstantsRatherThanWallClock(string exit, bool valid)
    {
        TopstepCsvParseResult result = await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Replace(3, exit)));
        Assert.Equal(valid, result.IsCompleteInputValid);
        if (!valid)
        {
            Assert.Equal(TopstepCsvDiagnosticCodes.TimestampOrder, Assert.Single(result.Diagnostics).Code);
        }
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-12.125", -12.125)]
    [InlineData("+8.75", 8.75)]
    public async Task PnLIsReportedDecimalNotRecalculated(string pnl, double expected)
    {
        TopstepSourceRow row = Assert.Single((await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Replace(7, pnl)))).Rows);
        Assert.Equal((decimal)expected, row.SourceReportedPnL);
    }

    [Fact]
    public async Task DoesNotImposeUnestablishedCostOrWholeContractSemantics()
    {
        string row = TopstepCsvFixtures.Replace(6, "-0.25");
        row = TopstepCsvFixtures.Replace(8, "0.5", row);
        row = TopstepCsvFixtures.Replace(12, "0", row);
        TopstepSourceRow parsed = Assert.Single((await ParseAsync(TopstepCsvFixtures.WithRows(row))).Rows);
        Assert.Equal(-0.25m, parsed.SourceReportedFees);
        Assert.Equal(0m, parsed.SourceReportedCommissions);
        Assert.Equal(0.5m, parsed.Size);
    }

    [Fact]
    public async Task SupportsBomQuotedCommaEscapedQuoteMultilineBlankLinesAndReorderedHeaders()
    {
        string csv = TopstepCsvFixtures.Header + ",Comment\r\n\r\n" + TopstepCsvFixtures.Row +
                     ",\"synthetic, \"\"quoted\"\"\r\ncomment\"\r\n" +
                     TopstepCsvFixtures.Replace(0, "000SYNTH02") + ",ok\n";
        await using var stream = new MemoryStream([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)]);
        TopstepCsvParseResult result = await _parser.ParseAsync(stream);
        Assert.True(result.IsCompleteInputValid);
        Assert.Equal([3, 5], result.Rows.Select(r => r.SourceLineNumber));
        Assert.Equal(TopstepCsvDiagnosticCodes.AdditionalHeader, Assert.Single(result.Diagnostics).Code);

        string reordered = string.Join(',', TopstepCsvFixtures.Header.Split(',').Reverse()) + "\n" +
                           string.Join(',', TopstepCsvFixtures.Row.Split(',').Reverse());
        Assert.Equal(result.Rows[0] with { SourceLineNumber = 2 }, Assert.Single((await ParseAsync(reordered)).Rows));

        string quoted = TopstepCsvFixtures.Replace(1, "\"TEST, \"\"contract\"\"\"");
        Assert.Equal("TEST, \"contract\"", Assert.Single((await ParseAsync(TopstepCsvFixtures.WithRows(quoted))).Rows).ContractName);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public async Task EveryRequiredFieldRejectsBlankWithLocation(int column)
    {
        TopstepCsvParseResult result = await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Replace(column, " ")));
        TopstepCsvDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(TopstepCsvDiagnosticCodes.RequiredValue, diagnostic.Code);
        Assert.Equal(TopstepCsvFixtures.Header.Split(',')[column], diagnostic.FieldName);
        Assert.Equal(1, diagnostic.SourceRecordIndex);
        Assert.Equal(2, diagnostic.SourceLineNumber);
        Assert.Equal(1, result.RejectedRecordCount);
        Assert.False(result.IsCompleteInputValid);
    }

    [Theory]
    [InlineData(4, "bad", "INVALID_DECIMAL")]
    [InlineData(5, "79228162514264337593543950336", "INVALID_DECIMAL")]
    [InlineData(4, "20000.12345678901234567890123456789", "INVALID_DECIMAL")]
    [InlineData(7, "0.00000000000000000000000000001", "INVALID_DECIMAL")]
    [InlineData(6, "$1.20", "INVALID_DECIMAL")]
    [InlineData(7, "\"1,20\"", "INVALID_DECIMAL")]
    [InlineData(8, "1e2", "INVALID_DECIMAL")]
    [InlineData(8, "0", "INVALID_SIZE")]
    [InlineData(8, "-1", "INVALID_SIZE")]
    [InlineData(9, "Buy", "UNSUPPORTED_TYPE")]
    [InlineData(9, "0", "UNSUPPORTED_TYPE")]
    [InlineData(9, "long", "UNSUPPORTED_TYPE")]
    [InlineData(2, "07/10/2026 17:00:00", "INVALID_TIMESTAMP")]
    [InlineData(3, "02/30/2026 17:00:00 +03:00", "INVALID_TIMESTAMP")]
    [InlineData(10, "07/10/2026", "INVALID_TIMESTAMP")]
    [InlineData(11, "bad", "INVALID_DURATION")]
    [InlineData(11, "-00:00:01", "INVALID_DURATION")]
    [InlineData(12, "NaN", "INVALID_DECIMAL")]
    public async Task MalformedValuesRejectRowWithoutDefaults(int column, string value, string code)
    {
        TopstepCsvParseResult result = await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Replace(column, value)));
        Assert.Empty(result.Rows);
        Assert.Equal(1, result.RejectedRecordCount);
        Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("", "MISSING_HEADER")]
    [InlineData("Id", "DUPLICATE_HEADER")]
    [InlineData("commissions", "MISSING_HEADER")]
    public async Task InvalidHeadersRejectWholeFile(string lastHeader, string code)
    {
        string header = TopstepCsvFixtures.Header.Replace("Commissions", lastHeader, StringComparison.Ordinal);
        TopstepCsvParseResult result = await ParseAsync(header + "\n" + TopstepCsvFixtures.Row);
        Assert.False(result.IsHeaderUsable);
        Assert.False(result.IsCompleteInputValid);
        Assert.Equal(1, result.RejectedRecordCount);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.SourceLineNumber == 1);
    }

    [Fact]
    public async Task ProviderSchemasAreDistinct()
    {
        Assert.False((await ParseAsync(TradovateCsvFixtures.MultiRow)).IsHeaderUsable);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row)));
        Assert.False((await new TradovateCsvParser().ParseAsync(stream)).IsHeaderUsable);
    }

    [Theory]
    [InlineData("\"Id,ContractName", "INVALID_HEADER")]
    [InlineData("\n  \r\n", "EMPTY_INPUT")]
    [InlineData(TopstepCsvFixtures.Header, "NO_DATA_ROWS")]
    public async Task MalformedOrEmptyInputIsNotComplete(string csv, string code)
    {
        TopstepCsvParseResult result = await ParseAsync(csv);
        Assert.False(result.IsCompleteInputValid);
        Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("bad,shape")]
    [InlineData("\"unclosed")]
    [InlineData("Id\"unexpected,rest")]
    [InlineData("\"Id\"trailing,rest")]
    public async Task MalformedCsvRowIsLocatedAndOtherValidRowsRemain(string malformed)
    {
        TopstepCsvParseResult result = await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row, malformed));
        Assert.Single(result.Rows);
        Assert.Equal(2, result.SourceRecordCount);
        Assert.Equal(1, result.RejectedRecordCount);
        TopstepCsvDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(TopstepCsvDiagnosticCodes.InvalidRowShape, diagnostic.Code);
        Assert.Equal(2, diagnostic.SourceRecordIndex);
        Assert.Equal(3, diagnostic.SourceLineNumber);
    }

    [Theory]
    [InlineData("5.000000000", "DUPLICATE_ID")]
    [InlineData("6", "CONFLICTING_ID")]
    [InlineData("invalid", "CONFLICTING_ID")]
    public async Task AllRepeatedIdentityOccurrencesAreRejectedWithoutExposingIds(string pnl, string code)
    {
        TopstepCsvParseResult result = await ParseAsync(TopstepCsvFixtures.WithRows(
            TopstepCsvFixtures.Row, TopstepCsvFixtures.Replace(7, pnl), TopstepCsvFixtures.Replace(0, "000OTHER")));
        Assert.Equal(3, result.SourceRecordCount);
        Assert.Equal(2, result.RejectedRecordCount);
        Assert.Equal("000OTHER", Assert.Single(result.Rows).Id);
        Assert.False(result.IsCompleteInputValid);
        Assert.Equal([2, 3], result.Diagnostics.Where(d => d.Code == code).Select(d => d.SourceLineNumber));
        Assert.All(result.Diagnostics, d => Assert.DoesNotContain("000SYNTH01", d.Message));
    }

    [Fact]
    public async Task CultureDoesNotChangeDecimalsOffsetsOrDeterminism()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("bg-BG");
            TopstepCsvParseResult first = await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            TopstepCsvParseResult second = await ParseAsync(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row));
            Assert.Equal(first.Rows.ToArray(), second.Rows.ToArray());
            Assert.Equal(first.Diagnostics.ToArray(), second.Diagnostics.ToArray());
            Assert.Equal(20000.125m, Assert.Single(first.Rows).EntryPrice);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task LeavesCallerStreamOpenAndDoesNotWriteToIt()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row));
        await using var stream = new MemoryStream(bytes, writable: false);
        Assert.True((await _parser.ParseAsync(stream)).IsCompleteInputValid);
        Assert.True(stream.CanRead);
        Assert.Equal(bytes, stream.ToArray());
    }

    [Fact]
    public async Task PreCancelledTokenStopsBeforeReading()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TopstepCsvFixtures.Row));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _parser.ParseAsync(stream, cancellation.Token));
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task CancellationDuringReadPropagatesAndLeavesStreamOpen()
    {
        using var cancellation = new CancellationTokenSource();
        await using var stream = new CancellingStream(cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _parser.ParseAsync(stream, cancellation.Token));
        Assert.Equal(cancellation.Token, stream.ObservedToken);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task InvalidUtf8IsDiagnosticButIoFailurePropagates()
    {
        await using var invalid = new MemoryStream([0xC3, 0x28]);
        Assert.Equal(TopstepCsvDiagnosticCodes.InvalidEncoding, Assert.Single((await _parser.ParseAsync(invalid)).Diagnostics).Code);
        await using var broken = new FailingStream();
        await Assert.ThrowsAsync<IOException>(() => _parser.ParseAsync(broken));
    }

    private async Task<TopstepCsvParseResult> ParseAsync(string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await _parser.ParseAsync(stream);
    }

    private sealed class CancellingStream(CancellationTokenSource cancellation) : MemoryStream
    {
        public CancellationToken ObservedToken { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObservedToken = cancellationToken;
            cancellation.Cancel();
            return ValueTask.FromCanceled<int>(cancellationToken);
        }
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Synthetic read failure."));
    }
}
