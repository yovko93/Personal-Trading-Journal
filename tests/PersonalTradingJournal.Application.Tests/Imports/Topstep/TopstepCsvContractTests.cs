using PersonalTradingJournal.Application.Imports.Topstep;

namespace PersonalTradingJournal.Application.Tests.Imports.Topstep;

public sealed class TopstepCsvContractTests
{
    [Fact]
    public void ResultSnapshotsCollectionsAndReportsCounts()
    {
        var rows = new List<TopstepSourceRow> { CreateRow() };
        var diagnostics = new List<TopstepCsvDiagnostic>();
        var result = new TopstepCsvParseResult(rows, diagnostics, 1, 0, true);
        rows.Clear();
        diagnostics.Add(new(TopstepCsvDiagnosticSeverity.Error, "ERROR", 1, 2, null, "Synthetic error."));

        Assert.Single(result.Rows);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1, result.SourceRecordCount);
        Assert.Equal(1, result.ValidRecordCount);
        Assert.Equal(0, result.RejectedRecordCount);
        Assert.True(result.IsCompleteInputValid);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    [InlineData(2, 0)]
    public void ResultRejectsInconsistentCounts(int total, int rejected)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TopstepCsvParseResult([CreateRow()], [], total, rejected, true));
    }

    [Fact]
    public void EmptyOrErroredResultIsNotComplete()
    {
        Assert.False(new TopstepCsvParseResult([], [], 0, 0, true).IsCompleteInputValid);
        Assert.False(new TopstepCsvParseResult([CreateRow()], [], 2, 1, true).IsCompleteInputValid);
        Assert.False(new TopstepCsvParseResult([CreateRow()],
            [new(TopstepCsvDiagnosticSeverity.Error, "ERROR", 1, 2, null, "Synthetic error.")], 1, 0, true).IsCompleteInputValid);
    }

    private static TopstepSourceRow CreateRow() => new(
        1, 2, "000SYNTH01", "MNQZ6",
        new DateTimeOffset(2026, 7, 10, 17, 0, 0, TimeSpan.FromHours(3)),
        new DateTimeOffset(2026, 7, 10, 17, 0, 1, TimeSpan.FromHours(3)),
        20000m, 20001m, 1.44m, 4m, 2m, TopstepTradeType.Long,
        new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.FromHours(-5)),
        TimeSpan.FromSeconds(1), "00:00:01", 1m);
}
