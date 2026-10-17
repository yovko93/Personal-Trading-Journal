using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Application.Tests.TradingPlans;

public sealed class TradingPlanContractTests
{
    private static readonly Guid Account = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Plan = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly TradingPlanKey Key = new(Account, new DateOnly(2026, 10, 17));

    [Theory]
    [InlineData("2026-01-01T04:59:59Z", "2025-12-31")]
    [InlineData("2026-01-01T05:00:00Z", "2026-01-01")]
    [InlineData("2026-03-08T06:59:59Z", "2026-03-08")]
    [InlineData("2026-03-08T07:00:00Z", "2026-03-08")]
    [InlineData("2026-07-01T03:59:59Z", "2026-06-30")]
    [InlineData("2026-07-01T04:00:00Z", "2026-07-01")]
    [InlineData("2026-11-01T05:30:00Z", "2026-11-01")]
    [InlineData("2026-11-01T06:30:00Z", "2026-11-01")]
    public void TodayUsesCanonicalNewYorkClockAcrossMidnightYearAndDst(string utc, string expectedDate)
    {
        var clock = new FixedClock(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
        var key = TradingPlanDatePolicy.Today(Account, clock);
        Assert.Equal(DateOnly.ParseExact(expectedDate, "yyyy-MM-dd"), key.NewYorkDate);
        Assert.Equal(Account, key.AccountId);
    }

    [Fact]
    public void SavingUsesCapturedSelectedDateRatherThanTodayOrSession()
    {
        var key = new TradingPlanKey(Account, new DateOnly(2026, 3, 7));
        var morning = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, key,
            new(TradingPlanSession.London), new(Plan, 3));
        var evening = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, key,
            new(TradingPlanSession.NewYork), new(Plan, 3));
        Assert.Equal(morning.Key, evening.Key);
        Assert.Equal(new DateOnly(2026, 3, 7), evening.Key.NewYorkDate);
        Assert.Equal(morning.Expected, evening.Expected);
    }

    [Fact]
    public void DraftSaveAcceptsIncompleteContentButReadyExplainsMissingFields()
    {
        var content = new TradingPlanContent();
        var draft = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, Key, content, new(Plan, 1));
        var ready = new TradingPlanWriteCommand(TradingPlanWriteOperation.MarkReady, Key, content, new(Plan, 1));
        Assert.Empty(draft.ValidateContent());
        Assert.Contains(ready.ValidateContent(), i => i.Field == "Focus");
        Assert.Same(content, ready.Content);
        Assert.Same(content, draft.Content);
    }

    [Theory]
    [InlineData(TradingPlanWriteOperation.SaveDraft)]
    [InlineData(TradingPlanWriteOperation.MarkReady)]
    public void ExistingWritesRequireLoadedIdentityAndVersion(TradingPlanWriteOperation operation)
    {
        Assert.Throws<ArgumentException>(() => new TradingPlanWriteCommand(operation, Key, new()));
        var command = new TradingPlanWriteCommand(operation, Key, new(), new(Plan, 7));
        Assert.Equal(Plan, command.Expected!.PlanId);
        Assert.Equal(7, command.Expected.Version);
        Assert.Throws<ArgumentException>(() => new TradingPlanWriteCommand(operation, Key, new(), new(Plan, 7), new(Plan, 1)));
    }

    [Fact]
    public void CreationCannotOverwriteLoadedPlanAndRevisionSourcesAreExplicit()
    {
        var create = new TradingPlanWriteCommand(TradingPlanWriteOperation.CreateDraft, Key, new());
        Assert.Null(create.Expected);
        Assert.Throws<ArgumentException>(() => new TradingPlanWriteCommand(TradingPlanWriteOperation.CreateDraft,
            Key, new(), new(Plan, 1)));
        Assert.Throws<ArgumentException>(() => new TradingPlanWriteCommand(TradingPlanWriteOperation.CopyForward, Key, new()));
        var copy = new TradingPlanWriteCommand(TradingPlanWriteOperation.CopyForward, Key, new(), sourceRevision: new(Plan, 2));
        Assert.Equal(2, copy.SourceRevision!.Revision);
        Assert.Null(copy.Expected);
        Assert.Throws<ArgumentException>(() => new TradingPlanWriteCommand(TradingPlanWriteOperation.StartDraft,
            Key, new(), new(Plan, 3), new(Guid.NewGuid(), 2)));
        var edit = new TradingPlanWriteCommand(TradingPlanWriteOperation.StartDraft, Key, new(), new(Plan, 3), new(Plan, 2));
        Assert.Equal(edit.Expected!.PlanId, edit.SourceRevision!.PlanId);
    }

    [Fact]
    public void InvalidVersionsAndSourceTokensAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new TradingPlanExpectedState(Guid.Empty, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanExpectedState(Plan, 0));
        Assert.Throws<ArgumentException>(() => new TradingPlanRuleLink(Guid.Empty, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanRuleLink(Plan, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanSetupLink(Plan, 0, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => new TradingPlanSetupLink(Plan, 1, DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(2))));
    }

    [Fact]
    public void TransitionGuardRejectsStaleVersionWrongScopeAndDuplicateCreation()
    {
        var state = new TradingPlanLoadedState(Key, new(Plan, 7), TradingPlanStatus.Draft, 2);
        var save = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, Key, new(), new(Plan, 7));
        Assert.Null(save.CheckTransition(state));
        Assert.Equal(TradingPlanWriteStatus.Conflict, save.CheckTransition(new(Key, new(Plan, 8), TradingPlanStatus.Draft, 2)));
        Assert.Equal(TradingPlanWriteStatus.Conflict, save.CheckTransition(new(
            new(Account, Key.NewYorkDate.AddDays(1)), new(Plan, 7), TradingPlanStatus.Draft, 2)));
        Assert.Equal(TradingPlanWriteStatus.Conflict, save.CheckTransition(null));
        var create = new TradingPlanWriteCommand(TradingPlanWriteOperation.CreateDraft, Key, new());
        Assert.Equal(TradingPlanWriteStatus.TargetAlreadyExists, create.CheckTransition(state));
        Assert.Null(create.CheckTransition(null));
    }

    [Fact]
    public void ReadyRequiresExplicitDraftTransitionAndCannotOverwriteAnExistingWorkingDraft()
    {
        var ready = new TradingPlanLoadedState(Key, new(Plan, 7), TradingPlanStatus.Ready, 3);
        var save = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, Key, new(), new(Plan, 7));
        var mark = new TradingPlanWriteCommand(TradingPlanWriteOperation.MarkReady, Key, new(), new(Plan, 7));
        Assert.Equal(TradingPlanWriteStatus.Conflict, save.CheckTransition(ready));
        Assert.Equal(TradingPlanWriteStatus.Conflict, mark.CheckTransition(ready));
        var start = new TradingPlanWriteCommand(TradingPlanWriteOperation.StartDraft, Key, new(), new(Plan, 7), new(Plan, 1));
        Assert.Null(start.CheckTransition(ready)); // Older Ready versions can seed a new Draft explicitly.
        Assert.Equal(TradingPlanWriteStatus.Conflict, start.CheckTransition(new(Key, new(Plan, 7), TradingPlanStatus.Draft, 3)));
        var future = new TradingPlanWriteCommand(TradingPlanWriteOperation.StartDraft, Key, new(), new(Plan, 7), new(Plan, 4));
        Assert.Equal(TradingPlanWriteStatus.Conflict, future.CheckTransition(ready));
        Assert.Equal(3, ready.LatestReadyRevision);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ReadyRecordedAtOrAfterEntryDoesNotProvePriorPreparation(int secondsAfterReady, bool expected)
    {
        var ready = new DateTimeOffset(2026, 10, 17, 13, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, TradingPlanDatePolicy.WasRecordedBeforeEntry(ready, ready.AddSeconds(secondsAfterReady)));
        Assert.False(TradingPlanDatePolicy.WasRecordedBeforeEntry(ready, null));
    }

    [Fact]
    public void NonUtcAuditTimesAreRejectedRatherThanSilentlyConverted()
    {
        var local = new DateTimeOffset(2026, 10, 17, 13, 0, 0, TimeSpan.FromHours(3));
        Assert.Throws<ArgumentException>(() => TradingPlanDatePolicy.WasRecordedBeforeEntry(local, null));
        Assert.Throws<ArgumentException>(() => TradingPlanDatePolicy.WasRecordedBeforeEntry(local.ToUniversalTime(), local));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void HistoryReadsMustBeBounded(int limit) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new TradingPlanHistoryQuery(Account, Key.NewYorkDate, Key.NewYorkDate, limit: limit));

    [Fact]
    public void HistoricalScopeNeverFallsBackToAllAccountsOrAnotherSameNameAccount()
    {
        Assert.Throws<ArgumentException>(() => new TradingPlanHistoryQuery(Guid.Empty, Key.NewYorkDate, Key.NewYorkDate));
        Assert.Throws<ArgumentException>(() => new TradingPlanHistoryQuery(Account, Key.NewYorkDate, Key.NewYorkDate.AddDays(-1)));
        var history = new TradingPlanHistoryQuery(Account, Key.NewYorkDate, Key.NewYorkDate, TradingPlanStatus.Ready);
        Assert.Equal(Account, history.AccountId);
        Assert.Equal(TradingPlanStatus.Ready, history.Status);
        Assert.Equal(20, history.Limit);
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
