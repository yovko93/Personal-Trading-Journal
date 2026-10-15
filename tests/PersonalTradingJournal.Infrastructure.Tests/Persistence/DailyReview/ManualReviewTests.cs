using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed class ManualReviewTests
{
    private static readonly DateOnly Day = new(2026, 10, 2);
    [Theory]
    [InlineData(ManualReviewKind.Daily, false)] [InlineData(ManualReviewKind.Daily, true)]
    [InlineData(ManualReviewKind.Weekly, false)] [InlineData(ManualReviewKind.Weekly, true)]
    [InlineData(ManualReviewKind.Monthly, false)] [InlineData(ManualReviewKind.Monthly, true)]
    public async Task PrepareRestartValidateAndSaveFrozenScopeOnce(ManualReviewKind kind, bool exact)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var account = new TradingAccount("Synthetic original", TradingAccountType.Personal, null, null, "USD", 0, DateTimeOffset.UtcNow);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        Guid? scope = exact ? account.Id : null;
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var journal = (await journals.CreateAsync(new(Day, scope, "Synthetic observation only"))).Journal!.Entry;
        var service = await Service(db);
        var draft = await service.PrepareAsync(new(kind, Day, scope, exact ? account.Name : "All accounts"));
        Assert.Contains("BEGIN UNTRUSTED EVIDENCE", draft.Prompt);
        Assert.Contains("Synthetic observation only", draft.Prompt);
        Assert.Contains("contractVersion", draft.Prompt);
        Assert.Equal(scope, draft.Request.AccountId);
        var restarted = await Service(db);
        Assert.Equal(draft, await restarted.LoadAsync(kind));
        await journals.DeleteAsync(new(journal.Id, journal.Revision));
        var response = Response(draft);
        var edited = await restarted.KeepResponseAsync(draft, "```json\n" + response + "\n```");
        var validation = restarted.Validate(edited);
        Assert.True(validation.Valid, validation.Message);
        Assert.Equal(scope, validation.Daily?.Summary.Scope.AccountId ?? validation.Period?.Summary.Scope.AccountId);
        var saved = await restarted.SaveAsync(edited);
        Assert.True(saved.Saved);
        Assert.Equal(saved, await restarted.SaveAsync(saved));
        var again = await Service(db); Assert.Equal(saved, await again.LoadAsync(kind));
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(kind == ManualReviewKind.Daily ? 1 : 0, await context.CoachingAnalyses.CountAsync());
        Assert.Equal(kind == ManualReviewKind.Daily ? 0 : 1, await context.PeriodReviews.CountAsync());
        string stored = kind == ManualReviewKind.Daily ? (await context.CoachingAnalyses.SingleAsync()).EvidenceJson : (await context.PeriodReviews.SingleAsync()).EvidenceJson;
        Assert.Equal(draft.EvidenceJson, stored); Assert.Empty(await context.DailyJournals.ToArrayAsync());
        await again.DiscardAsync(saved); Assert.Null(await again.LoadAsync(kind));
    }

    [Theory]
    [InlineData(ManualReviewKind.Daily)] [InlineData(ManualReviewKind.Weekly)] [InlineData(ManualReviewKind.Monthly)]
    public async Task InvalidResponsesRemainPendingAndNeverSave(ManualReviewKind kind)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); await Seed(db);
        var service = await Service(db); var draft = await service.PrepareAsync(new(kind, Day, null, "All accounts"));
        foreach (string text in new[] { "not JSON", "{}", Response(draft).Replace(PacketId(draft), "wrong"), Response(draft).Replace(Source(draft), "unknown-source"),
            "prefix\n```json\n" + Response(draft) + "\n```", Response(draft) + Response(draft) })
        {
            draft = await service.KeepResponseAsync(draft, text);
            Assert.False(service.Validate(draft).Valid);
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(draft));
            Assert.Equal(text, (await (await Service(db)).LoadAsync(kind))!.ResponseText);
        }
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.CoachingAnalyses.ToArrayAsync()); Assert.Empty(await context.PeriodReviews.ToArrayAsync());
        var wrongScope = draft with { Request = draft.Request with { AccountId = Guid.NewGuid() }, ResponseText = Response(draft) };
        Assert.False(service.Validate(wrongScope).Valid);
    }

    [Fact]
    public async Task CancellationOversizeAndConflictsPreservePendingFiles()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); await Seed(db);
        var service = await Service(db); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrepareAsync(new(ManualReviewKind.Daily, Day, null, "All accounts"), cancelled.Token));
        Assert.Null(await service.LoadAsync(ManualReviewKind.Daily));
        var draft = await service.PrepareAsync(new(ManualReviewKind.Daily, Day, null, "All accounts"));
        await Assert.ThrowsAsync<ManualReviewException>(() => service.PrepareAsync(new(ManualReviewKind.Daily, Day.AddDays(1), null, "All accounts")));
        await Assert.ThrowsAsync<ManualReviewException>(() => service.KeepResponseAsync(draft, new string('x', 65_537)));
        var updated = await service.KeepResponseAsync(draft, "unfinished");
        await Assert.ThrowsAsync<ManualReviewException>(() => service.KeepResponseAsync(draft, "stale"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DiscardAsync(updated, cancelled.Token));
        Assert.Equal(updated, await service.LoadAsync(ManualReviewKind.Daily));
    }

    [Fact]
    public async Task EmptyDayCreatesNoPendingWork()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var service = await Service(db);
        await Assert.ThrowsAsync<ManualReviewException>(() => service.PrepareAsync(new(ManualReviewKind.Daily, Day, null, "All accounts")));
        Assert.Null(await service.LoadAsync(ManualReviewKind.Daily));
    }

    private static async Task Seed(ReaderTestDatabase db) => await db.ServiceProvider.GetRequiredService<IDailyJournalRepository>().CreateAsync(new(Day, null, "Synthetic data"));
    [Theory] [InlineData(ManualReviewKind.Daily)] [InlineData(ManualReviewKind.Weekly)] [InlineData(ManualReviewKind.Monthly)]
    public async Task InterruptedSaveReconcilesAlreadyCommittedRowWithoutDuplicates(ManualReviewKind kind)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); await Seed(db);
        var service = await Service(db, failAfterCommit: true);
        var draft = await service.PrepareAsync(new(kind, Day, null, "All accounts"));
        draft = await service.KeepResponseAsync(draft, Response(draft));
        await Assert.ThrowsAsync<IOException>(() => service.SaveAsync(draft));
        Assert.False((await service.LoadAsync(kind))!.Saved);
        var resumed = await Service(db); var saved = await resumed.SaveAsync(draft); Assert.True(saved.Saved);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.CoachingAnalyses.CountAsync() + await context.PeriodReviews.CountAsync());
    }

    private static async Task<ManualReviewService> Service(ReaderTestDatabase db, bool failAfterCommit = false)
    {
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        string path = new SqliteConnectionStringBuilder(context.Database.GetConnectionString()).DataSource;
        var daily = db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>(); var period = db.ServiceProvider.GetRequiredService<IPeriodReviewRepository>();
        return new(new LocalApplicationPaths(Path.GetDirectoryName(path)!), db.ServiceProvider.GetRequiredService<IDailyReviewEvidenceReader>(),
            db.ServiceProvider.GetRequiredService<IPeriodComparisonEvidenceReader>(), failAfterCommit ? new InterruptedDaily(daily) : daily,
            failAfterCommit ? new InterruptedPeriod(period) : period, TimeProvider.System);
    }
    private sealed class InterruptedDaily(ICoachingAnalysisRepository inner) : ICoachingAnalysisRepository
    {
        public async Task<SavedCoachingAnalysis> SaveAsync(CoachingAnalysisSnapshot value, CancellationToken token = default) { await inner.SaveAsync(value, token); throw new IOException("Synthetic commit acknowledgement loss"); }
        public Task<SavedCoachingAnalysis?> GetAsync(Guid id, CancellationToken token = default) => inner.GetAsync(id, token);
        public Task<CoachingAnalysisHistoryPage> BrowseAsync(CoachingAnalysisHistoryQuery q, CancellationToken token = default) => inner.BrowseAsync(q, token);
        public Task<HistoricalCoachingAccountPage> BrowseHistoricalAccountsAsync(HistoricalCoachingAccountQuery q, CancellationToken token = default) => inner.BrowseHistoricalAccountsAsync(q, token);
        public Task<bool> DeleteAsync(Guid id, CancellationToken token = default) => inner.DeleteAsync(id, token);
    }
    private sealed class InterruptedPeriod(IPeriodReviewRepository inner) : IPeriodReviewRepository
    {
        public async Task<SavedPeriodReview> SaveAsync(PeriodReviewSnapshot value, CancellationToken token = default) { await inner.SaveAsync(value, token); throw new IOException("Synthetic commit acknowledgement loss"); }
        public Task<SavedPeriodReview?> GetAsync(Guid id, CancellationToken token = default) => inner.GetAsync(id, token);
        public Task<PeriodReviewHistoryPage> BrowseAsync(PeriodReviewHistoryQuery q, CancellationToken token = default) => inner.BrowseAsync(q, token);
        public Task<SavedPeriodScopePage> BrowseScopesAsync(PeriodReviewHistoryQuery q, CancellationToken token = default) => inner.BrowseScopesAsync(q, token);
        public Task<PeriodReviewDeleteStatus> DeleteAsync(Guid id, bool confirmed, CancellationToken token = default) => inner.DeleteAsync(id, confirmed, token);
    }
    internal static string PacketId(ManualReviewDraft draft) => JsonNode.Parse(draft.EvidenceJson)!["packetId"]!.GetValue<string>();
    internal static string Source(ManualReviewDraft draft) => draft.Request.Kind == ManualReviewKind.Daily ? "calculated:day" : "current:statistics";
    internal static string Response(ManualReviewDraft draft)
    {
        string id = PacketId(draft), source = Source(draft);
        var finding = new { basis = "CalculatedFact", text = "Insufficient evidence for behavioral conclusions.", sourceIds = new[] { source } };
        var section = new { insufficientEvidence = true, findings = new[] { finding } };
        return draft.Request.Kind == ManualReviewKind.Daily ? JsonSerializer.Serialize(new
        {
            contractVersion = CoachingContract.Version, packetId = id, daySummary = new { text = "Synthetic day coverage.", sourceIds = new[] { source } },
            executionObservations = Array.Empty<object>(), behaviorObservations = Array.Empty<object>(), improvementSuggestions = Array.Empty<object>(), uncertainties = Array.Empty<object>(),
        }) : JsonSerializer.Serialize(new
        {
            contractVersion = PeriodCoachingContract.Version, packetId = id, strengths = section, areasForImprovement = section,
            mistakesObserved = section, progressVersusPreviousPeriod = section,
            actionPlan = new { insufficientEvidence = true, explanation = "No supported plan.", sourceIds = new[] { source }, steps = Array.Empty<object>() },
        });
    }
}
