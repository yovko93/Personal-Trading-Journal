using System.Text;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

public sealed class TradovateExecutionReconstructorTests
{
    private readonly TradovateExecutionReconstructor _reconstructor = new();

    [Fact]
    public async Task CompleteHeaderOnlyInputIsBlockedAndNotEligible()
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(TradovateCsvFixtures.Header));
        TradovateCsvParseResult input = await new TradovateCsvParser().ParseAsync(stream);

        Assert.True(input.IsHeaderUsable);
        Assert.True(input.IsCompleteInputValid);
        Assert.Equal(0, input.SourceRecordCount);
        Assert.Empty(input.Rows);

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Blocked, result.Status);
        Assert.False(result.IsEligibleForAutomaticImport);
        Assert.Empty(result.Executions);
        Assert.Empty(result.MatchedPairs);
        Assert.Empty(result.Candidates);
        AssertDiagnostic(result, TradovateReconstructionDiagnosticCodes.EmptySourceData);
    }

    [Fact]
    public void OneMatchedRowProducesTwoUniqueFillsAndOneLongCandidate()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", 2m, At(9), At(9, 1),
                buyPrice: 20123.125m, sellPrice: 20124.375m, sourceReportedPnL: 125m));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Reconstructed, result.Status);
        Assert.True(result.IsEligibleForAutomaticImport);
        Assert.Equal(2, result.Executions.Count);
        Assert.Equal(1, result.UniqueBuyFillCount);
        Assert.Equal(1, result.UniqueSellFillCount);
        Assert.Contains(result.Executions, execution =>
            execution.Side == ExecutionSide.Buy &&
            execution.Price == 20123.125m &&
            execution.SourceLocalTimestamp == At(9));
        Assert.Contains(result.Executions, execution =>
            execution.Side == ExecutionSide.Sell &&
            execution.Price == 20124.375m &&
            execution.SourceLocalTimestamp == At(9, 1));
        TradovateTradeCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal(TradeDirection.Long, candidate.ProvisionalDirection);
        Assert.Equal(TradovateReconstructionStatus.Reconstructed, candidate.Status);
        Assert.Equal(2m, candidate.OpeningQuantity);
        Assert.Equal(2m, candidate.ClosingQuantity);
        Assert.Equal(0m, candidate.SignedPositionAtEnd);
        Assert.Equal(At(9), candidate.OpeningLocalTimestamp);
        Assert.Equal(At(9, 1), candidate.ClosingLocalTimestamp);
        Assert.Equal([1], candidate.SourceRecordIndices);

        TradovateSymbolReconciliation reconciliation =
            Assert.Single(result.SymbolReconciliations);
        Assert.Equal(2m, reconciliation.MatchedQuantity);
        Assert.Equal(2m, reconciliation.ReconstructedBuyQuantity);
        Assert.Equal(2m, reconciliation.ReconstructedSellQuantity);
        Assert.True(reconciliation.IsQuantityReconciled);
    }

    [Fact]
    public void RepeatedBuyFillIsReconstructedOnceWithSummedMatchedQuantity()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", 2m, At(9), At(10)),
            Row(2, "MNQU6", "BUY-1", "SELL-2", 1m, At(9), At(10, 1)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        TradovateReconstructedExecution buy = Assert.Single(
            result.Executions,
            execution => execution.Side == ExecutionSide.Buy);
        Assert.Equal("BUY-1", buy.ExternalFillId);
        Assert.Equal(3m, buy.Quantity);
        Assert.Equal([1, 2], buy.SourceRecordIndices);
        Assert.Equal([2, 3], buy.SourceLineNumbers);
        Assert.Equal(3, Assert.Single(result.Candidates).OrderedExecutions.Count);
    }

    [Fact]
    public void RepeatedSellFillIsReconstructedOnceAndSupportsShortLifecycle()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQZ6", "BUY-1", "SELL-1", 1m, At(10), At(9)),
            Row(2, "MNQZ6", "BUY-2", "SELL-1", 2m, At(10, 1), At(9)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        TradovateReconstructedExecution sell = Assert.Single(
            result.Executions,
            execution => execution.Side == ExecutionSide.Sell);
        Assert.Equal(3m, sell.Quantity);
        Assert.Equal([1, 2], sell.SourceRecordIndices);
        TradovateTradeCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal(TradeDirection.Short, candidate.ProvisionalDirection);
        Assert.Equal([ExecutionSide.Sell, ExecutionSide.Buy, ExecutionSide.Buy],
            candidate.OrderedExecutions.Select(execution => execution.Side));
    }

    [Fact]
    public void BothSidesMayRepeatAcrossSeveralMatchedPairsWithoutQuantityDuplication()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10)),
            Row(2, "MNQU6", "BUY-1", "SELL-2", 1m, At(9), At(10, 1)),
            Row(3, "MNQU6", "BUY-2", "SELL-1", 1m, At(9, 1), At(10)),
            Row(4, "MNQU6", "BUY-2", "SELL-2", 1m, At(9, 1), At(10, 1)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(4, result.Executions.Count);
        Assert.All(result.Executions, execution => Assert.Equal(2m, execution.Quantity));
        Assert.Equal(4m, Assert.Single(result.SymbolReconciliations).MatchedQuantity);
        Assert.Equal(4m, Assert.Single(result.Candidates).OpeningQuantity);
        Assert.Equal(4m, Assert.Single(result.Candidates).ClosingQuantity);
    }

    [Fact]
    public void InterleavedBrokerContractsAreReconstructedAndGroupedIndependently()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "U-BUY", "U-SELL", 1m, At(9), At(10)),
            Row(2, "MNQZ6", "Z-BUY", "Z-SELL", 2m, At(12), At(11)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(2, result.SymbolReconciliations.Count);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Contains(result.Candidates, candidate =>
            candidate.BrokerSymbol == "MNQU6" && candidate.ProvisionalDirection == TradeDirection.Long);
        Assert.Contains(result.Candidates, candidate =>
            candidate.BrokerSymbol == "MNQZ6" && candidate.ProvisionalDirection == TradeDirection.Short);
    }

    [Theory]
    [InlineData("price", "CONFLICTING_FILL_PRICE")]
    [InlineData("timestamp", "CONFLICTING_FILL_TIMESTAMP")]
    [InlineData("tick", "CONFLICTING_FILL_TICK_SIZE")]
    public void ConflictingFactsForSameFillBlockInsteadOfSelectingFirstValue(
        string conflict,
        string expectedCode)
    {
        TradovateMatchedFillRow first = Row(
            1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10));
        TradovateMatchedFillRow second = Row(
            2,
            "MNQU6",
            "BUY-1",
            "SELL-2",
            1m,
            conflict == "timestamp" ? At(9, 1) : At(9),
            At(10, 1),
            buyPrice: conflict == "price" ? 20125m : 20123.125m,
            tickSize: conflict == "tick" ? 0.5m : 0.25m);

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(
            TradovateReconstructionFixtures.Complete(first, second));

        Assert.Equal(TradovateReconstructionStatus.Blocked, result.Status);
        TradovateReconstructionDiagnostic diagnostic = AssertDiagnostic(result, expectedCode);
        Assert.Equal([1, 2], diagnostic.SourceRecordIndices);
        Assert.Equal(["BUY-1"], diagnostic.ExternalFillIds);
        Assert.DoesNotContain(result.Executions, execution => execution.ExternalFillId == "BUY-1");
        AssertDiagnostic(
            result,
            TradovateReconstructionDiagnosticCodes.QuantityReconciliationFailed);
        Assert.False(result.IsEligibleForAutomaticImport);
    }

    [Fact]
    public void SameSideFillIdAcrossSymbolsIsReportedAsSourceCollision()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "U-SELL", 1m, At(9), At(10)),
            Row(2, "MNQZ6", "BUY-1", "Z-SELL", 1m, At(11), At(12)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Blocked, result.Status);
        TradovateReconstructionDiagnostic diagnostic = AssertDiagnostic(
            result,
            TradovateReconstructionDiagnosticCodes.ConflictingFillSymbol);
        Assert.Null(diagnostic.BrokerSymbol);
        Assert.Equal([1, 2], diagnostic.SourceRecordIndices);
        Assert.All(result.Candidates, candidate =>
            Assert.Equal(TradovateReconstructionStatus.Blocked, candidate.Status));
    }

    [Fact]
    public void IdenticalMatchedRowsRemainPreservedAndMakeGroupingAmbiguous()
    {
        TradovateMatchedFillRow first = Row(
            1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10));
        TradovateMatchedFillRow duplicate = first with
        {
            SourceRecordIndex = 2,
            SourceLineNumber = 3,
        };

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(
            TradovateReconstructionFixtures.Complete(first, duplicate));

        Assert.Equal(TradovateReconstructionStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.MatchedPairs.Count);
        Assert.Equal(2m, result.Executions.Single(execution =>
            execution.Side == ExecutionSide.Buy).Quantity);
        Assert.Equal(TradovateReconstructionStatus.Ambiguous,
            Assert.Single(result.Candidates).Status);
        AssertDiagnostic(result, TradovateReconstructionDiagnosticCodes.DuplicateMatchedRow);
    }

    [Fact]
    public void PartialExitAndSubsequentFlatLifecycleProduceSeparateCandidates()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10)),
            Row(2, "MNQU6", "BUY-1", "SELL-2", 2m, At(9), At(10, 1)),
            Row(3, "MNQU6", "BUY-2", "SELL-3", 2m, At(11), At(12)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(2, result.Candidates.Count);
        TradovateTradeCandidate first = result.Candidates[0];
        Assert.Equal(3m, first.OpeningQuantity);
        Assert.Equal(3m, first.ClosingQuantity);
        Assert.Equal(3, first.OrderedExecutions.Count);
        Assert.Equal([1, 2], first.SourceRecordIndices);
        Assert.Equal([3], result.Candidates[1].SourceRecordIndices);
    }

    [Fact]
    public void ReversalCrossingZeroBlocksLifecycleWithoutReassigningPriorCandidate()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-0", "SELL-0", 1m, At(8), At(8, 1)),
            Row(2, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(9, 1)),
            Row(3, "MNQU6", "BUY-2", "SELL-1", 1m, At(9, 2), At(9, 1)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Blocked, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(TradovateReconstructionStatus.Reconstructed, result.Candidates[0].Status);
        Assert.Equal(TradovateReconstructionStatus.Blocked, result.Candidates[1].Status);
        AssertDiagnostic(result, TradovateReconstructionDiagnosticCodes.PositionReversal);
        string[] assignedIds = result.Candidates
            .SelectMany(candidate => candidate.OrderedExecutions)
            .Select(execution => execution.ExternalFillId)
            .ToArray();
        Assert.Equal(assignedIds.Length, assignedIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void OppositeSideTimestampTieThatCanChangeBoundaryIsAmbiguous()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10)),
            Row(2, "MNQU6", "BUY-2", "SELL-2", 1m, At(10), At(11)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Ambiguous, result.Status);
        TradovateTradeCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal(TradovateReconstructionStatus.Ambiguous, candidate.Status);
        Assert.Equal(0m, candidate.SignedPositionAtEnd);
        AssertDiagnostic(result, TradovateReconstructionDiagnosticCodes.TimestampOrderAmbiguous);
    }

    [Fact]
    public void OppositeSideTimestampTieThatCannotReachFlatUsesStableSafeOrder()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10)),
            Row(2, "MNQU6", "BUY-1", "SELL-2", 1m, At(9), At(11)),
            Row(3, "MNQU6", "BUY-2", "SELL-3", 1m, At(10), At(12)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Reconstructed, result.Status);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == TradovateReconstructionDiagnosticCodes.TimestampOrderAmbiguous);
        Assert.Equal(["BUY-1", "BUY-2", "SELL-1", "SELL-2", "SELL-3"],
            Assert.Single(result.Candidates).OrderedExecutions.Select(execution =>
                execution.ExternalFillId));
    }

    [Fact]
    public void SameSideTimestampTiesAreDeterministicAndInputOrderDoesNotChangeGrouping()
    {
        TradovateMatchedFillRow first = Row(
            1, "MNQU6", "BUY-B", "SELL-B", 1m, At(9), At(10));
        TradovateMatchedFillRow second = Row(
            2, "MNQU6", "BUY-A", "SELL-A", 1m, At(9), At(10));

        TradovateExecutionReconstructionResult original = _reconstructor.Reconstruct(
            TradovateReconstructionFixtures.Complete(first, second));
        TradovateExecutionReconstructionResult reversed = _reconstructor.Reconstruct(
            TradovateReconstructionFixtures.Complete(second, first));

        string[] originalIds = Assert.Single(original.Candidates).OrderedExecutions
            .Select(execution => execution.ExternalFillId)
            .ToArray();
        string[] reversedIds = Assert.Single(reversed.Candidates).OrderedExecutions
            .Select(execution => execution.ExternalFillId)
            .ToArray();
        Assert.Equal(["BUY-A", "BUY-B", "SELL-A", "SELL-B"], originalIds);
        Assert.Equal(originalIds, reversedIds);
    }

    [Fact]
    public void ParserErrorsBlockReconstructionAndNoPartialRowsProceed()
    {
        TradovateMatchedFillRow validRow = Row(
            1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10));
        var parseResult = new TradovateCsvParseResult(
            [validRow],
            [new TradovateCsvDiagnostic(
                TradovateCsvDiagnosticSeverity.Error,
                TradovateCsvDiagnosticCodes.InvalidPrice,
                2,
                3,
                "buyPrice",
                "Invalid price.")],
            sourceRecordCount: 2,
            rejectedRecordCount: 1,
            isHeaderUsable: true);

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(parseResult);

        Assert.Equal(TradovateReconstructionStatus.Blocked, result.Status);
        Assert.False(result.IsEligibleForAutomaticImport);
        Assert.Empty(result.Executions);
        Assert.Empty(result.Candidates);
        Assert.Empty(result.MatchedPairs);
        AssertDiagnostic(result, TradovateReconstructionDiagnosticCodes.ParserInputIncomplete);
    }

    [Fact]
    public void SourcePnlRemainsMatchedPairEvidenceWithoutCostsOrUtcConversion()
    {
        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(
            TradovateReconstructionFixtures.Complete(
                Row(1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10),
                    sourceReportedPnL: -269.50m)));

        Assert.Equal(-269.50m, Assert.Single(result.MatchedPairs).SourceReportedPnL);
        Assert.All(result.Executions, execution =>
            Assert.Equal(DateTimeKind.Unspecified, execution.SourceLocalTimestamp.Kind));
        Assert.Null(typeof(TradovateReconstructedExecution).GetProperty("Commission"));
        Assert.Null(typeof(TradovateReconstructedExecution).GetProperty("Fees"));
        Assert.False(result.IsSourceCompletenessIndependentlyVerified);
        AssertDiagnostic(result, TradovateReconstructionDiagnosticCodes.SourceCompletenessUnverified);
    }

    [Fact]
    public void QuantityOverflowProducesSafeBlockedResult()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", decimal.MaxValue, At(9), At(10)),
            Row(2, "MNQU6", "BUY-1", "SELL-2", 1m, At(9), At(10, 1)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == TradovateReconstructionDiagnosticCodes.QuantityOverflow);
        Assert.False(result.IsEligibleForAutomaticImport);
    }

    [Fact]
    public void ComplexFixtureReconstructsPartialFillsLongShortAndConsecutiveTrades()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "U-BUY-1", "U-SELL-1", 1m, At(9), At(10)),
            Row(2, "MNQU6", "U-BUY-1", "U-SELL-2", 1m, At(9), At(10, 1)),
            Row(3, "MNQZ6", "Z-BUY-1", "Z-SELL-1", 1m, At(12), At(11)),
            Row(4, "MNQZ6", "Z-BUY-2", "Z-SELL-1", 1m, At(12, 1), At(11)),
            Row(5, "MNQU6", "U-BUY-2", "U-SELL-3", 3m, At(13), At(14)));

        TradovateExecutionReconstructionResult result = _reconstructor.Reconstruct(input);

        Assert.Equal(TradovateReconstructionStatus.Reconstructed, result.Status);
        Assert.Equal(8, result.Executions.Count);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(5, result.ReconciledSourceRecordCount);
        Assert.Equal(2, result.SymbolReconciliations.Count);
        Assert.Equal(2, result.Candidates.Count(candidate => candidate.BrokerSymbol == "MNQU6"));
        Assert.Single(
            result.Candidates,
            candidate =>
                candidate.BrokerSymbol == "MNQZ6" &&
                candidate.ProvisionalDirection == TradeDirection.Short);
        Assert.Equal([1, 2], result.Candidates[0].SourceRecordIndices);
    }

    [Fact]
    public void RepeatedRunsAreEquivalentAndCancellationIsRespected()
    {
        TradovateCsvParseResult input = TradovateReconstructionFixtures.Complete(
            Row(1, "MNQU6", "BUY-1", "SELL-1", 1m, At(9), At(10)));

        TradovateExecutionReconstructionResult first = _reconstructor.Reconstruct(input);
        TradovateExecutionReconstructionResult second = _reconstructor.Reconstruct(input);

        Assert.Equal(
            first.Executions.Select(ExecutionFingerprint),
            second.Executions.Select(ExecutionFingerprint));
        Assert.Equal(
            first.Candidates.Select(CandidateFingerprint),
            second.Candidates.Select(CandidateFingerprint));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            _reconstructor.Reconstruct(input, source.Token));
    }

    private static TradovateReconstructionDiagnostic AssertDiagnostic(
        TradovateExecutionReconstructionResult result,
        string code) =>
        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == code);

    private static string ExecutionFingerprint(TradovateReconstructedExecution execution) =>
        $"{execution.BrokerSymbol}|{execution.Side}|{execution.ExternalFillId}|{execution.Quantity}|{execution.Price}|{execution.SourceLocalTimestamp:O}";

    private static string CandidateFingerprint(TradovateTradeCandidate candidate) =>
        $"{candidate.BrokerSymbol}|{candidate.ProvisionalDirection}|{candidate.Status}|{candidate.OpeningLocalTimestamp:O}|{candidate.ClosingLocalTimestamp:O}|{candidate.OpeningQuantity}|{candidate.ClosingQuantity}";

    private static TradovateMatchedFillRow Row(
        int sourceRecordIndex,
        string brokerSymbol,
        string buyFillId,
        string sellFillId,
        decimal matchedQuantity,
        DateTime boughtLocalTimestamp,
        DateTime soldLocalTimestamp,
        decimal buyPrice = 20123.125m,
        decimal sellPrice = 20124.375m,
        decimal tickSize = 0.25m,
        decimal sourceReportedPnL = 0m) =>
        TradovateReconstructionFixtures.Row(
            sourceRecordIndex,
            brokerSymbol,
            buyFillId,
            sellFillId,
            matchedQuantity,
            boughtLocalTimestamp,
            soldLocalTimestamp,
            buyPrice,
            sellPrice,
            tickSize,
            sourceReportedPnL);

    private static DateTime At(int hour, int minute = 0, int second = 0) =>
        TradovateReconstructionFixtures.At(hour, minute, second);
}
