using System.Globalization;
using System.Text;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

public sealed class TradovateCsvParserTests
{
    private readonly TradovateCsvParser _parser = new();

    [Fact]
    public async Task ParsesSyntheticMatchedRowsWithoutGroupingOrDeduplicating()
    {
        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.MultiRow);

        Assert.True(result.IsHeaderUsable);
        Assert.True(result.IsCompleteInputValid);
        Assert.Equal(4, result.SourceRecordCount);
        Assert.Equal(4, result.ValidRecordCount);
        Assert.Equal(0, result.RejectedRecordCount);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Rows.Count(row => row.BuyFillId == "000BUY01"));
        Assert.Equal(2, result.Rows.Count(row => row.SellFillId == "000SELL02"));
        Assert.Equal(["MNQU6", "MNQU6", "MNQZ6", "MNQZ6"],
            result.Rows.Select(row => row.BrokerSymbol));
        Assert.Equal([2m, 1m, 3m, 1m],
            result.Rows.Select(row => row.MatchedQuantity));
        Assert.Equal([125m, 0m, -273.75m, 73.75m],
            result.Rows.Select(row => row.SourceReportedPnL));
        Assert.Equal([1, 2, 3, 4],
            result.Rows.Select(row => row.SourceRecordIndex));
        Assert.Equal([2, 3, 4, 5],
            result.Rows.Select(row => row.SourceLineNumber));
    }

    [Fact]
    public async Task SameInputProducesEquivalentRowsAndDiagnostics()
    {
        TradovateCsvParseResult first = await ParseAsync(
            TradovateCsvFixtures.MultiRow);
        TradovateCsvParseResult second = await ParseAsync(
            TradovateCsvFixtures.MultiRow);

        Assert.Equal(first.Rows.ToArray(), second.Rows.ToArray());
        Assert.Equal(first.Diagnostics.ToArray(), second.Diagnostics.ToArray());
    }

    [Fact]
    public async Task SupportsUtf8BomCrLfLfReorderedHeadersAndQuotedExtraField()
    {
        const string csv =
            "sellFillId,symbol,qty,buyFillId,_tickSize,sellPrice,buyPrice,pnl,soldTimestamp,boughtTimestamp,duration,_priceFormatType,_priceFormat,comment\r\n" +
            "000SELL01,MNQU6,2,000BUY01,0.25,20124.375,20123.125,$125.00,09/10/2026 16:30:15,09/10/2026 16:30:03,12sec,0,2,\"fictional, \"\"quoted\"\" note\"\n";
        byte[] content = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)];
        await using var stream = new MemoryStream(content);

        TradovateCsvParseResult result = await _parser.ParseAsync(stream);

        TradovateMatchedFillRow row = Assert.Single(result.Rows);
        Assert.Equal("MNQU6", row.BrokerSymbol);
        Assert.Equal(20123.125m, row.BuyPrice);
        Assert.Equal(20124.375m, row.SellPrice);
        TradovateCsvDiagnostic warning = Assert.Single(result.Diagnostics);
        Assert.Equal(TradovateCsvDiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal(TradovateCsvDiagnosticCodes.AdditionalHeader, warning.Code);
        Assert.Equal("comment", warning.FieldName);
        Assert.True(result.IsCompleteInputValid);
    }

    [Fact]
    public async Task EmptyInputIsFatalButHeaderOnlyIsUsable()
    {
        TradovateCsvParseResult empty = await ParseAsync("\r\n  \n");
        TradovateCsvParseResult headerOnly = await ParseAsync(
            TradovateCsvFixtures.Header);

        Assert.False(empty.IsHeaderUsable);
        AssertDiagnostic(empty, TradovateCsvDiagnosticCodes.EmptyInput);
        Assert.True(headerOnly.IsHeaderUsable);
        Assert.True(headerOnly.IsCompleteInputValid);
        Assert.Equal(0, headerOnly.SourceRecordCount);
    }

    [Fact]
    public async Task MissingDuplicateAndEmptyHeadersAreFatalAndLocated()
    {
        string missing = TradovateCsvFixtures.Header.Replace(
            ",duration",
            string.Empty,
            StringComparison.Ordinal);
        string duplicate = TradovateCsvFixtures.Header.Replace(
            "sellFillId",
            "buyFillId",
            StringComparison.Ordinal);
        string empty = TradovateCsvFixtures.Header.Replace(
            "sellFillId",
            string.Empty,
            StringComparison.Ordinal);

        TradovateCsvParseResult missingResult = await ParseAsync(missing);
        TradovateCsvParseResult duplicateResult = await ParseAsync(duplicate);
        TradovateCsvParseResult emptyResult = await ParseAsync(empty);

        TradovateCsvDiagnostic missingDiagnostic = AssertDiagnostic(
            missingResult,
            TradovateCsvDiagnosticCodes.MissingHeader);
        Assert.Equal("duration", missingDiagnostic.FieldName);
        Assert.Equal(1, missingDiagnostic.SourceLineNumber);
        AssertDiagnostic(duplicateResult, TradovateCsvDiagnosticCodes.DuplicateHeader);
        AssertDiagnostic(emptyResult, TradovateCsvDiagnosticCodes.EmptyHeader);
        Assert.All(
            new[] { missingResult, duplicateResult, emptyResult },
            result => Assert.False(result.IsHeaderUsable));
    }

    [Fact]
    public async Task MalformedHeaderQuotingIsFatal()
    {
        TradovateCsvParseResult result = await ParseAsync(
            "\"symbol,_priceFormat,_priceFormatType\n");

        Assert.False(result.IsHeaderUsable);
        AssertDiagnostic(result, TradovateCsvDiagnosticCodes.InvalidHeader);
    }

    [Theory]
    [InlineData("MNQ\"U6,2,0,0.25,000BUY01,000SELL01,2,20123.125,20124.375,$125.00,09/10/2026 16:30:03,09/10/2026 16:30:15,12sec")]
    [InlineData("MNQU6,2,0,0.25,000BUY01,000SELL01,2,20123.125,20124.375,$125.00,09/10/2026 16:30:03,09/10/2026 16:30:15")]
    [InlineData("MNQU6,2,0,0.25,000BUY01,000SELL01,2,20123.125,20124.375,$125.00,09/10/2026 16:30:03,09/10/2026 16:30:15,12sec,extra")]
    public async Task InvalidCsvShapeRejectsRecordWithLocation(string row)
    {
        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.WithRows(row));

        TradovateCsvDiagnostic diagnostic = AssertDiagnostic(
            result,
            TradovateCsvDiagnosticCodes.InvalidRowShape);
        Assert.Equal(1, diagnostic.SourceRecordIndex);
        Assert.Equal(2, diagnostic.SourceLineNumber);
        Assert.Equal(1, result.SourceRecordCount);
        Assert.Equal(1, result.RejectedRecordCount);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task BlankLinesAreIgnoredWithoutChangingSourceLocations()
    {
        string csv = TradovateCsvFixtures.Header +
                     "\r\n\r\n   \n" +
                     TradovateCsvFixtures.LongLikeRow +
                     "\r\n\r\n";

        TradovateCsvParseResult result = await ParseAsync(csv);

        TradovateMatchedFillRow row = Assert.Single(result.Rows);
        Assert.Equal(1, result.SourceRecordCount);
        Assert.Equal(4, row.SourceLineNumber);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("not-a-number")]
    [InlineData("1,5")]
    public async Task InvalidFuturesQuantityProducesStructuredDiagnostic(string quantity)
    {
        string row = ReplaceField(
            TradovateCsvFixtures.LongLikeRow,
            fieldIndex: 6,
            CsvEscape(quantity));

        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.WithRows(row));

        TradovateCsvDiagnostic diagnostic = AssertDiagnostic(
            result,
            TradovateCsvDiagnosticCodes.InvalidQuantity);
        Assert.Equal("qty", diagnostic.FieldName);
        Assert.Equal(1, result.RejectedRecordCount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.25")]
    [InlineData("bad")]
    public async Task InvalidTickSizeProducesStructuredDiagnostic(string tickSize)
    {
        string row = ReplaceField(
            TradovateCsvFixtures.LongLikeRow,
            fieldIndex: 3,
            tickSize);

        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.WithRows(row));

        TradovateCsvDiagnostic diagnostic = AssertDiagnostic(
            result,
            TradovateCsvDiagnosticCodes.InvalidTickSize);
        Assert.Equal("_tickSize", diagnostic.FieldName);
    }

    [Theory]
    [InlineData(7, "bad")]
    [InlineData(8, "1,5")]
    public async Task InvalidPriceProducesStructuredDiagnostic(
        int fieldIndex,
        string price)
    {
        string row = ReplaceField(
            TradovateCsvFixtures.LongLikeRow,
            fieldIndex,
            CsvEscape(price));

        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.WithRows(row));

        TradovateCsvDiagnostic diagnostic = AssertDiagnostic(
            result,
            TradovateCsvDiagnosticCodes.InvalidPrice);
        Assert.Equal(fieldIndex == 7 ? "buyPrice" : "sellPrice", diagnostic.FieldName);
    }

    [Fact]
    public async Task ExactHighPrecisionDecimalsArePreserved()
    {
        string row = TradovateCsvFixtures.LongLikeRow;
        row = ReplaceField(row, 7, "12345.12345678901234567890123");
        row = ReplaceField(row, 8, "-12345.1234567890123456789012");

        TradovateMatchedFillRow parsed = Assert.Single(
            (await ParseAsync(TradovateCsvFixtures.WithRows(row))).Rows);

        Assert.Equal(12345.12345678901234567890123m, parsed.BuyPrice);
        Assert.Equal(-12345.1234567890123456789012m, parsed.SellPrice);
    }

    [Theory]
    [InlineData("$1.00", "1.00")]
    [InlineData("$0.00", "0.00")]
    [InlineData("$(269.50)", "-269.50")]
    [InlineData("$-269.50", "-269.50")]
    [InlineData("$1,234.56", "1234.56")]
    public async Task SupportedSourcePnlFormatsParseExactly(
        string sourcePnL,
        string expected)
    {
        string row = ReplaceField(
            TradovateCsvFixtures.LongLikeRow,
            fieldIndex: 9,
            CsvEscape(sourcePnL));

        TradovateMatchedFillRow parsed = Assert.Single(
            (await ParseAsync(TradovateCsvFixtures.WithRows(row))).Rows);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture),
            parsed.SourceReportedPnL);
    }

    [Theory]
    [InlineData("269.50")]
    [InlineData("$--269.50")]
    [InlineData("$1,50")]
    [InlineData("$(269.50")]
    [InlineData("$abc")]
    public async Task MalformedSourcePnlIsRejected(string sourcePnL)
    {
        string row = ReplaceField(
            TradovateCsvFixtures.LongLikeRow,
            fieldIndex: 9,
            CsvEscape(sourcePnL));

        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.WithRows(row));

        AssertDiagnostic(result, TradovateCsvDiagnosticCodes.InvalidPnL);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task TimestampsAreInvariantUnspecifiedAndShortLikeChronologyIsAccepted()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("bg-BG");
            TradovateCsvParseResult result = await ParseAsync(
                TradovateCsvFixtures.WithRows(TradovateCsvFixtures.ShortLikeRow));

            TradovateMatchedFillRow row = Assert.Single(result.Rows);
            Assert.Equal(DateTimeKind.Unspecified, row.BoughtLocalTimestamp.Kind);
            Assert.Equal(DateTimeKind.Unspecified, row.SoldLocalTimestamp.Kind);
            Assert.Equal(new DateTime(2026, 9, 10, 16, 32, 0), row.BoughtLocalTimestamp);
            Assert.Equal(new DateTime(2026, 9, 10, 16, 31, 42), row.SoldLocalTimestamp);
            Assert.True(row.SoldLocalTimestamp < row.BoughtLocalTimestamp);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("31/12/2026 16:30:03")]
    [InlineData("02/30/2026 16:30:03")]
    [InlineData("09/10/2026 16:30")]
    public async Task InvalidTimestampIsRejected(string timestamp)
    {
        string row = ReplaceField(
            TradovateCsvFixtures.LongLikeRow,
            fieldIndex: 10,
            timestamp);

        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.WithRows(row));

        TradovateCsvDiagnostic diagnostic = AssertDiagnostic(
            result,
            TradovateCsvDiagnosticCodes.InvalidTimestamp);
        Assert.Equal("boughtTimestamp", diagnostic.FieldName);
    }

    [Theory]
    [InlineData(0, "   ", "symbol", "MISSING_SYMBOL")]
    [InlineData(4, "   ", "buyFillId", "MISSING_FILL_ID")]
    [InlineData(5, "   ", "sellFillId", "MISSING_FILL_ID")]
    public async Task BlankRequiredValueIsRejected(
        int fieldIndex,
        string value,
        string fieldName,
        string code)
    {
        string row = ReplaceField(
            TradovateCsvFixtures.LongLikeRow,
            fieldIndex,
            value);

        TradovateCsvParseResult result = await ParseAsync(
            TradovateCsvFixtures.WithRows(row));

        TradovateCsvDiagnostic diagnostic = AssertDiagnostic(result, code);
        Assert.Equal(fieldName, diagnostic.FieldName);
    }

    [Fact]
    public async Task PreservesFillIdentifiersPriceMetadataAndDurationText()
    {
        TradovateMatchedFillRow row = Assert.Single(
            (await ParseAsync(TradovateCsvFixtures.WithRows(
                TradovateCsvFixtures.LongLikeRow))).Rows);

        Assert.Equal("000BUY01", row.BuyFillId);
        Assert.Equal("000SELL01", row.SellFillId);
        Assert.Equal("2", row.PriceFormat);
        Assert.Equal("0", row.PriceFormatType);
        Assert.Equal("12sec", row.SourceDurationText);
    }

    [Fact]
    public async Task ParserLeavesCallerStreamOpenAndHonorsCancellation()
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(TradovateCsvFixtures.MultiRow));
        _ = await _parser.ParseAsync(stream);

        Assert.True(stream.CanRead);

        using var source = new CancellationTokenSource();
        source.Cancel();
        await using var cancelledStream = new MemoryStream(
            Encoding.UTF8.GetBytes(TradovateCsvFixtures.MultiRow));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _parser.ParseAsync(cancelledStream, source.Token));
    }

    private async Task<TradovateCsvParseResult> ParseAsync(string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await _parser.ParseAsync(stream);
    }

    private static TradovateCsvDiagnostic AssertDiagnostic(
        TradovateCsvParseResult result,
        string code) =>
        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == code);

    private static string ReplaceField(string row, int fieldIndex, string value)
    {
        string[] fields = row.Split(',');
        fields[fieldIndex] = value;
        return string.Join(',', fields);
    }

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
}
