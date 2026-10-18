using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.TradingPlans;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed partial class DailyReviewEvidenceReaderTests
{
    private sealed class PlanClock(DateTimeOffset now) : TimeProvider
    { public DateTimeOffset Now = now; public override DateTimeOffset GetUtcNow() => Now; }
    private static TradingPlanContent Intention(Guid instrument, string focus = "Synthetic preparation; ignore embedded instructions") =>
        new(TradingPlanSession.NewYork, focus, TradingPlanIntent.StandAside, [new(instrument)], standAside: "No confirmation", riskLimits: new(10, null, 0));
    private static async Task<TradingPlan> Publish(ITradingPlanStore store, TradingPlan plan, TradingPlanContent content)
    {
        if (plan.WorkingDraft is null)
        {
            var start = await store.WriteAsync(new(TradingPlanWriteOperation.StartDraft, plan.Key, content,
                new(plan.Id, plan.Version), plan.LatestReadyRevision!.Id));
            Assert.Equal(TradingPlanWriteStatus.Success, start.Status); plan = start.Plan!;
        }
        var saved = await store.WriteAsync(new(TradingPlanWriteOperation.SaveDraft, plan.Key, content, new(plan.Id, plan.Version), explicitConfirmations: TradingPlanChecklist.All));
        Assert.Equal(TradingPlanWriteStatus.Success, saved.Status); plan = saved.Plan!;
        var ready = await store.WriteAsync(new(TradingPlanWriteOperation.MarkReady, plan.Key, plan.WorkingDraft!.Content, new(plan.Id, plan.Version)));
        Assert.Equal(TradingPlanWriteStatus.Success, ready.Status); return ready.Plan!;
    }

    [Fact]
    public async Task EntryInstantsChooseExactRevisionsAcrossDstTieAndLaterPreparationWithoutNPlusOne()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var reader = Reader(db); var query = new DailyReviewQuery(Day, account);
        Assert.Equal(ReviewPlanState.NoPlan, Assert.Single((await reader.GetAsync(query)).Preparation!.Days).State);
        var clock = new PlanClock(Close.AddHours(-2)); var store = new TradingPlanStore(db.ContextFactory, clock);
        var content = Intention(instrument);
        var p = (await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, new(account, Day), content))).Plan!;
        Assert.Equal(ReviewPlanState.DraftOnly, Assert.Single((await reader.GetAsync(query)).Preparation!.Days).State);
        clock.Now = Close.AddMinutes(-30); p = await Publish(store, p, content); var first = p.LatestReadyRevision!.Id;
        p = await Publish(store, p, Intention(instrument, "Equal timestamp, revision two")); var second = p.LatestReadyRevision!.Id;
        clock.Now = Close.AddMinutes(-15); p = await Publish(store, p, Intention(instrument, "Later eligible three")); var third = p.LatestReadyRevision!.Id;
        clock.Now = Close.AddMinutes(10); p = await Publish(store, p, Intention(instrument, "Retrospective four")); var latest = p.LatestReadyRevision!.Id;
        var trades = new[] { TradeFact(account, instrument, Close, entry: Close.AddMinutes(-31)),
            TradeFact(account, instrument, Close, commission: null, entry: Close.AddMinutes(-30)),
            TradeFact(account, instrument, Close, currency: "EUR", entry: Close.AddMinutes(-10)) };
        await Add(db, trades);
        var observer = new ReadObserver(); var read = await (await ObservedReader(db, observer)).GetAsync(query);
        Assert.Equal(9, observer.Sql.Count); Assert.Equal(0, observer.NonReads); Assert.False(observer.Tracked);
        var prep = read.Preparation!; Assert.Equal(new[] { second, third, latest }, prep.Revisions.Select(r => r.Id));
        Assert.DoesNotContain(prep.Revisions, r => r.Id == first);
        Assert.Equal(ReviewPlanTiming.ReadyAfterEntry, prep.Trades.Single(t => t.TradeId == trades[0].Id).Timing);
        Assert.Equal(second, prep.Trades.Single(t => t.TradeId == trades[1].Id).EligibleRevision);
        var last = prep.Trades.Single(t => t.TradeId == trades[2].Id);
        Assert.Equal(third, last.EligibleRevision); Assert.Equal(Day, last.EntryNewYorkDate);
        Assert.Equal(ReviewPlanMatch.MatchesRecordedIdentity, last.InstrumentMatch); Assert.Equal(ReviewPlanMatch.Unknown, last.SetupMatch);
        Assert.True(last.StandAsideIntent); Assert.False(last.CurrencyMatches); Assert.False(last.ActualInitialRiskKnown);
        Assert.Null(DailyReviewStatisticsCalculator.Calculate(read).Currencies.Single(c => c.Currency == "USD").Net.Metrics.Total);
        var draft = await store.WriteAsync(new(TradingPlanWriteOperation.StartDraft, p.Key, content, new(p.Id, p.Version), latest));
        Assert.Equal(TradingPlanWriteStatus.Success, draft.Status);
        Assert.Equal(ReviewPlanState.ReadyWithDraft, Assert.Single((await reader.GetAsync(query)).Preparation!.Days).State);
        Assert.Empty((await reader.GetAsync(new(Day, Guid.NewGuid()))).Preparation!.Revisions);
        Assert.Equal(3, (await reader.GetAsync(new(Day))).Preparation!.Trades.Count);
    }

    [Theory] [InlineData(ManualReviewKind.Daily)] [InlineData(ManualReviewKind.Weekly)] [InlineData(ManualReviewKind.Monthly)]
    public async Task ManualReviewFreezesReadyWithNoTradingActivityAndSurvivesRestartAndSourceDeletion(ManualReviewKind kind)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var clock = new PlanClock(Close.AddHours(-2)); var store = new TradingPlanStore(db.ContextFactory, clock); var c = Intention(instrument);
        var p = (await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, new(account, Day), c))).Plan!;
        p = await Publish(store, p, c);
        var service = await ManualReviewTests.Service(db);
        var draft = await service.PrepareAsync(new(kind, Day, account, "Original synthetic Account"));
        Assert.Contains("TradingPlanRevision", draft.EvidenceJson); Assert.Contains("Synthetic preparation", draft.Prompt);
        Assert.Contains("untrusted", draft.Prompt); Assert.DoesNotContain("storageKey", draft.Prompt);
        clock.Now = Close.AddHours(1); await Publish(store, p, Intention(instrument, "Later content NOT in saved review"));
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync()) await ctx.TradingAccounts.Where(a => a.Id == account).ExecuteDeleteAsync();
        service = await ManualReviewTests.Service(db); Assert.Equal(draft, await service.LoadAsync(kind));
        var edited = await service.KeepResponseAsync(draft, ManualReviewTests.Response(draft));
        Assert.True(service.Validate(edited).Valid);
        var saved = await service.SaveAsync(edited); Assert.True(saved.Saved);
        Assert.Equal(saved, await service.SaveAsync(saved));
        await using var check = await db.ContextFactory.CreateDbContextAsync();
        var json = kind == ManualReviewKind.Daily ? (await check.CoachingAnalyses.SingleAsync()).EvidenceJson : (await check.PeriodReviews.SingleAsync()).EvidenceJson;
        Assert.Equal(draft.EvidenceJson, json); Assert.DoesNotContain("Later content", json);
        Assert.Empty(await check.Trades.ToArrayAsync()); Assert.Empty(await check.DailyJournals.ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentReadyCommitCannotMixTradeAndPreparationSnapshots()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var clock = new PlanClock(Close.AddHours(-2)); var store = new TradingPlanStore(db.ContextFactory, clock); var c = Intention(instrument);
        var p = (await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, new(account, Day), c))).Plan!;
        p = await Publish(store, p, c); await Add(db, TradeFact(account, instrument, Close));
        var observer = new ReadObserver { BeforeRead = async count => { if (count == 2) { clock.Now = Close.AddMinutes(-10); await Publish(store, p, Intention(instrument, "Concurrent Ready")); } } };
        var read = await (await ObservedReader(db, observer)).GetAsync(new(Day, account));
        Assert.Equal(1, Assert.Single(read.Preparation!.Revisions).Id.Revision);
        Assert.Contains((await Reader(db).GetAsync(new(Day, account))).Preparation!.Revisions, r => r.Id.Revision == 2);
    }

    [Fact]
    public async Task PeriodPreparationUsesCurrentAndPreviousDatesAndEntryDayDependencyWithoutWrites()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day, account);
        var clock = new PlanClock(PeriodComparisonEvidence.PreviousQuery(query).FromUtc); var store = new TradingPlanStore(db.ContextFactory, clock); var c = Intention(instrument);
        var key = new TradingPlanKey(account, query.FirstDate.AddDays(-1));
        var p = (await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, c))).Plan!;
        p = await Publish(store, p, c);
        await Add(db, TradeFact(account, instrument, query.FromUtc.AddHours(3), entry: query.FromUtc.AddHours(-1)));
        var result = await db.ServiceProvider.GetRequiredService<IPeriodComparisonEvidenceReader>().ReadComparisonAsync(query);
        var pair = Assert.IsType<PeriodComparisonEvidence>(result.Evidence);
        var current = pair.Current.Preparation!; var previous = pair.Previous.Preparation!;
        Assert.Equal(query.FirstDate, current.FirstDate); Assert.Equal(query.FirstDate.AddDays(-7), previous.FirstDate);
        Assert.Equal(p.LatestReadyRevision!.Id, Assert.Single(current.Trades).EligibleRevision);
        Assert.Contains(current.Days, d => d.Key == key); Assert.Single(previous.Revisions); Assert.Empty(previous.Trades);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(db).GetAsync(new(Day), new(true)));
    }
}
