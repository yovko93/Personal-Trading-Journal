using System.Text.Json;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class CoachingGenerationTests
{
    private static CoachingEvidencePacket Packet() =>
        CoachingEvidencePacketBuilder.Build(new DailyReviewEvidence(new(new DateOnly(2026, 10, 8)), [], [])).Packet!;

    private static string Json(CoachingEvidencePacket packet, string? id = null, string source = "calculated:day") =>
        JsonSerializer.Serialize(new
        {
            contractVersion = packet.ContractVersion, packetId = id ?? packet.PacketId,
            daySummary = new { text = "No recorded activity.", sourceIds = new[] { source } },
            executionObservations = Array.Empty<object>(), behaviorObservations = Array.Empty<object>(),
            improvementSuggestions = Array.Empty<object>(), uncertainties = Array.Empty<object>(),
        });

    [Fact]
    public async Task ExplicitInvocationOnlySendsSameImmutablePacketOnceAndReturnsValidatedReviewAndUsage()
    {
        var packet = Packet();
        var metadata = new CoachingRequestMetadata("Fake", "test", "client", Usage: new(12, 0, 5, 17));
        var provider = new Fake((p, _) =>
        {
            Assert.Same(packet, p);
            return Task.FromResult(new CoachingProviderReply(CoachingGenerationStatus.Success, Json(p), metadata));
        });
        var service = new DailyCoachingGenerationService(provider, new(), TimeProvider.System);
        Assert.Equal(0, provider.Calls);
        var result = await service.GenerateAsync(packet);
        Assert.Equal(CoachingGenerationStatus.Success, result.Status);
        Assert.NotNull(result.Review);
        Assert.Same(metadata, result.Metadata);
        Assert.Null(result.Metadata!.MonetaryCost);
        Assert.Equal(17, result.Metadata.Usage!.TotalTokens);
        Assert.Equal(1, provider.Calls);
        Assert.DoesNotContain("No recorded", result.ToString());
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("identity")]
    [InlineData("citation")]
    [InlineData("missing")]
    public async Task RejectsInvalidCompleteResponse(string failure)
    {
        var packet = Packet();
        string? json = failure switch
        {
            "malformed" => "{", "identity" => Json(packet, "wrong"),
            "citation" => Json(packet, source: "trade:foreign"), _ => null,
        };
        var provider = new Fake((_, _) => Task.FromResult(new CoachingProviderReply(CoachingGenerationStatus.Success, json, null)));
        var result = await new DailyCoachingGenerationService(provider, new(), TimeProvider.System).GenerateAsync(packet);
        Assert.Equal(CoachingGenerationStatus.InvalidResponse, result.Status);
        Assert.Null(result.Review);
        Assert.Equal(1, provider.Calls);
    }

    public static IEnumerable<object[]> Failures => Enum.GetValues<CoachingGenerationStatus>()
        .Where(s => s != CoachingGenerationStatus.Success).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailureNeverReturnsPartialReviewOrRetries(CoachingGenerationStatus status)
    {
        var provider = new Fake((p, _) => Task.FromResult(new CoachingProviderReply(status, Json(p), null)));
        var result = await new DailyCoachingGenerationService(provider, new(), TimeProvider.System).GenerateAsync(Packet());
        Assert.Equal(status, result.Status);
        Assert.Null(result.Review);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationAndDeadlineBoundNoncooperativeProviderAndDiscardLateSuccess(bool callerCancellation)
    {
        var pending = new TaskCompletionSource<CoachingProviderReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var provider = new Fake((_, token) => { observed = token; return pending.Task; });
        var clock = new ManualClock();
        using var caller = new CancellationTokenSource();
        var packet = Packet();
        var task = new DailyCoachingGenerationService(provider, new(), clock).GenerateAsync(packet, caller.Token);
        Assert.Equal(1, provider.Calls);
        if (callerCancellation) caller.Cancel(); else clock.Fire();
        var result = await task;
        Assert.True(observed.IsCancellationRequested);
        Assert.Equal(callerCancellation ? CoachingGenerationStatus.Cancelled : CoachingGenerationStatus.TimedOut, result.Status);
        pending.SetResult(new(CoachingGenerationStatus.Success, Json(packet), null));
        Assert.Null(result.Review);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task PrecancelledAndInvalidConfigurationNeverInvokeProvider()
    {
        var provider = new Fake((_, _) => throw new InvalidOperationException());
        var service = new DailyCoachingGenerationService(provider, new(), TimeProvider.System);
        Assert.Equal(CoachingGenerationStatus.Cancelled,
            (await service.GenerateAsync(Packet(), new CancellationToken(true))).Status);
        Assert.Equal(CoachingGenerationStatus.InvalidConfiguration,
            (await new DailyCoachingGenerationService(provider, new() { Timeout = TimeSpan.Zero }, TimeProvider.System)
                .GenerateAsync(Packet())).Status);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ProviderExceptionDoesNotExposeSourceOrCredential()
    {
        var provider = new Fake((_, _) => throw new InvalidOperationException("secret-key private-journal"));
        var result = await new DailyCoachingGenerationService(provider, new(), TimeProvider.System).GenerateAsync(Packet());
        Assert.Equal(CoachingGenerationStatus.ProviderFailure, result.Status);
        Assert.DoesNotContain("secret-key", result.Message);
        Assert.DoesNotContain("private-journal", result.Message);
    }

    private sealed class Fake(Func<CoachingEvidencePacket, CancellationToken, Task<CoachingProviderReply>> run) : ICoachingProvider
    {
        public int Calls { get; private set; }
        public Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken token)
        { Calls++; return run(packet, token); }
    }

    private sealed class ManualClock : TimeProvider
    {
        private Action? fire;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { fire = () => callback(state); return new TimerStub(); }
        public void Fire() => fire!();
        private sealed class TimerStub : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
