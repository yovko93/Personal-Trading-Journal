using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class DailyReviewWorkspaceTests
{
    [Fact]
    public void JournalOnlySummaryDoesNotInventTradeResultsAndKeepsIdentityInTechnicalDetails()
    {
        var journal = ReviewFixture.Journal(null, "All accounts", ReviewFixture.Day);
        var evidence = new DailyReviewEvidence(new(ReviewFixture.Day), [], [journal]);
        var display = new ReviewEvidencePresentation(evidence, DailyReviewStatisticsCalculator.Calculate(evidence));
        Assert.Equal("No closed trades · 1 Journal entries (user-written observations)", display.Counts);
        Assert.Empty(display.CoverageSummary);
        Assert.Contains("do not imply a trading result", display.EmptyText);
        var row = Assert.Single(display.Journals);
        Assert.Contains("All accounts", row.Heading);
        Assert.DoesNotContain("Revision", row.Heading);
        Assert.Contains("revision 3", row.Identity);
        Assert.DoesNotContain("null", row.Heading);
        Assert.DoesNotContain(journal.JournalId.ToString(), row.Heading);
        Assert.Contains(journal.JournalId.ToString(), row.Identity);
        Assert.Empty(display.Metrics);
    }

    [Fact]
    public async Task HistoricalPagesAreBoundedPreserveSelectedScopeAndRemainAvailableWhenCurrentEvidenceFails()
    {
        var f = new ReviewFixture();
        var ids = Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToArray();
        var queries = new List<HistoricalCoachingAccountQuery>();
        f.History.ScopeHandler = (q, _) =>
        {
            lock (queries) queries.Add(q);
            return Task.FromResult(new HistoricalCoachingAccountPage(
                ids.Skip(q.Offset).Take(q.PageSize).Select(id => new HistoricalCoachingAccount(id, "P 21")).ToArray(),
                ids.Length, q.Page, q.PageSize));
        };
        f.Reader.Handler = (_, _) => throw new IOException("Current source failure");
        var saved = ReviewFixture.Saved(new(new(ReviewFixture.Day, ids[0]), [], []));
        f.History.Items.Add(saved);
        await f.Vm.ActivateAsync();
        Assert.Contains("Current evidence could not", f.Vm.CurrentError);
        Assert.Null(f.Vm.ScopeError);
        Assert.True(f.Vm.ShowHistoricalAccountPaging);
        Assert.Equal(25, f.Vm.Accounts.Count(a => a.IsHistorical));
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ids[0]);
        await f.Vm.LoadTask;
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        var snapshot = f.Vm.Snapshot;
        await f.Vm.NextHistoricalAccountsCommand.ExecuteAsync(null);
        Assert.Same(snapshot, f.Vm.Snapshot); // Discovery paging does not replace the open history/current selection.
        Assert.Equal(ids[0], f.Vm.SelectedAccount.Id);
        Assert.Contains(f.Vm.Accounts, a => a.Id == ids[25]);
        Assert.Equal(2, f.Vm.Accounts.Count(a => a.IsHistorical)); // Page + retained selected ID.
        Assert.False(f.Vm.NextHistoricalAccountsCommand.CanExecute(null));
        Assert.True(f.Vm.PreviousHistoricalAccountsCommand.CanExecute(null));
        Assert.All(queries, q => Assert.Equal(25, q.PageSize));
        f.Vm.Deactivate();
    }

    [Fact]
    public async Task LateDiscoveryCannotReplaceNewDateAndErrorsAreRecoverableWithoutChangingScope()
    {
        var f = new ReviewFixture();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<HistoricalCoachingAccountPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.History.ScopeHandler = (q, token) =>
        {
            if (q.ReviewDate == ReviewFixture.Day) { started.TrySetResult(token); return pending.Task; }
            return Task.FromResult(new HistoricalCoachingAccountPage([new(ReviewFixture.OtherId, "Correct date")], 1, 1, 25));
        };
        var first = f.Vm.ActivateAsync();
        var cancelled = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        f.Vm.SelectedDate = ReviewFixture.Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await f.Vm.LoadTask;
        Assert.True(cancelled.IsCancellationRequested);
        pending.SetResult(new([new(Guid.NewGuid(), "Stale date")], 1, 1, 25));
        await first;
        Assert.DoesNotContain(f.Vm.Accounts, a => a.Label.Contains("Stale date"));
        var selected = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.OtherId);
        f.Vm.SelectedAccount = selected;
        await f.Vm.LoadTask;
        f.History.ScopeHandler = (_, _) => throw new IOException("private diagnostics");
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("Refresh to retry", f.Vm.ScopeError);
        Assert.DoesNotContain("private", f.Vm.ScopeError);
        Assert.Equal(selected.Id, f.Vm.SelectedAccount.Id);
        f.History.ScopeHandler = (q, _) => Task.FromResult(new HistoricalCoachingAccountPage([], 0, q.Page, q.PageSize));
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(f.Vm.ScopeError);
        Assert.Equal(selected.Id, f.Vm.SelectedAccount.Id);
        Assert.Contains("unavailable", f.Vm.AccountNotice);
        f.Vm.Deactivate();
    }

    [Theory]
    [InlineData("2026-03-08T04:59:00Z", "2026-03-07")]
    [InlineData("2026-03-08T07:00:00Z", "2026-03-08")]
    [InlineData("2026-11-01T06:30:00Z", "2026-11-01")]
    public async Task DefaultsUseNewYorkDateAndDoNotGenerate(string instant, string day)
    {
        var f = new ReviewFixture(DateTimeOffset.Parse(instant));
        Assert.Null(f.Vm.SelectedAccount.Id);
        Assert.Equal(DateOnly.Parse(day), DateOnly.FromDateTime(f.Vm.SelectedDate!.Value));
        Assert.Equal(0, f.History.Writes);
        await f.Vm.ActivateAsync();
        Assert.Contains("No Trade or Journal", f.Vm.Current!.EmptyText);
        Assert.Contains("No saved AI", f.Vm.HistoryEmptyText);
        Assert.Equal(0, f.History.Writes);
        Assert.False(f.Vm.GenerateCommand.CanExecute(null)); // No configured orchestration in this read-only fixture.
    }

    [Fact]
    public async Task StrictNetCoverageCurrenciesAccountIdentityAndInactiveSelectionsArePreserved()
    {
        var f = new ReviewFixture();
        f.Reader.Handler = (q, _) => Task.FromResult(ReviewFixture.Evidence(q));
        await f.Vm.ActivateAsync();
        var usd = f.Vm.Current!.Metrics.Single(m => m.Currency == "USD");
        Assert.Null(usd.Net.Metrics.Total);
        Assert.Contains("Net: Unavailable (USD)", usd.Values);
        Assert.Contains("commissions missing for 1 Trade", usd.Coverage);
        Assert.Contains("No partial total", usd.Coverage);
        Assert.Equal(2, f.Vm.Current.Metrics.Count);
        Assert.Equal(2, f.Vm.Current.Metrics.Select(m => m.Currency).Distinct().Count());
        Assert.Equal(3, f.Vm.Current.Journals.Count);
        Assert.Contains(f.Vm.Accounts, a => a.Label.Contains("inactive"));
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.Id == ReviewFixture.AccountId);
        await f.Vm.LoadTask;
        Assert.All(f.Vm.Current!.Trades, t => Assert.Equal(ReviewFixture.AccountId, t.Source.Account.Id));
        Assert.All(f.Vm.Current.Journals, j => Assert.Equal(ReviewFixture.AccountId, j.Source.TradingAccountId));
        Assert.Contains("Exact-account", f.Vm.HistoryScope);
        f.Accounts.Items = [];
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(ReviewFixture.AccountId, f.Vm.SelectedAccount.Id);
        Assert.Contains("unavailable", f.Vm.AccountNotice);
    }

    [Fact]
    public async Task PagingIsTenBoundedScopeStableAndDeletionReconcilesLastPage()
    {
        var f = new ReviewFixture();
        f.History.Items.AddRange(Enumerable.Range(0, 11).Select(_ => ReviewFixture.Saved()));
        await f.Vm.ActivateAsync();
        Assert.Equal(10, f.Vm.Analyses.Count);
        Assert.True(f.Vm.NextCommand.CanExecute(null));
        await f.Vm.NextCommand.ExecuteAsync(null);
        var row = Assert.Single(f.Vm.Analyses);
        var date = f.Vm.SelectedDate;
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(row);
        Assert.NotNull(f.Vm.Snapshot);
        Assert.Equal(date, f.Vm.SelectedDate);
        Assert.Contains("page 2", f.Vm.HistoryLabel);
        f.Vm.CloseAnalysisCommand.Execute(null);
        Assert.Null(f.Vm.Snapshot);
        Assert.Contains("page 2", f.Vm.HistoryLabel);
        await f.Vm.DeleteAnalysisCommand.ExecuteAsync(row);
        Assert.Equal(0, f.History.Deletes);
        f.Dialogs.ConfirmationResult = true;
        await f.Vm.DeleteAnalysisCommand.ExecuteAsync(row);
        Assert.Equal(1, f.History.Deletes);
        Assert.Equal(10, f.Vm.Analyses.Count);
        Assert.Contains("page 1", f.Vm.HistoryLabel);
        Assert.False(f.Vm.NextCommand.CanExecute(null));
        Assert.Contains(row.Source.Id.ToString(), f.Dialogs.ConfirmationRequest!.Message);
        Assert.Equal(0, f.History.Writes);
        Assert.All(f.History.Queries, q => Assert.Equal(10, q.PageSize));
    }

    [Fact]
    public async Task SnapshotUsesStoredSourcesAndUsageNeverCurrentEvidence()
    {
        var f = new ReviewFixture();
        var original = ReviewFixture.Saved(ReviewFixture.Evidence(new(ReviewFixture.Day)));
        f.History.Items.Add(original);
        f.Reader.Handler = (q, _) => Task.FromResult(new DailyReviewEvidence(q, [], []));
        await f.Vm.ActivateAsync();
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        Assert.Empty(f.Vm.Current!.Trades);
        Assert.Equal(2, f.Vm.Snapshot!.Evidence.Trades.Count);
        Assert.Equal(3, f.Vm.Snapshot.Evidence.Journals.Count);
        Assert.All(f.Vm.Snapshot.Evidence.Journals, j => Assert.False(j.CanNavigate));
        Assert.Contains("input 10", f.Vm.Snapshot.Metadata);
        Assert.Equal("Saved day summary from supplied evidence.", f.Vm.Snapshot.Statements[0].Text);
        Assert.Contains("calculated:day", f.Vm.Snapshot.Statements[0].SourceDetails);
        Assert.DoesNotContain("calculated:day", f.Vm.Snapshot.Statements[0].Text);
        Assert.Contains("missingOrUncertainData", f.Vm.Snapshot.CompleteEvidence);
        Assert.Contains("Unavailable (USD)", f.Vm.Snapshot.Evidence.Metrics.First(m => m.Currency == "USD").Values);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("identity")]
    [InlineData("json")]
    [InlineData("citation")]
    public async Task UnsupportedOrDamagedSnapshotsFailWithoutSubstitutingCurrentData(string defect)
    {
        var f = new ReviewFixture();
        var saved = ReviewFixture.Saved();
        saved = defect switch
        {
            "version" => saved with { EvidenceContractVersion = "future" },
            "identity" => saved with { PacketId = "other" },
            "citation" => saved with { ResponseJson = saved.ResponseJson.Replace("calculated:day", "unknown:source") },
            _ => saved with { EvidenceJson = "broken" },
        };
        f.History.Items.Add(saved);
        await f.Vm.ActivateAsync();
        await f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        Assert.Null(f.Vm.Snapshot);
        Assert.Contains("not substituted", f.Vm.DetailMessage);
    }

    [Fact]
    public async Task IndependentReadErrorsAndDeleteFailureRemainActionable()
    {
        var f = new ReviewFixture();
        f.History.Items.Add(ReviewFixture.Saved());
        f.Reader.Handler = (_, _) => throw new IOException("Private content must not leak");
        await f.Vm.ActivateAsync();
        Assert.NotNull(f.Vm.CurrentError);
        Assert.DoesNotContain("Private", f.Vm.CurrentError);
        Assert.Single(f.Vm.Analyses);
        f.Dialogs.ConfirmationResult = true;
        f.History.FailDelete = true;
        await f.Vm.DeleteAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        Assert.Single(f.Vm.Analyses);
        Assert.Contains("Deletion failed", f.Vm.DetailMessage);
        f.Reader.Handler = (q, _) => Task.FromResult(new DailyReviewEvidence(q, [], []));
        f.History.FailBrowse = true;
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.NotNull(f.Vm.Current);
        Assert.NotNull(f.Vm.HistoryError);
        f.History.FailBrowse = false;
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(f.Vm.HistoryError);
    }

    [Fact]
    public async Task LateEvidenceAndHistoryCannotReplaceNewDateOrAccount()
    {
        var f = new ReviewFixture();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<DailyReviewEvidence>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Reader.Handler = (q, ct) => { started.TrySetResult(ct); return pending.Task; };
        Task old = f.Vm.ActivateAsync();
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Reader.Handler = (q, _) => Task.FromResult(new DailyReviewEvidence(q, [], []));
        f.Vm.SelectedDate = ReviewFixture.Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await f.Vm.LoadTask;
        Assert.True(token.IsCancellationRequested);
        pending.SetResult(ReviewFixture.Evidence(new(ReviewFixture.Day)));
        await old;
        Assert.Empty(f.Vm.Current!.Trades);
        Assert.Contains("09 Oct 2026", f.Vm.Current.Summary);
        Assert.False(f.Vm.IsLoading);
    }

    [Fact]
    public async Task LateDetailDoesNotReopenAfterClosePageChangeOrDeactivation()
    {
        var f = new ReviewFixture();
        var saved = ReviewFixture.Saved(); f.History.Items.Add(saved);
        await f.Vm.ActivateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<SavedCoachingAnalysis?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.History.GetHandler = (_, _) => { started.SetResult(); return pending.Task; };
        Task open = f.Vm.OpenAnalysisCommand.ExecuteAsync(f.Vm.Analyses.Single());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Vm.CloseAnalysisCommand.Execute(null);
        pending.SetResult(saved); await open;
        Assert.Null(f.Vm.Snapshot);
        f.Vm.Deactivate();
        Assert.False(f.Vm.IsLoading);
        Assert.Null(f.Vm.DetailMessage);
    }

    [Fact]
    public async Task LateHistoryCannotPublishOldScopeRowsOrCounts()
    {
        var f = new ReviewFixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<CoachingAnalysisHistoryPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.History.BrowseHandler = (_, _) => { started.TrySetResult(); return pending.Task; };
        Task old = f.Vm.ActivateAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.History.BrowseHandler = null;
        f.Vm.SelectedAccount = new(ReviewFixture.AccountId, "P 21");
        await f.Vm.LoadTask;
        pending.SetResult(new([ReviewFixture.Saved().Summary], 11, 1, 10));
        await old;
        Assert.Empty(f.Vm.Analyses);
        Assert.Contains("0 analyses", f.Vm.HistoryLabel);
        Assert.False(f.Vm.NextCommand.CanExecute(null));
        Assert.Equal(ReviewFixture.AccountId, f.Vm.SelectedAccount.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-25)]
    public void CompleteZeroPositiveNegativeAndExcludedOpenActivityKeepTheirDefinitions(int amount)
    {
        var evidence = ReviewFixture.Evidence(new(ReviewFixture.Day));
        var source = evidence.Trades.Single(t => t.PricingCurrency == "EUR");
        var closed = source with { Facts = source.Facts! with { GrossPnL = amount, NetPnL = amount } };
        var selected = evidence with { Trades = [closed] };
        var model = new ReviewEvidencePresentation(selected, DailyReviewStatisticsCalculator.Calculate(selected));
        Assert.Equal(amount, model.Metrics[0].Net.Metrics.Total);
        Assert.Contains(amount == 0 ? "1 break-even" : amount > 0 ? "1 wins" : "1 losses", model.Metrics[0].Outcomes);
        var open = closed with { Inclusion = DailyReviewTradeInclusion.OpenActivityOnDate,
            Facts = closed.Facts! with { Status = TradeStatus.Open, ClosedAtUtc = null, GrossPnL = null, NetPnL = null } };
        selected = selected with { Trades = [open] };
        model = new(selected, DailyReviewStatisticsCalculator.Calculate(selected));
        Assert.Contains("No closed trades", model.Summary);
        Assert.Contains("1 open/partial Trades excluded", model.CoverageSummary);
        Assert.Null(model.Metrics[0].Net.Metrics.Total);
        Assert.Contains("Unavailable", model.Metrics[0].Values);
    }

    [Fact]
    public async Task CurrentSourceCommandsRetainExactIdentityIncludingNullJournalScope()
    {
        var f = new ReviewFixture(); f.Reader.Handler = (q, _) => Task.FromResult(ReviewFixture.Evidence(q));
        await f.Vm.ActivateAsync();
        Guid? tradeId = null;
        DailyReviewJournalEvidence? journal = null;
        f.Vm.OpenTradeAsync = id => { tradeId = id; return Task.CompletedTask; };
        f.Vm.OpenJournalAsync = row => { journal = row; return Task.CompletedTask; };
        foreach (var row in f.Vm.Current!.Trades)
        {
            await f.Vm.OpenTradeCommand.ExecuteAsync(row); Assert.Equal(row.Id, tradeId);
        }
        foreach (var row in f.Vm.Current.Journals)
        {
            await f.Vm.OpenJournalCommand.ExecuteAsync(row); Assert.Same(row.Source, journal);
        }
        Assert.Null(f.Vm.SelectedAccount.Id);
    }

    [Fact]
    public async Task CancelAndCommitRefreshDoNotPublishCancelledData()
    {
        var f = new ReviewFixture();
        await f.Vm.ActivateAsync();
        f.Vm.OnDataCommitted(); await f.Vm.LoadTask;
        Assert.Equal(2, f.Reader.Calls);
        f.Vm.Deactivate(); f.Vm.OnDataCommitted();
        Assert.Equal(2, f.Reader.Calls);
        f.Vm.SelectedDate = null;
        await f.Vm.ActivateAsync();
        Assert.Null(f.Vm.Current);
        Assert.Contains("valid New York date", f.Vm.CurrentError);
    }
}

