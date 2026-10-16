using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

namespace PersonalTradingJournal.Desktop.Tests.PeriodReviews;

public sealed class PeriodWorkspaceTests
{
    [Fact]
    public void SavedV1ReportStillReadsWithoutProjectionOrCurrentSources()
    {
        var saved = PeriodFixture.Saved(new(ReviewPeriodKind.Week, ReviewFixture.Day));
        var root = JsonNode.Parse(saved.EvidenceJson)!; root.AsObject().Remove("providerProjection"); root["contractVersion"] = "period-coaching.v1";
        var response = JsonNode.Parse(saved.ResponseJson)!; response["contractVersion"] = "period-coaching.v1";
        var legacy = saved with { EvidenceContractVersion = "period-coaching.v1", ResponseContractVersion = "period-coaching.v1",
            EvidenceJson = root.ToJsonString(), ResponseJson = response.ToJsonString() };
        var report = PeriodReport.Read(legacy); Assert.Equal(5, report.Cards.Count);
        Assert.Equal(root.ToJsonString(), legacy.EvidenceJson);
    }
    [Fact]
    public async Task HistoricalScopesArePagedAndShrinkingLastPageIsReconciled()
    {
        var f = new PeriodFixture(); var q = f.Vm.Query!;
        for (int i = 0; i < 26; i++) f.Store.Items.Add(PeriodFixture.Saved(new(q.Kind, q.FirstDate, Guid.NewGuid())));
        await f.Vm.ActivateAsync(); Assert.Equal(25, f.Vm.Accounts.Count(a => a.IsHistorical));
        await f.Vm.NextScopesCommand.ExecuteAsync(null); Assert.Single(f.Vm.Accounts, a => a.IsHistorical);
        var original = f.Vm.Accounts.Single(a => a.IsHistorical); f.Vm.SelectedAccount = original; await f.Vm.LoadTask;
        Assert.Single(f.Vm.History); await f.Vm.OpenCommand.ExecuteAsync(f.Vm.History.Single()); Assert.NotNull(f.Vm.Report);
        f.Dialogs.ConfirmationResult = true; await f.Vm.DeleteCommand.ExecuteAsync(f.Vm.History.Single());
        Assert.False(f.Vm.PreviousScopesCommand.CanExecute(null)); Assert.Equal(original.Id, f.Vm.SelectedAccount.Id);
        Assert.Empty(f.Vm.History); Assert.False(f.Vm.GenerateCommand.CanExecute(null)); f.Vm.Deactivate();
    }
    [Fact]
    public async Task RecordedCoverageAndCapturedCitationsStayHistoricalAndHumanReadable()
    {
        var f = new PeriodFixture(); var q = f.Vm.Query!; var pair = PeriodFixture.Pair(q);
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Synthetic principle"), q.FromUtc);
        var target = TradingRuleAssessmentTarget.ForNewYorkDay(q.FirstDate, TradingRuleAccountScope.AllAccounts);
        var a = TradingRuleAssessment.Record(Guid.NewGuid(), rule, target, TradingRuleAssessmentOutcome.CannotEvaluate,
            TradingRuleAssessmentSource.Manual, TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Missing,
            "Observation was not recorded", [], null, q.FromUtc);
        var details = new TradingRuleAssessmentDetails(a, 1, null, TradingRuleSourceState.Unavailable, []);
        var trade = pair.Current.ClosedTrades[0]; var setup = Guid.NewGuid();
        var capture = new ReviewPlaybookAssociation(1, setup, q.FromUtc, q.FromUtc, ReviewPlaybookAssociationState.Current,
            new(setup, "Frozen setup", null, 3, q.FromUtc, "Captured context", "Entry", "Confirm", "Invalid", "Execution",
                [new(Guid.NewGuid(), "Synthetic annotation", "Valid", trade.TradeId, Guid.NewGuid(), true)]));
        pair = pair with { Current = pair.Current with { ApplicableRules = [rule], Assessments = [details],
            ClosedTrades = [trade with { Playbook = new(ReviewPlaybookState.Captured, [capture]) }] } };
        f.Reader.Handler = (_, _) => Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, pair, null));
        await f.Vm.ActivateAsync();
        var coverage = f.Vm.Current!.Rules.Single(r => r.Heading.Contains("New York-day"));
        Assert.Contains("Cannot evaluate: 1", coverage.Text); Assert.Contains("Not assessed: 6", coverage.Text); Assert.Contains("compliant: 0", coverage.Text);
        await f.Vm.GenerateCommand.ExecuteAsync(null); Assert.NotNull(f.Vm.Report);
        var citations = f.Vm.Report.Cards[0].Findings[0].Citations;
        Assert.Contains(citations, c => c.Detail.Contains("Manual user assessment") && c.Detail.Contains("Unavailable") && c.Detail.Contains("All accounts"));
        Assert.Contains(citations, c => c.Detail.Contains("Captured context") && c.Detail.Contains("Synthetic annotation") && c.Label.Contains("revision 3"));
        Assert.DoesNotContain(citations, c => c.Label.Contains(rule.RuleId.ToString()) || c.Label.Contains(trade.TradeId.ToString()));
        f.Vm.Deactivate();
    }
    [Fact]
    public async Task LateDetailAndCancelledDetailCannotReopenAReport()
    {
        var f = new PeriodFixture(); var saved = PeriodFixture.Saved(f.Vm.Query!); f.Store.Items.Add(saved);
        var started = new TaskCompletionSource(); var pending = new TaskCompletionSource<SavedPeriodReview?>();
        f.Store.Detail = (_, _) => { started.TrySetResult(); return pending.Task; };
        await f.Vm.ActivateAsync(); var open = f.Vm.OpenCommand.ExecuteAsync(f.Vm.History.Single()); await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(f.Vm.CancelLoadCommand.CanExecute(null)); f.Vm.CancelLoadCommand.Execute(null);
        f.Vm.SelectedDate = new(2027, 1, 1); await f.Vm.LoadTask; pending.SetResult(saved); await open;
        Assert.Null(f.Vm.Report); Assert.False(f.Vm.IsLoading); Assert.Empty(f.Vm.History); f.Vm.Deactivate();
    }
    [Fact]
    public async Task MissingCredentialsAndOversizedPacketNeverCallProviderOrSave()
    {
        var f = new PeriodFixture(); f.Credentials.Source = CoachingCredentialSource.None; await f.Vm.ActivateAsync();
        Assert.True(f.Vm.NeedsConfiguration); Assert.False(f.Vm.GenerateCommand.CanExecute(null)); await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(0, f.Provider.Calls); f.Credentials.Source = CoachingCredentialSource.Saved;
        var pair = PeriodFixture.Pair(f.Vm.Query!); var j = pair.Current.Journals[0];
        pair = pair with { Current = pair.Current with { Journals = [j with { Text = new string('x', 300_000) }] } };
        f.Reader.Handler = (_, _) => Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, pair, null));
        await f.Vm.RefreshCommand.ExecuteAsync(null); await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(0, f.Provider.Calls); Assert.Equal(0, f.Store.Saves); Assert.Null(f.Vm.Report); Assert.False(string.IsNullOrWhiteSpace(f.Vm.Message)); f.Vm.Deactivate();
    }
    [Theory] [InlineData(ReviewPeriodKind.Week)] [InlineData(ReviewPeriodKind.Month)]
    public async Task ExplicitGenerationSavesFreshSnapshotAndOpensFiveCards(ReviewPeriodKind kind)
    {
        var f = new PeriodFixture(kind); await f.Vm.ActivateAsync(); await f.Vm.RefreshCommand.ExecuteAsync(null);
        f.Vm.Deactivate(); await f.Vm.ActivateAsync(); Assert.Equal(0, f.Provider.Calls);
        Assert.Contains("Net is unavailable", f.Vm.Current!.Coverage); Assert.Equal(2, f.Vm.Current.Metrics.Count);
        await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(1, f.Provider.Calls); Assert.Equal(1, f.Store.Saves); Assert.NotNull(f.Vm.Report);
        Assert.Equal(new[] { "Strengths", "Areas for Improvement", "Mistakes Observed", "Progress", "Action Plan" }, f.Vm.Report.Cards.Select(c => c.Heading));
        Assert.Equal(f.Provider.Packet!.Json, f.Store.Items.Single().EvidenceJson);
        Assert.Contains("Monetary cost unknown", f.Vm.Report.Metadata); Assert.Single(f.Vm.History);
        f.Vm.CloseCommand.Execute(null); Assert.Null(f.Vm.Report); Assert.Single(f.Vm.History);
        await f.Vm.OpenCommand.ExecuteAsync(f.Vm.History.Single()); Assert.NotNull(f.Vm.Report); Assert.Equal(1, f.Provider.Calls);
        f.Vm.Deactivate();
    }
    [Theory] [InlineData("cancel")] [InlineData("date")] [InlineData("scope")] [InlineData("leave")] [InlineData("refresh")]
    public async Task DoubleClickAndCancellationNeverPublishLateResponse(string action)
    {
        var f = new PeriodFixture(); var pending = new TaskCompletionSource<CoachingProviderReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Handler = (_, _) => pending.Task; await f.Vm.ActivateAsync();
        var generation = f.Vm.GenerateCommand.ExecuteAsync(null); var token = await f.Provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Vm.GenerateCommand.ExecuteAsync(null); Assert.Equal(1, f.Provider.Calls);
        Assert.False(f.Vm.TryLeave()); Assert.False(token.IsCancellationRequested);
        if (action == "cancel") f.Vm.CancelGenerationCommand.Execute(null);
        if (action == "date") f.Vm.SelectedDate = new DateTime(2026, 12, 1);
        if (action == "scope") f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId);
        if (action == "leave") { f.Dialogs.ConfirmationResult = true; Assert.True(f.Vm.TryLeave()); f.Vm.Deactivate(); }
        if (action == "refresh") await f.Vm.RefreshCommand.ExecuteAsync(null);
        await generation.WaitAsync(TimeSpan.FromSeconds(10)); await f.Vm.LoadTask;
        Assert.True(token.IsCancellationRequested); pending.SetResult(PeriodFixture.ProviderFake.Success(f.Provider.Packet!));
        Assert.Equal(0, f.Store.Saves); Assert.Null(f.Vm.Report); f.Vm.Deactivate();
    }
    [Theory] [InlineData(CoachingGenerationStatus.MissingCredentials)] [InlineData(CoachingGenerationStatus.RateLimited)]
    [InlineData(CoachingGenerationStatus.TimedOut)] [InlineData(CoachingGenerationStatus.InvalidResponse)] [InlineData(CoachingGenerationStatus.InputTooLarge)]
    [InlineData(CoachingGenerationStatus.ModelUnavailable)] [InlineData(CoachingGenerationStatus.IncompleteResponse)] [InlineData(CoachingGenerationStatus.QuotaExceeded)]
    public async Task FailedGenerationKeepsCurrentEvidenceAndHistoryWithoutRecord(CoachingGenerationStatus status)
    {
        var f = new PeriodFixture(); f.Provider.Handler = (_, _) => Task.FromResult(new CoachingProviderReply(status, null, null));
        await f.Vm.ActivateAsync(); await f.Vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal(0, f.Store.Saves); Assert.Empty(f.Vm.History); Assert.Null(f.Vm.Report); Assert.NotNull(f.Vm.Current);
        Assert.False(string.IsNullOrWhiteSpace(f.Vm.Message)); f.Vm.Deactivate();
    }
    [Fact]
    public async Task EmptyPeriodSendsNothingAndStorageFailureNeverShowsCompletedReview()
    {
        var f = new PeriodFixture(); f.Reader.Handler = (q, _) => Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, PeriodFixture.Pair(q, empty: true), null));
        await f.Vm.ActivateAsync(); await f.Vm.GenerateCommand.ExecuteAsync(null); Assert.Equal(0, f.Provider.Calls); Assert.Equal(0, f.Store.Saves);
        Assert.StartsWith("No closed trades", f.Vm.Current!.Summary);
        f.Reader.Handler = (q, _) => Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, PeriodFixture.Pair(q), null));
        f.Store.FailSave = true; await f.Vm.GenerateCommand.ExecuteAsync(null); Assert.Null(f.Vm.Report); Assert.Empty(f.Vm.History);
        Assert.Contains("not confirmed saved", f.Vm.Message); Assert.DoesNotContain("private", f.Vm.Message); f.Vm.Deactivate();
    }
    [Fact]
    public async Task LateReadCannotReplaceNewSelectionAndHistoryLoadsIndependently()
    {
        var f = new PeriodFixture(); var pending = new TaskCompletionSource<PeriodComparisonEvidenceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(); var old = f.Vm.Query!;
        f.Reader.Handler = (q, _) => { if (q == old) { started.SetResult(); return pending.Task; } return Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, PeriodFixture.Pair(q), null)); };
        var first = f.Vm.ActivateAsync(); await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        f.Vm.SelectedDate = new(2026, 12, 1); await f.Vm.LoadTask; var current = f.Vm.Current;
        pending.SetResult(new(PeriodEvidenceStatus.Ready, PeriodFixture.Pair(old), null)); await first;
        Assert.Same(current, f.Vm.Current); Assert.Contains("Nov", f.Vm.Heading); // Monday of the containing week.
        f.Reader.Handler = (_, _) => throw new IOException("private"); await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.NotNull(f.Vm.CurrentError); Assert.Null(f.Vm.HistoryError); Assert.DoesNotContain("private", f.Vm.CurrentError); f.Vm.Deactivate();
    }
    [Fact]
    public async Task TenRowPagingHistoricalIdentityAndConfirmedDeleteStayInScope()
    {
        var f = new PeriodFixture(); var q = f.Vm.Query!;
        for (int i = 0; i < 11; i++) f.Store.Items.Add(PeriodFixture.Saved(q));
        var deleted = Guid.NewGuid(); f.Store.Items.Add(PeriodFixture.Saved(new(q.Kind, q.FirstDate, deleted)));
        await f.Vm.ActivateAsync(); Assert.Equal(10, f.Vm.History.Count); await f.Vm.NextHistoryCommand.ExecuteAsync(null);
        Assert.Single(f.Vm.History); var row = f.Vm.History.Single(); await f.Vm.DeleteCommand.ExecuteAsync(row); Assert.Equal(0, f.Store.Deletes);
        f.Dialogs.ConfirmationResult = true; await f.Vm.DeleteCommand.ExecuteAsync(row); Assert.Equal(10, f.Vm.History.Count); Assert.False(f.Vm.NextHistoryCommand.CanExecute(null));
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == deleted); await f.Vm.LoadTask;
        Assert.True(f.Vm.SelectedAccount.IsHistorical); Assert.False(f.Vm.GenerateCommand.CanExecute(null)); Assert.Single(f.Vm.History);
        await f.Vm.OpenCommand.ExecuteAsync(f.Vm.History.Single()); Assert.NotNull(f.Vm.Report); Assert.Equal(deleted, f.Vm.Query!.Scope.AccountId);
        f.Vm.CloseCommand.Execute(null); Assert.Equal(deleted, f.Vm.SelectedAccount.Id); f.Vm.Deactivate();
    }
    [Theory] [InlineData("2026-03-09T03:30:00Z", "2026-03-02", "2026-03-01")] [InlineData("2026-11-01T03:30:00Z", "2026-10-26", "2026-10-01")]
    public void DefaultUsesNewYorkDateAndSeparateWeekMonth(string utc, string week, string month)
    {
        var w = new PeriodFixture(ReviewPeriodKind.Week, DateTimeOffset.Parse(utc)); var m = new PeriodFixture(ReviewPeriodKind.Month, DateTimeOffset.Parse(utc));
        Assert.Equal(DateOnly.Parse(week), w.Vm.Query!.FirstDate); Assert.Equal(DateOnly.Parse(month), m.Vm.Query!.FirstDate);
        Assert.Null(w.Vm.SelectedAccount.Id); Assert.Null(m.Vm.SelectedAccount.Id);
    }
}

