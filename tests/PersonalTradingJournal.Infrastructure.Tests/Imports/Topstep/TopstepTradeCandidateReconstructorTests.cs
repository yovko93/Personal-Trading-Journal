using System.Text;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Topstep;

public sealed class TopstepTradeCandidateReconstructorTests
{
    private readonly TopstepTradeCandidateReconstructor _reconstructor = new();

    [Theory]
    [InlineData(TopstepTradeType.Long, TradeDirection.Long)]
    [InlineData(TopstepTradeType.Short, TradeDirection.Short)]
    public void OneClosedRowPreservesDirectionQuantityEconomicsAndFullSourceEvidence(
        TopstepTradeType type, TradeDirection direction)
    {
        TopstepSourceRow row = Row(1) with
        {
            Type = type, EntryPrice = 20000.123456789m, ExitPrice = 20001.987654321m,
            Size = 2.5m, SourceReportedPnL = -19.25m, SourceReportedFees = 1.75m,
            SourceReportedCommissions = 0.5m,
        };
        TopstepTradeReconstructionResult result = Reconstruct(row);
        TopstepTradeCandidate candidate = Assert.Single(result.Candidates);

        Assert.True(result.CanUseRowCandidates);
        Assert.False(result.IsPositionGroupingVerified);
        Assert.False(candidate.ArePositionBoundariesVerified);
        Assert.Same(row, candidate.SourceRow);
        Assert.Equal(row.ContractName, candidate.ContractName);
        Assert.Equal(direction, candidate.Direction);
        Assert.Equal(row.Size, candidate.Quantity);
        Assert.Equal(row.EntryPrice, candidate.EntryPrice);
        Assert.Equal(row.ExitPrice, candidate.ExitPrice);
        Assert.Equal(row.EnteredAtUtc, candidate.OpenedAtUtc);
        Assert.Equal(row.ExitedAtUtc, candidate.ClosedAtUtc);
        Assert.Equal(TimeSpan.Zero, candidate.OpenedAtUtc.Offset);
        Assert.Equal(TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ApparentPartialClosesRemainThreeRowsRatherThanAnInventedCommonEntry()
    {
        TopstepSourceRow[] rows =
        [
            Row(1, exitSecond: 10) with { Size = 2m, ExitPrice = 20001.125m },
            Row(2, exitSecond: 20) with { Size = 3m, ExitPrice = 20002.375m },
            Row(3, exitSecond: 30) with { Size = 5m, ExitPrice = 20003.625m },
        ];
        TopstepTradeReconstructionResult result = Reconstruct(rows);

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal([2m, 3m, 5m], result.Candidates.Select(c => c.Quantity));
        Assert.Equal(10m, result.Candidates.Sum(c => c.Quantity));
        Assert.Equal(rows.Select(r => r.EntryPrice), result.Candidates.Select(c => c.EntryPrice));
        Assert.Equal(rows.Select(r => r.ExitPrice), result.Candidates.Select(c => c.ExitPrice));
        Assert.Equal(rows.Select(r => r.ExitedAtUtc), result.Candidates.Select(c => c.ClosedAtUtc));
        Assert.Equal([1, 2, 3], Assert.Single(RelationshipWarnings(result)).SourceReferences.Select(r => r.SourceRecordIndex));
        Assert.True(result.CanUseRowCandidates);
    }

    [Fact]
    public void IdenticalTimesAndPricesWithDistinctIdsAreNotDeduplicatedOrGrouped()
    {
        TopstepTradeReconstructionResult result = Reconstruct(Row(1), Row(2));
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(4m, result.Candidates.Sum(c => c.Quantity));
        Assert.Equal(["SYNTH-1", "SYNTH-2"], result.Candidates.Select(c => c.SourceRow.Id));
        Assert.Single(RelationshipWarnings(result));
    }

    [Fact]
    public void SharedEntryTimestampWithDifferentEntryPricesRetainsBothEconomics()
    {
        TopstepTradeReconstructionResult result = Reconstruct(
            Row(1) with { EntryPrice = 20000.125m }, Row(2) with { EntryPrice = 20000.875m });
        Assert.Equal([20000.125m, 20000.875m], result.Candidates.Select(c => c.EntryPrice));
        Assert.Single(RelationshipWarnings(result));
    }

    [Fact]
    public void OverlappingChainIsOnlyADiagnosticGroupNotATradeGroup()
    {
        TopstepTradeReconstructionResult result = Reconstruct(
            Row(1, 0, 10), Row(2, 5, 20), Row(3, 15, 25), Row(4, 30, 40));

        Assert.Equal(4, result.Candidates.Count);
        Assert.Equal([1, 2, 3], Assert.Single(RelationshipWarnings(result)).SourceReferences.Select(r => r.SourceRecordIndex));
        Assert.Equal(4, result.Candidates.Select(c => c.SourceRow.SourceRecordIndex).Distinct().Count());
    }

    [Fact]
    public void SeparateReportedIntervalsRemainSeparateWithoutClaimingVerifiedAccountFlatness()
    {
        TopstepTradeReconstructionResult result = Reconstruct(Row(1, 0, 10), Row(2, 20, 30));
        Assert.Equal(2, result.Candidates.Count);
        Assert.Empty(RelationshipWarnings(result));
        Assert.False(result.IsPositionGroupingVerified);
        Assert.All(result.Candidates, c => Assert.False(c.ArePositionBoundariesVerified));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(11)]
    public void DirectionChangesNeverManufactureAReversalFillOrNetOpposingRows(int nextEntry)
    {
        TopstepTradeReconstructionResult result = Reconstruct(
            Row(1, 0, 10), Row(2, nextEntry, 20) with { Type = TopstepTradeType.Short, Size = 3m });

        Assert.Equal([TradeDirection.Long, TradeDirection.Short], result.Candidates.Select(c => c.Direction));
        Assert.Equal([2m, 3m], result.Candidates.Select(c => c.Quantity));
        Assert.Equal(nextEntry <= 10 ? 1 : 0, RelationshipWarnings(result).Count());
        Assert.True(result.CanUseRowCandidates);
    }

    [Fact]
    public void SameSecondRoundTripNeedsNoGuessedCrossRowFillOrder()
    {
        TopstepTradeReconstructionResult result = Reconstruct(
            Row(1, 0, 0), Row(2, 0, 0) with { Type = TopstepTradeType.Short });
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, c => Assert.Equal(c.OpenedAtUtc, c.ClosedAtUtc));
        Assert.Single(RelationshipWarnings(result));
    }

    [Fact]
    public void ContractsAreIndependentButTradeDayIsNotUsedToInventBoundaries()
    {
        TopstepSourceRow first = Row(1);
        TopstepSourceRow second = Row(2) with { SourceTradeDay = first.SourceTradeDay.AddDays(1) };
        TopstepSourceRow otherContract = Row(3) with { ContractName = "MNQH7" };
        TopstepTradeReconstructionResult result = Reconstruct(first, second, otherContract);

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal([1, 2], Assert.Single(RelationshipWarnings(result)).SourceReferences.Select(r => r.SourceRecordIndex));
        Assert.Equal(second.SourceTradeDay, result.Candidates[1].SourceRow.SourceTradeDay);
        Assert.Equal(TimeSpan.FromHours(-5), result.Candidates[1].SourceRow.SourceTradeDay.Offset);
    }

    [Fact]
    public void OffsetEquivalentIntervalsAreRelatedWithoutChangingTheirSourceOffsets()
    {
        TopstepSourceRow first = Row(1);
        TopstepSourceRow second = Row(2) with
        {
            SourceEnteredAt = first.SourceEnteredAt.ToOffset(TimeSpan.FromHours(-4)),
            SourceExitedAt = first.SourceExitedAt.ToOffset(TimeSpan.FromHours(-4)),
        };
        TopstepTradeReconstructionResult result = Reconstruct(first, second);
        Assert.Single(RelationshipWarnings(result));
        Assert.Equal(result.Candidates[0].OpenedAtUtc, result.Candidates[1].OpenedAtUtc);
        Assert.Equal(TimeSpan.FromHours(-4), result.Candidates[1].SourceRow.SourceEnteredAt.Offset);
    }

    [Fact]
    public void SourceReferencesLinkEveryCandidateAndDiagnosticWithoutLeakingIds()
    {
        TopstepSourceRow[] rows = [Row(1) with { SourceLineNumber = 4 }, Row(2) with { SourceLineNumber = 7 }];
        TopstepTradeReconstructionResult result = Reconstruct(rows);

        Assert.Equal(rows, result.Candidates.Select(c => c.SourceRow));
        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal([new TopstepSourceReference(1, 4), new TopstepSourceReference(2, 7)], d.SourceReferences);
            Assert.All(rows, row => Assert.DoesNotContain(row.Id, d.Message));
        });
    }

    [Fact]
    public void DeterministicOrderUsesSourceLocationsForPresentationNotBrokerIdsOrFillSequencing()
    {
        TopstepSourceRow first = Row(1, 20, 30) with { Id = "Z-SYNTH" };
        TopstepSourceRow second = Row(2, 0, 10) with { Id = "A-SYNTH" };
        TopstepTradeReconstructionResult left = Reconstruct(first, second);
        TopstepTradeReconstructionResult right = Reconstruct(second, first);

        Assert.Equal([first, second], left.Candidates.Select(c => c.SourceRow));
        Assert.Equal(left.Candidates.Select(c => c.SourceRow), right.Candidates.Select(c => c.SourceRow));
        Assert.Equal(left.Diagnostics.Select(d => (d.Code, d.Message)), right.Diagnostics.Select(d => (d.Code, d.Message)));
        Assert.Equal(left.Diagnostics.SelectMany(d => d.SourceReferences), right.Diagnostics.SelectMany(d => d.SourceReferences));
    }

    [Theory]
    [InlineData(TopstepTradeType.Long, 5)]
    [InlineData(TopstepTradeType.Short, -5)]
    public void DomainCanRepresentRowLocalEconomicsWithoutClaimingBrokerFillIdentities(TopstepTradeType type, int gross)
    {
        TopstepTradeCandidate candidate = Assert.Single(Reconstruct(Row(1, 0, 0) with { Type = type }).Candidates);
        // Test-only representation proof: internal entity IDs are not recovered broker fills.
        Guid tradeId = Guid.NewGuid();
        ExecutionSide entrySide = candidate.Direction == TradeDirection.Long ? ExecutionSide.Buy : ExecutionSide.Sell;
        ExecutionSide exitSide = entrySide == ExecutionSide.Buy ? ExecutionSide.Sell : ExecutionSide.Buy;
        var entry = new TradeExecution(tradeId, 1, candidate.OpenedAtUtc, entrySide, candidate.Quantity,
            candidate.EntryPrice, null, null, null, null, candidate.ContractName);
        var exit = new TradeExecution(tradeId, 2, candidate.ClosedAtUtc, exitSide, candidate.Quantity,
            candidate.ExitPrice, null, null, null, null, candidate.ContractName);
        Trade trade = Trade.Start(Guid.NewGuid(), Guid.NewGuid(), new TradePricingSnapshot(2m, "USD"), entry, candidate.OpenedAtUtc);
        trade.AddExecution(exit, candidate.ClosedAtUtc);

        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(candidate.Direction, trade.Direction);
        Assert.Equal(candidate.EntryPrice, trade.AverageEntryPrice);
        Assert.Equal(candidate.ExitPrice, trade.AverageExitPrice);
        Assert.Equal((decimal)gross, trade.GrossPnL);
        Assert.Null(trade.NetPnL);
        Assert.All(trade.Executions, execution => Assert.Null(execution.ExternalExecutionId));
    }

    [Fact]
    public async Task ParserToCandidatesIsReadOnlyAndKeepsReportedFieldsWithoutReconciliation()
    {
        string second = TopstepCsvFixtures.Replace(0, "000SYNTH02");
        second = TopstepCsvFixtures.Replace(7, "-17.25", second);
        second = TopstepCsvFixtures.Replace(6, "-0.50", second);
        byte[] bytes = Encoding.UTF8.GetBytes(TopstepCsvFixtures.WithRows(TopstepCsvFixtures.Row, second));
        await using var stream = new MemoryStream(bytes, writable: false);
        TopstepCsvParseResult parsed = await new TopstepCsvParser().ParseAsync(stream);
        TopstepSourceRow[] before = parsed.Rows.ToArray();

        TopstepTradeReconstructionResult result = _reconstructor.Reconstruct(parsed);

        Assert.Same(parsed, result.Source);
        Assert.Equal(before, parsed.Rows);
        Assert.Equal(before, result.Candidates.Select(c => c.SourceRow));
        Assert.Equal(-17.25m, result.Candidates[1].SourceRow.SourceReportedPnL);
        Assert.Equal(-0.50m, result.Candidates[1].SourceRow.SourceReportedFees);
        Assert.Equal(bytes, stream.ToArray());
        Assert.True(stream.CanRead);
        Assert.True(result.CanUseRowCandidates);
    }

    [Theory]
    [InlineData("")]
    [InlineData(TopstepCsvFixtures.Header)]
    [InlineData(TopstepCsvFixtures.Header + "\n" + TopstepCsvFixtures.Row + "\ninvalid")]
    [InlineData(TopstepCsvFixtures.Header + "\n" + TopstepCsvFixtures.Row + "\n" + TopstepCsvFixtures.Row)]
    public async Task InvalidOrIncompleteParserResultsBlockAllCandidatesAndRetainParserDiagnostics(string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        TopstepCsvParseResult parsed = await new TopstepCsvParser().ParseAsync(stream);
        TopstepTradeReconstructionResult result = _reconstructor.Reconstruct(parsed);
        Assert.Empty(result.Candidates);
        Assert.False(result.CanUseRowCandidates);
        Assert.Same(parsed, result.Source);
        Assert.NotEmpty(result.Source.Diagnostics);
        Assert.Equal(TopstepReconstructionDiagnosticCodes.SourceNotValid, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("direction")]
    [InlineData("time")]
    [InlineData("identity")]
    [InlineData("location")]
    [InlineData("contract")]
    public void InvalidConstructedNormalizedRowsBlockRatherThanCreatingMisleadingCandidates(string problem)
    {
        TopstepSourceRow row = problem switch
        {
            "quantity" => Row(1) with { Size = 0m },
            "direction" => Row(1) with { Type = (TopstepTradeType)99 },
            "time" => Row(1, 10, 0),
            "identity" => Row(1) with { Id = " " },
            "location" => Row(1) with { SourceRecordIndex = 0 },
            _ => Row(1) with { ContractName = " " },
        };
        TopstepTradeReconstructionResult result = Reconstruct(row);
        Assert.False(result.CanUseRowCandidates);
        Assert.Empty(result.Candidates);
        Assert.Equal(TopstepReconstructionDiagnosticCodes.InvalidNormalizedRow, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("record")]
    [InlineData("line")]
    public void ConstructedNormalizedRowsMustHaveUniqueSourceIdentityAndLocations(string duplicate)
    {
        TopstepSourceRow second = duplicate switch
        {
            "id" => Row(2) with { Id = Row(1).Id },
            "record" => Row(2) with { SourceRecordIndex = 1 },
            _ => Row(2) with { SourceLineNumber = 2 },
        };
        TopstepTradeReconstructionResult result = Reconstruct(Row(1), second);
        Assert.False(result.CanUseRowCandidates);
        Assert.Empty(result.Candidates);
        Assert.Equal(TopstepReconstructionDiagnosticCodes.SourceIdentityNotUnique, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void CancellationPropagatesEvenBeforeInvalidInputHandling()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            _reconstructor.Reconstruct(new([], [], 0, 0, false), cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            _reconstructor.Reconstruct(new([Row(1)], [], 1, 0, true), cancellation.Token));
    }

    private TopstepTradeReconstructionResult Reconstruct(params TopstepSourceRow[] rows) =>
        _reconstructor.Reconstruct(new(rows, [], rows.Length, 0, true));

    private static IEnumerable<TopstepReconstructionDiagnostic> RelationshipWarnings(TopstepTradeReconstructionResult result) =>
        result.Diagnostics.Where(d => d.Code == TopstepReconstructionDiagnosticCodes.PositionGroupingAmbiguous);

    private static TopstepSourceRow Row(int index, int entrySecond = 0, int exitSecond = 10)
    {
        var start = new DateTimeOffset(2026, 7, 10, 17, 0, 0, TimeSpan.FromHours(3));
        return new(index, index + 1, $"SYNTH-{index}", "MNQZ6", start.AddSeconds(entrySecond), start.AddSeconds(exitSecond),
            20000.125m, 20001.375m, 1.44m, 5m, 2m, TopstepTradeType.Long,
            new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.FromHours(-5)),
            TimeSpan.FromTicks(11234567), "00:00:01.1234567", 1m);
    }
}
