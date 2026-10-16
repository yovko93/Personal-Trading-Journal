using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class SavedPeriodReviewTests
{
    private static readonly PeriodReviewQuery Query = new(ReviewPeriodKind.Week, new(2026, 11, 1));
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static PeriodCoachingResult Generation(Guid? account = null)
    {
        var p = PeriodCoachingTests.Packet(PeriodCoachingTests.Evidence(new(Query.Kind, Query.FirstDate, account)));
        return new(PeriodCoachingStatus.Success, PeriodCoachingResponseValidator.Validate(PeriodCoachingTests.Review(p), p).Response, p,
            new("Groq", "openai/gpt-oss-120b", "synthetic", "req_synthetic", "chatcmpl-synthetic", new(20, null, 10, 30), ErrorCode: "not persisted"), "not persisted");
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void SnapshotOwnsExactPeriodPacketAndAllowlistedMetadata(bool exact)
    {
        var g = Generation(exact ? Guid.NewGuid() : null); var saved = PeriodReviewSnapshot.Create(g, Now).Review;
        Assert.Equal(g.Packet!.Json, saved.EvidenceJson); Assert.Equal(g.Packet.Query.FirstDate, saved.Summary.FirstDate);
        Assert.Equal(g.Packet.Query.BeforeUtc, saved.Summary.BeforeUtc); Assert.Equal(g.Packet.Query.Scope.AccountId, saved.Summary.Scope.AccountId);
        Assert.Equal(exact ? "Synthetic" : "All accounts", saved.Summary.AccountDisplayName);
        Assert.Equal(Now, saved.Summary.GeneratedAtUtc); Assert.Equal(g.Metadata!.Usage, saved.Metadata.Usage);
        Assert.Equal(g.Packet.ContractVersion, saved.ResponseContractVersion); Assert.DoesNotContain("not persisted", saved.ResponseJson);
        Assert.DoesNotContain("Synthetic observation", saved.ToString());
    }
    [Theory]
    [InlineData("failed")] [InlineData("packet")] [InlineData("response")] [InlineData("identity")]
    [InlineData("metadata")] [InlineData("provider")] [InlineData("usage")] [InlineData("utc")]
    public void InvalidSnapshotsAreRejectedBeforePersistence(string problem)
    {
        var g = Generation();
        g = problem switch
        {
            "failed" => g with { Status = PeriodCoachingStatus.TooLarge }, "packet" => g with { Packet = null },
            "response" => g with { Review = null }, "identity" => g with { Review = g.Review! with { PacketId = "wrong" } },
            "metadata" => g with { Metadata = null }, "provider" => g with { Metadata = g.Metadata! with { Provider = "private text" } },
            "usage" => g with { Metadata = g.Metadata! with { Usage = new(-1, null, 10, 9) } }, _ => g,
        };
        Assert.Throws<ArgumentException>(() => PeriodReviewSnapshot.Create(g, problem == "utc" ? Now.ToOffset(TimeSpan.FromHours(1)) : Now));
    }
    [Fact]
    public async Task ExplicitOrchestrationReadsOnceSavesOnceAndNeverRebuildsEvidence()
    {
        var reader = new Reader(); var provider = new Provider(); var store = new Store(); var service = Service(reader, provider, store);
        Assert.Equal(0, reader.Reads); Assert.Equal(0, provider.Calls); Assert.Equal(0, store.Saves);
        var result = await service.GenerateAsync(Query);
        Assert.Equal(SavedCoachingGenerationStatus.Saved, result.Status); Assert.Equal(CoachingGenerationPhase.Saved, result.Diagnostics!.Phase);
        Assert.Equal(1, reader.Reads); Assert.Equal(1, provider.Calls); Assert.Equal(1, store.Saves);
        Assert.Same(store.Snapshot!.Review, result.Review); Assert.Equal(provider.Packet!.Json, result.Review!.EvidenceJson);
    }
    [Theory]
    [InlineData(CoachingGenerationStatus.Cancelled)] [InlineData(CoachingGenerationStatus.InputTooLarge)]
    [InlineData(CoachingGenerationStatus.TimedOut)] [InlineData(CoachingGenerationStatus.InvalidResponse)]
    [InlineData(CoachingGenerationStatus.MissingCredentials)] [InlineData(CoachingGenerationStatus.RateLimited)]
    public async Task FailedGenerationsNeverSave(CoachingGenerationStatus status)
    {
        var provider = new Provider { Status = status }; var store = new Store();
        var result = await Service(new(), provider, store).GenerateAsync(Query);
        Assert.NotEqual(SavedCoachingGenerationStatus.Saved, result.Status); Assert.Null(result.Review);
        Assert.Equal(1, provider.Calls); Assert.Equal(0, store.Saves);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task StorageFailureOrCancellationNeverReportsUnsavedResponseAsSuccess(bool cancel)
    {
        var store = new Store { Error = cancel ? new OperationCanceledException() : new IOException("private content") }; var provider = new Provider();
        var result = await Service(new(), provider, store).GenerateAsync(Query);
        Assert.Equal(cancel ? SavedCoachingGenerationStatus.Cancelled : SavedCoachingGenerationStatus.StorageFailed, result.Status);
        Assert.Null(result.Review); Assert.DoesNotContain("private", result.Message); Assert.Equal(1, provider.Calls); Assert.Equal(1, store.Saves);
    }
    [Fact]
    public async Task PreCancellationWritesNothingAndPagesAreBounded()
    {
        var reader = new Reader(); var provider = new Provider(); var store = new Store();
        Assert.Equal(SavedCoachingGenerationStatus.Cancelled, (await Service(reader, provider, store).GenerateAsync(Query, new(true))).Status);
        Assert.Equal(0, reader.Reads); Assert.Equal(0, provider.Calls); Assert.Equal(0, store.Saves);
        Assert.Throws<OperationCanceledException>(() => PeriodReviewSnapshot.Create(Generation(), Now, new(true)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodReviewHistoryQuery(Query, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodReviewHistoryQuery(Query, 1, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodReviewHistoryQuery(Query, int.MaxValue, 100));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SuccessfulTransportWithInvalidCitationOrMetadataNeverSaves(bool metadata)
    {
        var provider = new Provider { InvalidMetadata = metadata, InvalidCitation = !metadata }; var store = new Store();
        var result = await Service(new(), provider, store).GenerateAsync(Query);
        Assert.Equal(metadata ? SavedCoachingGenerationStatus.InvalidGeneration : SavedCoachingGenerationStatus.GenerationFailed, result.Status);
        Assert.Null(result.Review); Assert.Equal(1, provider.Calls); Assert.Equal(0, store.Saves);
    }
    private static GenerateAndSavePeriodReviewService Service(Reader reader, Provider provider, Store store) =>
        new(new(reader, provider, new(), TimeProvider.System), store, TimeProvider.System);
    private sealed class Reader : IPeriodComparisonEvidenceReader
    {
        public int Reads;
        public Task<PeriodComparisonEvidenceResult> ReadComparisonAsync(PeriodReviewQuery query, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, PeriodCoachingTests.Evidence(query), null)); }
    }
    private sealed class Provider : IPeriodCoachingProvider
    {
        public int Calls; public PeriodCoachingPacket? Packet; public CoachingGenerationStatus Status = CoachingGenerationStatus.Success;
        public bool InvalidCitation, InvalidMetadata;
        public Task<CoachingProviderReply> GenerateAsync(PeriodCoachingPacket packet, CancellationToken token)
        {
            Calls++; Packet = packet; string response = PeriodCoachingTests.Review(packet);
            if (InvalidCitation) response = response.Replace("current:statistics", "foreign");
            return Task.FromResult(new CoachingProviderReply(Status, response, new("Fake", "synthetic", InvalidMetadata ? "not an identifier" : "client")));
        }
    }
    private sealed class Store : IPeriodReviewRepository
    {
        public int Saves; public PeriodReviewSnapshot? Snapshot; public Exception? Error;
        public Task<SavedPeriodReview> SaveAsync(PeriodReviewSnapshot snapshot, CancellationToken cancellationToken = default)
        { Saves++; Snapshot = snapshot; return Error is null ? Task.FromResult(snapshot.Review) : Task.FromException<SavedPeriodReview>(Error); }
        public Task<SavedPeriodReview?> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PeriodReviewHistoryPage> BrowseAsync(PeriodReviewHistoryQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SavedPeriodScopePage> BrowseScopesAsync(PeriodReviewHistoryQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PeriodReviewDeleteStatus> DeleteAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