internal sealed class PeriodFixture
{
    internal PeriodFixture(ReviewPeriodKind kind = ReviewPeriodKind.Week, DateTimeOffset? now = null)
    {
        var service = new GenerateAndSavePeriodReviewService(new(Reader, Provider, new(), TimeProvider.System), Store, TimeProvider.System);
        Vm = new(kind, Reader, Store, Accounts, Dialogs, new Clock(now ?? ReviewFixture.Now), service.GenerateAsync, Credentials);
    }
    internal PeriodReviewViewModel Vm { get; }
    internal ReaderFake Reader { get; } = new();
    internal StoreFake Store { get; } = new();
    internal ProviderFake Provider { get; } = new();
    internal ReviewFixture.AccountsFake Accounts { get; } = new();
    internal FakeDialogService Dialogs { get; } = new();
    internal CredentialsFake Credentials { get; } = new();
    internal sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    internal sealed class CredentialsFake : ICoachingCredentials
    { public CoachingCredentialSource Source = CoachingCredentialSource.Saved; public CoachingCredentialSource GetSource() => Source; public string? Resolve() => throw new InvalidOperationException("View must not resolve credentials"); }
    internal static PeriodComparisonEvidence Pair(PeriodReviewQuery q, bool empty = false)
    {
        var d = ReviewFixture.Evidence(new(q.FirstDate, q.Scope.AccountId));
        return new(new(PeriodComparisonEvidence.PreviousQuery(q), [], [], [], [], [], [], []),
            new(q, empty ? [] : d.Trades, [], empty ? [] : d.Journals, [], [], [], []), PeriodSourceConsistency.SingleReadTransaction);
    }
    internal static SavedPeriodReview Saved(PeriodReviewQuery q)
    {
        var p = PeriodCoachingPacketBuilder.Build(Pair(q)).Packet!;
        return PeriodReviewSnapshot.Create(new(PeriodCoachingStatus.Success, PeriodCoachingResponseValidator.Validate(ProviderFake.Json(p), p).Response,
            p, new("Fake", "synthetic", "client", Usage: new(10, 0, 5, 15)), ""), ReviewFixture.Now).Review;
    }
    internal sealed class ReaderFake : IPeriodComparisonEvidenceReader
    {
        internal Func<PeriodReviewQuery, CancellationToken, Task<PeriodComparisonEvidenceResult>> Handler = (q, _) => Task.FromResult(new PeriodComparisonEvidenceResult(PeriodEvidenceStatus.Ready, Pair(q), null));
        public Task<PeriodComparisonEvidenceResult> ReadComparisonAsync(PeriodReviewQuery query, CancellationToken cancellationToken = default) => Handler(query, cancellationToken);
    }
    internal sealed class ProviderFake : IPeriodCoachingProvider
    {
        public int Calls; public PeriodCoachingPacket? Packet;
        public TaskCompletionSource<CancellationToken> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<PeriodCoachingPacket, CancellationToken, Task<CoachingProviderReply>>? Handler;
        public Task<CoachingProviderReply> GenerateAsync(PeriodCoachingPacket p, CancellationToken token) { Calls++; Packet = p; Started.TrySetResult(token); return Handler?.Invoke(p, token) ?? Task.FromResult(Success(p)); }
        internal static CoachingProviderReply Success(PeriodCoachingPacket p) => new(CoachingGenerationStatus.Success, Json(p), new("Fake", "synthetic", "client", Usage: new(10, 0, 5, 15)));
        internal static string Json(PeriodCoachingPacket p)
        {
            var section = new PeriodCoachingSection(false, [new(PeriodFindingBasis.Interpretation, "Synthetic interpretation. Verify against the recorded evidence; no adherence is inferred.", p.SuppliedSources.Take(12).Select(s => s.Id).ToArray())]);
            var response = new PeriodCoachingResponse(p.ContractVersion, p.PacketId, section, section, section, section,
                new(false, "Plan a measurable review of recorded activity.", ["current:statistics"], [new("Review one entry", "One recorded review", "Next session", ["current:statistics"])]));
            return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } });
        }
    }
    internal sealed class StoreFake : IPeriodReviewRepository
    {
        public List<SavedPeriodReview> Items { get; } = []; public int Saves, Deletes; public bool FailSave;
        public Task<SavedPeriodReview> SaveAsync(PeriodReviewSnapshot s, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); if (FailSave) throw new IOException("private"); Saves++; Items.Add(s.Review); return Task.FromResult(s.Review); }
        public Func<Guid, CancellationToken, Task<SavedPeriodReview?>>? Detail;
        public Task<SavedPeriodReview?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Detail?.Invoke(id, cancellationToken) ?? Task.FromResult(Items.SingleOrDefault(s => s.Summary.Id == id));
        public Task<PeriodReviewHistoryPage> BrowseAsync(PeriodReviewHistoryQuery q, CancellationToken cancellationToken = default)
        {
            var all = Items.Where(s => PeriodReport.Matches(s.Summary, q.Period)).OrderByDescending(s => s.Summary.GeneratedAtUtc).ThenBy(s => s.Summary.Id).ToArray();
            return Task.FromResult(new PeriodReviewHistoryPage(all.Skip(q.Offset).Take(q.PageSize).Select(s => s.Summary).ToArray(), all.Length, q.Page, q.PageSize));
        }
        public Task<SavedPeriodScopePage> BrowseScopesAsync(PeriodReviewHistoryQuery q, CancellationToken cancellationToken = default)
        {
            var all = Items.Where(s => s.Summary.Kind == q.Period.Kind && s.Summary.FirstDate == q.Period.FirstDate).GroupBy(s => s.Summary.Scope.AccountId)
                .OrderBy(g => g.Key).Select(g => new SavedPeriodScope(g.First().Summary.Scope, g.First().Summary.AccountDisplayName)).ToArray();
            return Task.FromResult(new SavedPeriodScopePage(all.Skip(q.Offset).Take(q.PageSize).ToArray(), all.Length, q.Page, q.PageSize));
        }
        public Task<PeriodReviewDeleteStatus> DeleteAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default)
        { if (!confirmed) return Task.FromResult(PeriodReviewDeleteStatus.NotConfirmed); Deletes++; return Task.FromResult(Items.RemoveAll(s => s.Summary.Id == id) == 1 ? PeriodReviewDeleteStatus.Deleted : PeriodReviewDeleteStatus.NotFound); }
    }
}
