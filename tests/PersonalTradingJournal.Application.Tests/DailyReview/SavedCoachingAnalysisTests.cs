using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class SavedCoachingAnalysisTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 20, 0, 0, TimeSpan.Zero);
    private static CoachingEvidencePacket Packet() =>
        CoachingEvidencePacketBuilder.Build(new DailyReviewEvidence(new(new DateOnly(2026, 10, 8)), [], [])).Packet!;
    private static CoachingGenerationResult Good(CoachingEvidencePacket packet) => new(CoachingGenerationStatus.Success,
        new(packet.ContractVersion, packet.PacketId, new("No records.", new[] { "calculated:day" }), [], [], [], []),
        new("Fake", "test-model", "client-test", "req_test", "resp_test", new(10, 0, 5, 15)), "Not persisted");

    [Fact]
    public void SnapshotCopiesExactPacketValidatedResponseAndAllowlistedMetadata()
    {
        var packet = Packet();
        var result = Good(packet);
        var sources = new List<string> { "calculated:day" };
        result = result with { Review = result.Review! with { DaySummary = new("No records.", sources) } };
        var snapshot = CoachingAnalysisSnapshot.Create(packet, result, Now);
        sources.Clear();
        Assert.Equal(packet.Json, snapshot.Analysis.EvidenceJson);
        Assert.Contains("calculated:day", snapshot.Analysis.ResponseJson);
        Assert.Equal(packet.PacketId, snapshot.Analysis.PacketId);
        Assert.Equal(CoachingAnalysisScopeKind.AllAccounts, snapshot.Analysis.Summary.Scope.Kind);
        Assert.Equal("All accounts", snapshot.Analysis.Summary.AccountDisplayName);
        Assert.Equal(Now, snapshot.Analysis.Summary.GeneratedAtUtc);
        Assert.Equal(result.Metadata!.Usage, snapshot.Analysis.Metadata.Usage);
        Assert.DoesNotContain("Not persisted", snapshot.Analysis.ResponseJson);
        Assert.DoesNotContain("No records", snapshot.ToString());
    }

    public static IEnumerable<object[]> Failures => Enum.GetValues<CoachingGenerationStatus>()
        .Where(s => s != CoachingGenerationStatus.Success).Select(s => new object[] { s });
    [Theory]
    [MemberData(nameof(Failures))]
    public void EveryUnsuccessfulGenerationIsUnsavable(CoachingGenerationStatus status)
    {
        var packet = Packet();
        Assert.Throws<ArgumentException>(() => CoachingAnalysisSnapshot.Create(packet, Good(packet) with { Status = status }, Now));
    }

    [Theory]
    [InlineData("citation")]
    [InlineData("packet")]
    [InlineData("metadata")]
    [InlineData("usage")]
    [InlineData("utc")]
    public void InvalidSuccessfulResultCannotCrossWriteBoundary(string defect)
    {
        var packet = Packet();
        var result = Good(packet);
        result = defect switch
        {
            "citation" => result with { Review = result.Review! with { DaySummary = new("Claim", new[] { "foreign" }) } },
            "packet" => result with { Review = result.Review! with { PacketId = "foreign" } },
            "metadata" => result with { Metadata = null },
            "usage" => result with { Metadata = result.Metadata! with { Usage = new(10, 0, 5, 100) } },
            _ => result,
        };
        Assert.Throws<ArgumentException>(() => CoachingAnalysisSnapshot.Create(packet, result,
            defect == "utc" ? Now.ToOffset(TimeSpan.FromHours(2)) : Now));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void PagingIsBounded(int page, int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new CoachingAnalysisHistoryQuery(new(2026, 10, 8), new(), page, size));

    [Fact]
    public async Task ExplicitGenerationAndSaveOnlyReturnsSavedAfterRepositorySuccess()
    {
        var packet = Packet();
        var provider = new FakeProvider(Good(packet));
        var repository = new FakeRepository();
        var service = new GenerateAndSaveCoachingService(new(provider, new(), TimeProvider.System), repository, new Clock());
        Assert.Equal(0, provider.Calls);
        var result = await service.GenerateAsync(packet);
        Assert.Equal(SavedCoachingGenerationStatus.Saved, result.Status);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, repository.Writes);
        Assert.Same(repository.Snapshot!.Analysis, result.Analysis);
        Assert.Equal(Now, result.Analysis!.Summary.GeneratedAtUtc);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task NoHistoryWriteAfterGenerationFailure(CoachingGenerationStatus status)
    {
        var packet = Packet();
        var provider = new FakeProvider(Good(packet) with { Status = status });
        var repository = new FakeRepository();
        var result = await new GenerateAndSaveCoachingService(new(provider, new(), TimeProvider.System), repository, new Clock())
            .GenerateAsync(packet);
        Assert.NotEqual(SavedCoachingGenerationStatus.Saved, result.Status);
        Assert.Null(result.Analysis);
        Assert.Equal(0, repository.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageFailureOrCancellationIsNotReportedAsStoredAndNeverRetries(bool cancel)
    {
        var packet = Packet();
        var provider = new FakeProvider(Good(packet));
        var repository = new FakeRepository { Failure = cancel ? new OperationCanceledException() : new IOException("Private evidence") };
        var result = await new GenerateAndSaveCoachingService(new(provider, new(), TimeProvider.System), repository, new Clock())
            .GenerateAsync(packet);
        Assert.Equal(cancel ? SavedCoachingGenerationStatus.Cancelled : SavedCoachingGenerationStatus.StorageFailed, result.Status);
        Assert.Null(result.Analysis);
        Assert.DoesNotContain("Private evidence", result.Message);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, repository.Writes);
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FakeProvider(CoachingGenerationResult result) : ICoachingProvider
    {
        public int Calls { get; private set; }
        public Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken token)
        {
            Calls++;
            // Wire representation deliberately goes through the real M15.3 validator in generation.
            string json = $$"""
                {"contractVersion":"{{packet.ContractVersion}}","packetId":"{{packet.PacketId}}",
                "daySummary":{"text":"No records.","sourceIds":["calculated:day"]},
                "executionObservations":[],"behaviorObservations":[],"improvementSuggestions":[],"uncertainties":[]}
                """;
            return Task.FromResult(new CoachingProviderReply(result.Status, json, result.Metadata));
        }
    }
    private sealed class FakeRepository : ICoachingAnalysisRepository
    {
        public Task<HistoricalCoachingAccountPage> BrowseHistoricalAccountsAsync(HistoricalCoachingAccountQuery query, CancellationToken token) => throw new NotSupportedException();
        public Exception? Failure { get; init; }
        public int Writes { get; private set; }
        public CoachingAnalysisSnapshot? Snapshot { get; private set; }
        public Task<SavedCoachingAnalysis> SaveAsync(CoachingAnalysisSnapshot snapshot, CancellationToken token)
        {
            Writes++; Snapshot = snapshot;
            if (Failure is not null) throw Failure;
            return Task.FromResult(snapshot.Analysis);
        }
        public Task<SavedCoachingAnalysis?> GetAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<CoachingAnalysisHistoryPage> BrowseAsync(CoachingAnalysisHistoryQuery query, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
    }
}
