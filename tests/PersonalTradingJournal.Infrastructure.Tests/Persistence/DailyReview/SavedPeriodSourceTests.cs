using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed partial class DailyReviewEvidenceReaderTests
{
    [Fact]
    public async Task SavedPeriodSurvivesSourceDeletionSameNameReplacementAndFreshServicesWithoutProvider()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db, "Original exact scope");
        var q = new PeriodReviewQuery(ReviewPeriodKind.Week, Day, account);
        var trade = TradeFact(account, instrument, q.FromUtc.AddHours(12), fees: null); await Add(db, trade);
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var journal = (await journals.CreateAsync(new(q.FirstDate, account, "Original user observation"))).Journal!.Entry;
        var store = db.ServiceProvider.GetRequiredService<IPeriodReviewRepository>(); var calls = 0;
        var generator = new PeriodCoachingGenerationService(db.ServiceProvider.GetRequiredService<IPeriodComparisonEvidenceReader>(),
            new PeriodProvider(async packet =>
            {
                calls++;
                await journals.UpdateAsync(new(journal.Id, journal.Revision, "Changed while generation pending"));
                return new(CoachingGenerationStatus.Success, PeriodCoachingProviderTests.Review(packet), new("Fake", "synthetic", "client"));
            }), new(), TimeProvider.System);
        var result = await new GenerateAndSavePeriodReviewService(generator, store, TimeProvider.System).GenerateAsync(q);
        Assert.Equal(SavedCoachingGenerationStatus.Saved, result.Status); Assert.Equal(1, calls); var saved = result.Review!;
        Assert.Contains("Original user observation", saved.EvidenceJson); Assert.DoesNotContain("Changed while", saved.EvidenceJson);
        Assert.Equal("Original exact scope", saved.Summary.AccountDisplayName);
        await journals.DeleteAsync(new(journal.Id, journal.Revision + 1));
        string connection;
        await using (var change = await db.ContextFactory.CreateDbContextAsync())
        {
            connection = change.Database.GetConnectionString()!;
            await change.Trades.Where(t => t.Id == trade.Id).ExecuteDeleteAsync();
            await change.TradingAccounts.Where(a => a.Id == account).ExecuteDeleteAsync();
        }
        var (replacement, _) = await References(db, "Original exact scope"); Assert.NotEqual(account, replacement);
        var factory = new ObservedFactory(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection).Options);
        // Fresh repository/context lifetime; history has no dependency on provider selection, credentials or current sources.
        var restarted = new PersonalTradingJournal.Infrastructure.PeriodReviews.PeriodReviewRepository(factory);
        Assert.Equal(saved, await restarted.GetAsync(saved.Summary.Id));
        Assert.Single((await restarted.BrowseAsync(new(q))).Items);
        Assert.Empty((await restarted.BrowseAsync(new(new(q.Kind, q.FirstDate, replacement)))).Items);
        var scope = Assert.Single((await restarted.BrowseScopesAsync(new(new(q.Kind, q.FirstDate)))).Items);
        Assert.Equal(account, scope.Scope.AccountId); Assert.Equal("Original exact scope", scope.AccountDisplayName);
        Assert.Equal(1, calls);
    }
    private sealed class PeriodProvider(Func<PeriodCoachingPacket, Task<CoachingProviderReply>> generate) : IPeriodCoachingProvider
    {
        public Task<CoachingProviderReply> GenerateAsync(PeriodCoachingPacket packet, CancellationToken token) => generate(packet);
    }
}
