using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Imports.Tradovate;

public sealed class TradovateReconstructionContractTests
{
    [Fact]
    public void ReconstructedExecutionSnapshotsProvenanceAndRequiresUnspecifiedTime()
    {
        var recordIndices = new List<int> { 3, 1, 3 };
        var lineNumbers = new List<int> { 4, 2, 4 };

        var execution = new TradovateReconstructedExecution(
            "MNQU6",
            ExecutionSide.Buy,
            "000-BUY",
            3m,
            20123.125m,
            LocalTime(9),
            0.25m,
            recordIndices,
            lineNumbers);
        recordIndices.Clear();
        lineNumbers.Clear();

        Assert.Equal([1, 3], execution.SourceRecordIndices);
        Assert.Equal([2, 4], execution.SourceLineNumbers);
        Assert.Equal(DateTimeKind.Unspecified, execution.SourceLocalTimestamp.Kind);
        Assert.Throws<ArgumentException>(() => new TradovateReconstructedExecution(
            "MNQU6",
            ExecutionSide.Buy,
            "000-BUY",
            1m,
            1m,
            DateTime.SpecifyKind(LocalTime(9), DateTimeKind.Utc),
            0.25m,
            [1],
            [2]));
    }

    [Fact]
    public void CandidateSnapshotsExecutionsSourceRecordsAndDiagnosticCodes()
    {
        TradovateReconstructedExecution execution = CreateExecution();
        var executions = new List<TradovateReconstructedExecution> { execution };
        var records = new List<int> { 1 };
        var codes = new List<string> { TradovateReconstructionDiagnosticCodes.IncompleteLifecycle };

        var candidate = new TradovateTradeCandidate(
            "MNQU6",
            TradeDirection.Long,
            executions,
            LocalTime(9),
            closingLocalTimestamp: null,
            openingQuantity: 1m,
            closingQuantity: 0m,
            signedPositionAtEnd: 1m,
            TradovateReconstructionStatus.Incomplete,
            records,
            codes);
        executions.Clear();
        records.Clear();
        codes.Clear();

        Assert.Single(candidate.OrderedExecutions);
        Assert.Equal([1], candidate.SourceRecordIndices);
        Assert.Equal([TradovateReconstructionDiagnosticCodes.IncompleteLifecycle],
            candidate.DiagnosticCodes);
    }

    [Fact]
    public void ResultSnapshotsCollectionsAndExposesEligibilityAndCounts()
    {
        TradovateReconstructedExecution execution = CreateExecution();
        var executions = new List<TradovateReconstructedExecution> { execution };
        var result = new TradovateExecutionReconstructionResult(
            executions,
            [],
            [new TradovateMatchedPairEvidence(1, 2, "MNQU6", "000-BUY", "000-SELL", 1m, 5m)],
            [new TradovateSymbolReconciliation("MNQU6", 1, 1m, 1m, 1m, true)],
            [],
            sourceRecordCount: 1,
            TradovateReconstructionStatus.Reconstructed);
        executions.Clear();

        Assert.Single(result.Executions);
        Assert.Equal(1, result.ReconciledSourceRecordCount);
        Assert.Equal(1, result.UniqueBuyFillCount);
        Assert.Equal(0, result.UniqueSellFillCount);
        Assert.True(result.IsEligibleForAutomaticImport);
        Assert.False(result.IsSourceCompletenessIndependentlyVerified);
    }

    [Fact]
    public void DiagnosticSnapshotsAndCanonicalizesSourceReferences()
    {
        var diagnostic = new TradovateReconstructionDiagnostic(
            TradovateReconstructionDiagnosticSeverity.Error,
            TradovateReconstructionDiagnosticCodes.DuplicateMatchedRow,
            "MNQU6",
            [4, 2, 4],
            ["SELL-1", "BUY-1", "SELL-1"],
            "Ambiguous matched rows.");

        Assert.Equal([2, 4], diagnostic.SourceRecordIndices);
        Assert.Equal(["BUY-1", "SELL-1"], diagnostic.ExternalFillIds);
    }

    private static TradovateReconstructedExecution CreateExecution() => new(
        "MNQU6",
        ExecutionSide.Buy,
        "000-BUY",
        1m,
        20123.125m,
        LocalTime(9),
        0.25m,
        [1],
        [2]);

    private static DateTime LocalTime(int hour) =>
        new(2026, 9, 10, hour, 0, 0, DateTimeKind.Unspecified);
}
