using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed partial class DailyReviewEvidenceReaderTests
{
    [Theory]
    [InlineData(ReviewPeriodKind.Week, false)] [InlineData(ReviewPeriodKind.Month, true)]
    public async Task PeriodCoachingReadsRealSnapshotWithoutSavingAndKeepsSourcesAfterWriterChanges(ReviewPeriodKind kind, bool exact)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var (other, _) = await References(db, "Other synthetic", false);
        var query = new PeriodReviewQuery(kind, Day, exact ? account : null); var previous = PeriodComparisonEvidence.PreviousQuery(query);
        await Add(db, TradeFact(account, instrument, query.FromUtc.AddHours(12), commission: null),
            TradeFact(other, instrument, query.FromUtc.AddHours(14), currency: "EUR"),
            TradeFact(account, instrument, previous.FromUtc.AddHours(12)));
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        await journals.CreateAsync(new(query.FirstDate, null, "Aggregate observation", true));
        await journals.CreateAsync(new(query.FirstDate, account, "Exact observation", true));
        var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
        PeriodCoachingPacket? submitted = null;
        var handler = new PeriodCoachingProviderTests.Handler(async (request, token) =>
        {
            using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            string json = body.RootElement.GetProperty("input")[0].GetProperty("content").GetString()!;
            // An ordinary writer can commit after the read snapshot. It cannot change the sent packet.
            await using var writer = await db.ContextFactory.CreateDbContextAsync();
            foreach (var j in await writer.DailyJournals.ToListAsync(token)) j.Text = "Later observation";
            await writer.SaveChangesAsync(token);
            using var packetDoc = System.Text.Json.JsonDocument.Parse(json);
            string id = packetDoc.RootElement.GetProperty("packetId").GetString()!;
            var sources = packetDoc.RootElement.GetProperty("sources");
            Assert.Equal(exact ? 1 : 2, sources.EnumerateArray().Count(s => s.GetProperty("period").GetString() == "current" && s.GetProperty("kind").GetString() == "Trade"));
            var prototype = PeriodCoachingProviderTests.Packet(PeriodCoachingProviderTests.Evidence(kind));
            string response = PeriodCoachingProviderTests.Review(prototype).Replace(prototype.PacketId, id);
            return PeriodCoachingProviderTests.Response(200, PeriodCoachingProviderTests.Envelope(false, response).ToJsonString());
        });
        using var client = new HttpClient(handler);
        var generator = new PeriodCoachingGenerationService(reader, new OpenAiCoachingProvider(client, new(), () => "synthetic"), new(), TimeProvider.System);
        var result = await generator.GenerateAsync(query); Assert.Equal(PeriodCoachingStatus.Success, result.Status); submitted = result.Packet;
        Assert.Equal(1, handler.Calls); Assert.Equal(0, observer.NonReads); Assert.False(observer.Tracked);
        Assert.Contains("Exact observation", submitted!.Json); Assert.DoesNotContain("Later observation", submitted.Json);
        Assert.Equal(exact ? 1 : 2, submitted.Sources.Count(s => s.Period == "current" && s.Kind == PeriodCoachingSourceKind.Journal));
        await using var verify = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(3, await verify.Trades.CountAsync()); Assert.Equal(2, await verify.DailyJournals.CountAsync());
        Assert.Equal(2, await verify.DailyJournalRevisions.CountAsync()); Assert.Equal(0, await verify.CoachingAnalyses.CountAsync());
        Assert.All(await verify.DailyJournals.ToListAsync(), j => Assert.Equal("Later observation", j.Text));
        // Restart-style fresh repository read sees current data, but cannot rewrite the response's original packet.
        var refreshed = await db.ServiceProvider.GetRequiredService<IPeriodComparisonEvidenceReader>().ReadComparisonAsync(query);
        Assert.All(refreshed.Evidence!.Current.Journals, j => Assert.Equal("Later observation", j.Text));
        Assert.DoesNotContain("Later observation", submitted.Json);
    }

    [Theory] [InlineData(31, false)] [InlineData(300, true)]
    public async Task LargerPeriodsRespectSnapshotLimitsWithoutPartialSave(int count, bool tooLarge)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var (account, instrument) = await References(db);
        var query = new PeriodReviewQuery(ReviewPeriodKind.Week, Day, account);
        await Add(db, Enumerable.Range(0, count).Select(i => TradeFact(account, instrument, query.FromUtc.AddDays(i % 5).AddHours(12))).ToArray());
        var handler = new PeriodCoachingProviderTests.Handler(async (request, token) =>
        {
            Assert.False(tooLarge);
            using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            using var packet = System.Text.Json.JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            var prototype = PeriodCoachingProviderTests.Packet(PeriodCoachingProviderTests.Evidence());
            var response = PeriodCoachingProviderTests.Review(prototype).Replace(prototype.PacketId, packet.RootElement.GetProperty("packetId").GetString()!);
            return PeriodCoachingProviderTests.Response(200, PeriodCoachingProviderTests.Envelope(true, response).ToJsonString());
        });
        using var client = new HttpClient(handler);
        var generator = new PeriodCoachingGenerationService(db.ServiceProvider.GetRequiredService<IPeriodComparisonEvidenceReader>(),
            new GroqCoachingProvider(client, new(), () => "synthetic"), new(), TimeProvider.System);
        var result = await new GenerateAndSavePeriodReviewService(generator, db.ServiceProvider.GetRequiredService<IPeriodReviewRepository>(), TimeProvider.System).GenerateAsync(query);
        Assert.Equal(tooLarge ? PeriodCoachingStatus.TooLarge : PeriodCoachingStatus.Success, result.GenerationStatus);
        Assert.Equal(!tooLarge, result.Review is not null); Assert.Equal(tooLarge ? 0 : 1, handler.Calls);
        await using var verify = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(count, await verify.Trades.CountAsync()); Assert.Equal(count * 2, await verify.TradeExecutions.CountAsync());
        Assert.Equal(tooLarge ? 0 : 1, await verify.PeriodReviews.CountAsync());
        Assert.Equal(0, await verify.CoachingAnalyses.CountAsync()); Assert.Equal(0, await verify.DailyJournals.CountAsync());
    }
}