internal sealed class ReviewFixture
{
    internal static readonly DateOnly Day = new(2026, 10, 8);
    internal static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid OtherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly DateTimeOffset Now = new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);
    internal ReviewFixture(DateTimeOffset? now = null, ICoachingProvider? provider = null, TimeProvider? generationClock = null) =>
        Vm = new(Reader, History, Accounts, Dialogs, new Clock(now ?? Now), provider is null ? null :
            new GenerateAndSaveCoachingService(new(provider, new(), generationClock ?? TimeProvider.System), History, new Clock(now ?? Now)));
    internal EvidenceReaderFake Reader { get; } = new();
    internal HistoryFake History { get; } = new();
    internal AccountsFake Accounts { get; } = new();
    internal FakeDialogService Dialogs { get; } = new();
    internal DailyReviewViewModel Vm { get; }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    internal static DailyReviewEvidence Evidence(DailyReviewQuery query)
    {
        var trades = new[] { Trade(query, AccountId, "USD", 50m, false), Trade(query, OtherId, "EUR", -10m, true) };
        var journals = new[] { Journal(null, "All accounts", query.Date), Journal(AccountId, "P 21", query.Date), Journal(OtherId, "Other", query.Date) };
        return new(query, trades.Where(t => query.TradingAccountId is null || t.Account.Id == query.TradingAccountId).ToArray(),
            journals.Where(j => query.TradingAccountId is null || j.TradingAccountId == query.TradingAccountId).ToArray());
    }
    private static DailyReviewTradeEvidence Trade(DailyReviewQuery q, Guid account, string currency, decimal gross, bool costs)
    {
        var close = q.FromUtc.AddHours(10);
        return new(Guid.NewGuid(), new(account, account == AccountId ? "P 21" : "Other", true), new(Guid.NewGuid(), "MNQ", true), null,
            currency, 2m, close.AddHours(-1), close, DailyReviewTradeInclusion.ClosedOnDate,
            new(1, TradeStatus.Closed, TradeDirection.Long, close.AddHours(-1), close, 0, 100m, 125m, costs ? 2m : null, gross, costs ? gross - 2m : null),
            [new(Guid.NewGuid(), 1, close.AddHours(-1), ExecutionSide.Buy, 1m, 100m, costs ? 1m : null, 0m, costs ? 1m : null, null, null, null),
             new(Guid.NewGuid(), 2, close, ExecutionSide.Sell, 1m, 125m, costs ? 1m : null, 0m, costs ? 1m : null, null, null, null)], [],
            costs ? DailyReviewTradeQuality.None : DailyReviewTradeQuality.UnknownCommission | DailyReviewTradeQuality.UnknownNetPnL);
    }
    internal static DailyReviewJournalEvidence Journal(Guid? account, string name, DateOnly date) => new(Guid.NewGuid(), date, account, name,
        account is null ? DailyJournalAccountState.AllAccounts : DailyJournalAccountState.Active,
        "Original journal text — remain patient.", new("Followed the plan", "", "Wait for confirmation"), account == AccountId, 3, Now, Now);
    internal static SavedCoachingAnalysis Saved(DailyReviewEvidence? evidence = null)
    {
        var result = CoachingEvidencePacketBuilder.Build(evidence ?? new(new(Day), [], []));
        Assert.Equal(CoachingPacketBuildStatus.Ready, result.Status);
        var packet = result.Packet!;
        return CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Saved day summary from supplied evidence.", ["calculated:day"]), [], [], [], []),
            new("Fake", "test-model", "client-test", "req_test", "resp_test", new(10, 0, 5, 15)), "Not displayed"), Now).Analysis;
    }
    internal sealed class EvidenceReaderFake : IDailyReviewEvidenceReader
    {
        internal Func<DailyReviewQuery, CancellationToken, Task<DailyReviewEvidence>> Handler { get; set; } = (q, _) => Task.FromResult(new DailyReviewEvidence(q, [], []));
        internal int Calls;
        public Task<DailyReviewEvidence> GetAsync(DailyReviewQuery query, CancellationToken token = default) { Interlocked.Increment(ref Calls); return Handler(query, token); }
    }
    internal sealed class AccountsFake : ITradingAccountReader
    {
        internal IReadOnlyList<AccountListItem> Items { get; set; } = [new(AccountId, "P 21", TradingAccountType.Personal, null, null, "USD", null, false)];
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken token = default) => Task.FromResult(Items);
        public Task<TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
    }
    internal sealed class HistoryFake : ICoachingAnalysisRepository
    {
        internal Func<HistoricalCoachingAccountQuery, CancellationToken, Task<HistoricalCoachingAccountPage>>? ScopeHandler;
        public Task<HistoricalCoachingAccountPage> BrowseHistoricalAccountsAsync(HistoricalCoachingAccountQuery query, CancellationToken token = default) =>
            ScopeHandler?.Invoke(query, token) ?? Task.FromResult(new HistoricalCoachingAccountPage([], 0, query.Page, query.PageSize));
        internal List<SavedCoachingAnalysis> Items { get; } = [];
        internal List<CoachingAnalysisHistoryQuery> Queries { get; } = [];
        internal int Writes, Deletes;
        internal bool FailBrowse, FailDelete;
        internal bool FailSave { get; set; }
        internal Func<Guid, CancellationToken, Task<SavedCoachingAnalysis?>>? GetHandler;
        internal Func<CoachingAnalysisHistoryQuery, CancellationToken, Task<CoachingAnalysisHistoryPage>>? BrowseHandler;
        public Task<SavedCoachingAnalysis> SaveAsync(CoachingAnalysisSnapshot snapshot, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (FailSave) throw new IOException("private database payload");
            Writes++; Items.Add(snapshot.Analysis); return Task.FromResult(snapshot.Analysis);
        }
        public Task<SavedCoachingAnalysis?> GetAsync(Guid id, CancellationToken token = default) => GetHandler?.Invoke(id, token) ?? Task.FromResult(Items.SingleOrDefault(a => a.Summary.Id == id));
        public Task<CoachingAnalysisHistoryPage> BrowseAsync(CoachingAnalysisHistoryQuery q, CancellationToken token = default)
        {
            if (BrowseHandler is not null) return BrowseHandler(q, token);
            if (FailBrowse) throw new IOException("private");
            lock (Queries) Queries.Add(q);
            var all = Items.Where(a => a.Summary.ReviewDate == q.ReviewDate && a.Summary.Scope == q.Scope)
                .OrderByDescending(a => a.Summary.GeneratedAtUtc).ThenBy(a => a.Summary.Id).ToArray();
            return Task.FromResult(new CoachingAnalysisHistoryPage(all.Skip(q.Offset).Take(q.PageSize).Select(a => a.Summary).ToArray(), all.Length, q.Page, q.PageSize));
        }
        public Task<bool> DeleteAsync(Guid id, CancellationToken token = default)
        {
            if (FailDelete) throw new IOException("private");
            Deletes++; return Task.FromResult(Items.RemoveAll(a => a.Summary.Id == id) > 0);
        }
    }
}
