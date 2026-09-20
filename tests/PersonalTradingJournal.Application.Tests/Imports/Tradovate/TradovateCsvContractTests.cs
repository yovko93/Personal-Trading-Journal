using PersonalTradingJournal.Application.Imports.Tradovate;

namespace PersonalTradingJournal.Application.Tests.Imports.Tradovate;

public sealed class TradovateCsvContractTests
{
    [Fact]
    public void ParseResultSnapshotsRowsAndDiagnosticsAndReportsCounts()
    {
        var rows = new List<TradovateMatchedFillRow> { CreateRow() };
        var diagnostics = new List<TradovateCsvDiagnostic>();

        var result = new TradovateCsvParseResult(
            rows,
            diagnostics,
            sourceRecordCount: 1,
            rejectedRecordCount: 0,
            isHeaderUsable: true);
        rows.Clear();
        diagnostics.Add(CreateError());

        Assert.Single(result.Rows);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1, result.SourceRecordCount);
        Assert.Equal(1, result.ValidRecordCount);
        Assert.Equal(0, result.RejectedRecordCount);
        Assert.True(result.IsCompleteInputValid);
    }

    [Fact]
    public void ParseResultRequiresValidAndRejectedCountsToMatchSourceCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TradovateCsvParseResult(
                [CreateRow()],
                [],
                sourceRecordCount: 2,
                rejectedRecordCount: 0,
                isHeaderUsable: true));
    }

    [Fact]
    public void ParseResultWithErrorOrRejectedRecordIsNotComplete()
    {
        var result = new TradovateCsvParseResult(
            [],
            [CreateError()],
            sourceRecordCount: 1,
            rejectedRecordCount: 1,
            isHeaderUsable: true);

        Assert.False(result.IsCompleteInputValid);
    }

    private static TradovateMatchedFillRow CreateRow() => new(
        1,
        2,
        "MNQU6",
        "2",
        "0",
        0.25m,
        "000BUY01",
        "000SELL01",
        1m,
        20123.25m,
        20124.25m,
        5m,
        new DateTime(2026, 9, 10, 16, 30, 3, DateTimeKind.Unspecified),
        new DateTime(2026, 9, 10, 16, 30, 15, DateTimeKind.Unspecified),
        "12sec");

    private static TradovateCsvDiagnostic CreateError() => new(
        TradovateCsvDiagnosticSeverity.Error,
        TradovateCsvDiagnosticCodes.InvalidQuantity,
        1,
        2,
        "qty",
        "Invalid quantity.");
}
