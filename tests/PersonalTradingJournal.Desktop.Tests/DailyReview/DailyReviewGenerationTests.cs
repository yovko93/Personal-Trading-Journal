using System.Text.Json;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class DailyReviewGenerationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalDiscoveryFailureDoesNotBlockCurrentEvidenceOrExplicitGeneration(bool exact)
    {
        var provider = new Provider(); var f = Ready(provider);
        f.History.ScopeHandler = (_, _) => throw new IOException("private discovery details");
        await f.Vm.ActivateAsync();
        if (exact) { f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId); await f.Vm.LoadTask; }
        Assert.NotNull(f.Vm.ScopeError); Assert.Null(f.Vm.CurrentError); Assert.NotNull(f.Vm.Current);
        Assert.True(f.Vm.GenerateCommand.CanExecute(null));
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(1, provider.Calls); Assert.Equal(1, f.History.Writes); Assert.NotNull(f.Vm.Snapshot);
        Assert.Contains("Phase: Saved", f.Vm.GenerationDiagnostics);
        Assert.DoesNotContain("private", f.Vm.GenerationDiagnostics);
        f.Vm.Deactivate();
    }
    [Theory]
    [InlineData("trades")]
    [InlineData("journal")]
    [InlineData("both")]
    public async Task ExplicitClickReadsFreshPacketSavesOnceAndOpensSnapshotWithUsage(string content)
    {
        var provider = new Provider();
        var f = new ReviewFixture(provider: provider);
        f.Reader.Handler = (q, _) =>
        {
            var e = ReviewFixture.Evidence(q);
            return Task.FromResult(e with { Trades = content == "journal" ? [] : e.Trades,
                Journals = content == "trades" ? [] : e.Journals });
        };
        await f.Vm.ActivateAsync();
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        f.Vm.Deactivate(); await f.Vm.ActivateAsync();
        Assert.Equal(0, provider.Calls);
        int reads = f.Reader.Calls;
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(reads + 1, f.Reader.Calls);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, f.History.Writes);
        var saved = Assert.Single(f.History.Items);
        Assert.Equal(provider.Packet!.Json, saved.EvidenceJson);
        Assert.Equal(ReviewFixture.Day, saved.Summary.ReviewDate);
        Assert.Null(saved.Summary.Scope.AccountId);
        Assert.Equal(saved.Summary.Id, Assert.Single(f.Vm.Analyses).Source.Id);
        Assert.NotNull(f.Vm.Snapshot);
        Assert.Contains("total 17", f.Vm.Snapshot.Metadata);
        Assert.Contains("Cost unknown", f.Vm.Snapshot.Metadata);
        Assert.Contains("saved and opened", f.Vm.GenerationMessage);
        Assert.False(f.Vm.IsGenerating);
        f.Vm.SelectedDate = ReviewFixture.Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await f.Vm.LoadTask;
        Assert.Null(f.Vm.GenerationMessage);
        Assert.Null(f.Vm.Snapshot);
        Assert.Equal(1, provider.Calls);
        f.Vm.Deactivate();
    }

    [Fact]
    public async Task DoubleClickSendsOnePacketForCapturedExactScope()
    {
        var pending = new TaskCompletionSource<CoachingProviderReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Provider { Handler = (_, _) => pending.Task };
        var f = Ready(provider);
        await f.Vm.ActivateAsync();
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId);
        await f.Vm.LoadTask;
        var first = f.Vm.GenerateCommand.ExecuteAsync(null);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(f.Vm.GenerateCommand.CanExecute(null));
        await f.Vm.GenerateCommand.ExecuteAsync(null); // Bypasses ICommand.CanExecute deliberately.
        Assert.Equal(1, provider.Calls);
        Assert.Equal(ReviewFixture.AccountId, provider.Packet!.Content.Query.TradingAccountId);
        Assert.All(provider.Packet.Content.RecordedTradeFacts, t => Assert.Equal(ReviewFixture.AccountId, t.Account.Id));
        pending.SetResult(Provider.Success(provider.Packet));
        await first;
        Assert.Equal(1, f.History.Writes);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("date")]
    [InlineData("account")]
    [InlineData("refresh")]
    [InlineData("leave")]
    public async Task CancellationInvalidatesLateProviderWithoutSavingOrChangingNewSelection(string action)
    {
        var pending = new TaskCompletionSource<CoachingProviderReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Provider { Handler = (_, _) => pending.Task };
        var f = Ready(provider);
        await f.Vm.ActivateAsync();
        var generation = f.Vm.GenerateCommand.ExecuteAsync(null);
        var token = await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        switch (action)
        {
            case "date": f.Vm.SelectedDate = ReviewFixture.Day.AddDays(1).ToDateTime(TimeOnly.MinValue); break;
            case "account": f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId); break;
            case "refresh": await f.Vm.RefreshCommand.ExecuteAsync(null); break;
            case "leave": f.Vm.Deactivate(); break;
            default: f.Vm.CancelGenerationCommand.Execute(null); break;
        }
        await f.Vm.LoadTask;
        var date = f.Vm.SelectedDate; var account = f.Vm.SelectedAccount.Id;
        await generation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(token.IsCancellationRequested);
        pending.SetResult(Provider.Success(provider.Packet!));
        Assert.Equal(0, f.History.Writes);
        Assert.Empty(f.History.Items);
        Assert.Null(f.Vm.Snapshot);
        Assert.Equal(date, f.Vm.SelectedDate); Assert.Equal(account, f.Vm.SelectedAccount.Id);
        Assert.Contains("cancelled", f.Vm.GenerationMessage);
        Assert.Equal(1, provider.Calls);
    }

    public static IEnumerable<object[]> Failures => Enum.GetValues<CoachingGenerationStatus>()
        .Where(s => s != CoachingGenerationStatus.Success).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task ProviderFailuresNeverSaveOrReplaceCurrentEvidenceOrExistingHistory(CoachingGenerationStatus status)
    {
        var provider = new Provider { Handler = (_, _) => Task.FromResult(new CoachingProviderReply(status, "SECRET", null)) };
        var f = Ready(provider);
        f.History.Items.Add(ReviewFixture.Saved());
        await f.Vm.ActivateAsync();
        var current = f.Vm.Current; var row = f.Vm.Analyses.Single();
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Same(current, f.Vm.Current);
        Assert.Same(row, f.Vm.Analyses.Single());
        Assert.Equal(0, f.History.Writes);
        Assert.Single(f.History.Items);
        Assert.False(string.IsNullOrWhiteSpace(f.Vm.GenerationMessage));
        Assert.DoesNotContain("SECRET", f.Vm.GenerationMessage);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("citation")]
    [InlineData("identity")]
    [InlineData("storage")]
    public async Task InvalidOrUnsavedResponsesNeverBecomeHistory(string failure)
    {
        var provider = new Provider { Handler = (p, _) => Task.FromResult(Provider.Success(p) with
        {
            ResponseJson = failure switch
            {
                "json" => "SECRET",
                "citation" => Provider.Json(p, source: "trade:unknown"),
                "identity" => Provider.Json(p, id: "wrong-packet"),
                _ => Provider.Json(p)
            }
        }) };
        var f = Ready(provider); f.History.FailSave = failure == "storage";
        await f.Vm.ActivateAsync();
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Empty(f.History.Items); Assert.Empty(f.Vm.Analyses);
        Assert.Null(f.Vm.Snapshot);
        Assert.DoesNotContain("SECRET", f.Vm.GenerationMessage);
        Assert.DoesNotContain("private", f.Vm.GenerationMessage);
        Assert.Contains(failure == "storage" ? "not confirmed saved" : "validation", f.Vm.GenerationMessage);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("blank-journal")]
    [InlineData("oversized")]
    [InlineData("read-failure")]
    [InlineData("deleted-before-click")]
    public async Task NoUsableEvidenceOrUnavailableScopeSendsNothing(string scenario)
    {
        var provider = new Provider();
        var f = Ready(provider);
        await f.Vm.ActivateAsync();
        if (scenario == "deleted-before-click")
        {
            f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId);
            await f.Vm.LoadTask;
            f.Accounts.Items = [];
        }
        else f.Reader.Handler = (q, _) => scenario switch
        {
            "read-failure" => throw new IOException("SECRET"),
            "empty" => Task.FromResult(new DailyReviewEvidence(q, [], [])),
            _ => Task.FromResult(new DailyReviewEvidence(q, [], [ReviewFixture.Journal(null, "All accounts", q.Date) with
            { Text = scenario == "oversized" ? new string('x', 300_000) : " ", Answers = new("", "", "") }]))
        };
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, f.History.Writes);
        Assert.False(string.IsNullOrWhiteSpace(f.Vm.GenerationMessage));
        Assert.DoesNotContain("SECRET", f.Vm.GenerationMessage);
        if (scenario == "deleted-before-click") Assert.False(f.Vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public async Task HistoricalScopeCannotGenerateEvenByExecutingDisabledCommand()
    {
        var p = new Provider(); var f = Ready(p); f.Accounts.Items = [];
        f.History.ScopeHandler = (q, _) => Task.FromResult(new HistoricalCoachingAccountPage(
            [new(ReviewFixture.AccountId, "Deleted P 21")], 1, q.Page, q.PageSize));
        await f.Vm.ActivateAsync();
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId);
        await f.Vm.LoadTask;
        Assert.False(f.Vm.GenerateCommand.CanExecute(null));
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(0, p.Calls);
        Assert.Contains("historical/deleted", f.Vm.GenerationAvailability);
    }

    internal static ReviewFixture Ready(Provider provider)
    {
        var f = new ReviewFixture(provider: provider);
        f.Reader.Handler = (q, _) => Task.FromResult(ReviewFixture.Evidence(q));
        return f;
    }

    [Fact]
    public async Task ProviderDeadlineReportsTimeoutWithoutSavingLateResponse()
    {
        var pending = new TaskCompletionSource<CoachingProviderReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Provider { Handler = (_, _) => pending.Task };
        var clock = new DeadlineClock();
        var f = new ReviewFixture(provider: provider, generationClock: clock);
        f.Reader.Handler = (q, _) => Task.FromResult(ReviewFixture.Evidence(q));
        await f.Vm.ActivateAsync();
        var request = f.Vm.GenerateCommand.ExecuteAsync(null);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        clock.Fire();
        await request.WaitAsync(TimeSpan.FromSeconds(10));
        pending.SetResult(Provider.Success(provider.Packet!));
        Assert.Contains("timed out", f.Vm.GenerationMessage);
        Assert.Equal(0, f.History.Writes);
    }

    private sealed class DeadlineClock : TimeProvider
    {
        private Action? _fire;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { _fire = () => callback(state); return new TimerStub(); }
        internal void Fire() => _fire!();
        private sealed class TimerStub : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    internal sealed class Provider : ICoachingProvider
    {
        internal Func<CoachingEvidencePacket, CancellationToken, Task<CoachingProviderReply>> Handler { get; set; } =
            (p, _) => Task.FromResult(Success(p));
        internal TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        internal CoachingEvidencePacket? Packet;
        public Task<CoachingProviderReply> GenerateAsync(CoachingEvidencePacket packet, CancellationToken token)
        { Interlocked.Increment(ref Calls); Packet = packet; Started.TrySetResult(token); return Handler(packet, token); }
        internal static CoachingProviderReply Success(CoachingEvidencePacket p) =>
            new(CoachingGenerationStatus.Success, Json(p), new("Fake", "test-model", "client-test", Usage: new(12, 0, 5, 17)));
        internal static string Json(CoachingEvidencePacket p, string? id = null, string source = "calculated:day") =>
            JsonSerializer.Serialize(new { contractVersion = p.ContractVersion, packetId = id ?? p.PacketId,
                daySummary = new { text = "Review from supplied evidence.", sourceIds = new[] { source } },
                executionObservations = Array.Empty<object>(), behaviorObservations = Array.Empty<object>(),
                improvementSuggestions = Array.Empty<object>(), uncertainties = Array.Empty<object>() });
    }
}
