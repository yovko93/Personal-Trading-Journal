using PersonalTradingJournal.Application.Imports.Topstep;

namespace PersonalTradingJournal.Application.Tests.Imports.Topstep;

public sealed class TopstepTradeCandidateContractTests
{
    [Fact]
    public void ResultAndDiagnosticSnapshotTheirCollectionsAndRetainSourceEvidence()
    {
        TopstepSourceRow row = Row();
        var source = new TopstepCsvParseResult([row], [], 1, 0, true);
        var candidates = new List<TopstepTradeCandidate> { new(row) };
        var locations = new List<TopstepSourceReference> { new(1, 2) };
        var diagnostic = new TopstepReconstructionDiagnostic(TopstepReconstructionDiagnosticSeverity.Warning,
            TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified, locations, "Unverified position boundary.");
        var diagnostics = new List<TopstepReconstructionDiagnostic> { diagnostic };
        var result = new TopstepTradeReconstructionResult(source, candidates, diagnostics);

        candidates.Clear();
        diagnostics.Clear();
        locations.Clear();

        Assert.Same(source, result.Source);
        Assert.Same(row, Assert.Single(result.Candidates).SourceRow);
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
        Assert.Equal(new TopstepSourceReference(1, 2), Assert.Single(diagnostic.SourceReferences));
        Assert.True(result.CanUseRowCandidates);
        Assert.False(result.IsPositionGroupingVerified);
    }

    [Fact]
    public void EmptyErroredOrIncompleteResultsAreNotReadyForRowReview()
    {
        TopstepSourceRow row = Row();
        var source = new TopstepCsvParseResult([row], [], 1, 0, true);
        Assert.False(new TopstepTradeReconstructionResult(source, [], []).CanUseRowCandidates);
        Assert.False(new TopstepTradeReconstructionResult(source, [new(row)],
            [new(TopstepReconstructionDiagnosticSeverity.Error, "ERROR", [], "Synthetic error.")]).CanUseRowCandidates);
        Assert.False(new TopstepTradeReconstructionResult(new([row], [], 2, 1, true), [new(row)], []).CanUseRowCandidates);
    }

    [Fact]
    public void CandidateDoesNotAcceptUnrepresentableDirectionalLifecycle()
    {
        TopstepSourceRow row = Row();
        Assert.Throws<ArgumentNullException>(() => new TopstepTradeCandidate(null!));
        Assert.Throws<ArgumentException>(() => new TopstepTradeCandidate(row with { Size = 0m }));
        Assert.Throws<ArgumentException>(() => new TopstepTradeCandidate(row with { Type = (TopstepTradeType)99 }));
        Assert.Throws<ArgumentException>(() => new TopstepTradeCandidate(row with { SourceExitedAt = row.SourceEnteredAt.AddSeconds(-1) }));
    }

    private static TopstepSourceRow Row() => new(
        1, 2, "SYNTH-ROW", "MNQZ6",
        new DateTimeOffset(2026, 7, 10, 17, 0, 0, TimeSpan.FromHours(3)),
        new DateTimeOffset(2026, 7, 10, 17, 0, 1, TimeSpan.FromHours(3)),
        20000m, 20001m, 1.44m, 4m, 2m, TopstepTradeType.Long,
        new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.FromHours(-5)),
        TimeSpan.FromSeconds(1), "00:00:01", 1m);
}
