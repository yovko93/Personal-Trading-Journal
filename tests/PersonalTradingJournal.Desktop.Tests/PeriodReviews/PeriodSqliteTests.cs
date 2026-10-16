using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PersonalTradingJournal.Desktop.Tests.PeriodReviews;

public sealed class PeriodSqliteTests
{
    [Theory] [InlineData(ReviewPeriodKind.Week, false)] [InlineData(ReviewPeriodKind.Month, false)] [InlineData(ReviewPeriodKind.Week, true)]
    public async Task TwentyFiveTradeExplicitGroqProjectionSavesFullSnapshotAndDisplaysCards(ReviewPeriodKind kind, bool unsupportedCitation)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var account = new TradingAccount("Synthetic", TradingAccountType.Personal, null, null, "USD", 0, ReviewFixture.Now);
        var instrument = new Instrument("NQ", "Synthetic", AssetClass.Futures, "CME", "USD", .25m, .25m, ReviewFixture.Now);
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await db.Provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        var q = new PeriodReviewQuery(kind, ReviewFixture.Day, account.Id);
        for (int i = 0; i < 25; i++)
        {
            var id = Guid.NewGuid(); var close = q.FromUtc.AddDays(i % 5).AddHours(12);
            var trade = Trade.Rehydrate(id, account.Id, instrument.Id, new(1, "USD"), null,
                [new(id, 1, close.AddHours(-1), ExecutionSide.Buy, 1, 100, 0, 0, null, null, null),
                 new(id, 2, close, ExecutionSide.Sell, 1, 102, i == 0 ? null : 0, 0, null, null, null)], ReviewFixture.Now, ReviewFixture.Now);
            await db.Provider.GetRequiredService<ITradeStore>().AddAsync(trade);
        }
        var reader = db.Provider.GetRequiredService<IPeriodComparisonEvidenceReader>();
        var store = db.Provider.GetRequiredService<IPeriodReviewRepository>();
        var build = PeriodCoachingPacketBuilder.Build((await reader.ReadComparisonAsync(q)).Evidence!);
        Assert.NotNull(build.Packet); var packet = build.Packet!;
        using var handler = new SyntheticGroq(packet, unsupportedCitation);
        using var client = new HttpClient(handler);
        var service = new GenerateAndSavePeriodReviewService(new(reader, new GroqCoachingProvider(client, new(), () => "synthetic-only"), new(), TimeProvider.System), store, TimeProvider.System);
        var vm = new PeriodReviewViewModel(kind, reader, store, db.Provider.GetRequiredService<ITradingAccountReader>(), new FakeDialogService(),
            new PeriodFixture.Clock(ReviewFixture.Now), service.GenerateAsync, new PeriodFixture.CredentialsFake());
        await vm.ActivateAsync(); vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account.Id); await vm.LoadTask;
        Assert.Equal(0, handler.Calls); Assert.Empty(vm.History);
        await vm.GenerateCommand.ExecuteAsync(null);
        if (unsupportedCitation)
        {
            Assert.Equal(1, handler.Calls); Assert.Null(vm.Report); Assert.Empty(vm.History); Assert.NotNull(vm.Message);
            await using var rejected = await db.ContextFactory.CreateDbContextAsync();
            Assert.Empty(await rejected.PeriodReviews.ToArrayAsync()); Assert.Equal(25, await rejected.Trades.CountAsync());
            vm.Deactivate(); return;
        }
        Assert.True(vm.Report is not null, vm.Message); Assert.Equal(1, handler.Calls); Assert.Equal(5, vm.Report.Cards.Count);
        Assert.Contains("Net is unavailable", vm.Current!.Coverage);
        var saved = (await store.GetAsync(Assert.Single(vm.History).Source.Id))!;
        Assert.Equal(packet.Json, saved.EvidenceJson); Assert.Equal("period-coaching.v2", saved.ResponseContractVersion);
        Assert.Equal(25, packet.Sources.Count(s => s.Kind == PeriodCoachingSourceKind.Trade));
        Assert.Equal(50, packet.Sources.Count(s => s.Kind == PeriodCoachingSourceKind.Execution));
        Assert.Equal(2, packet.SuppliedSources.Count(s => s.Kind == PeriodCoachingSourceKind.Trade));
        vm.Deactivate();
        // Reopen immutable details from a fresh repository read; no extra HTTP call.
        Assert.Equal(5, PeriodReport.Read((await store.GetAsync(saved.Summary.Id))!).Cards.Count);
        await using var verify = await db.ContextFactory.CreateDbContextAsync();
        Assert.Single(await verify.PeriodReviews.ToArrayAsync()); Assert.Equal(25, await verify.Trades.CountAsync());
    }
    private sealed class SyntheticGroq(PeriodCoachingPacket packet, bool unsupportedCitation) : HttpMessageHandler
    {
        internal int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
            Assert.Equal(packet.ProviderJson, body["messages"]![1]!["content"]!.GetValue<string>());
            Assert.True(body["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());
            string source = unsupportedCitation ? packet.Sources.Last(s => s.Kind == PeriodCoachingSourceKind.Trade).Id : "current:statistics";
            var section = new { insufficientEvidence = false, findings = new[] { new { basis = unsupportedCitation ? "RecordedEvidence" : "CalculatedFact", text = "25 closed Trades are recorded. Net coverage is incomplete.", sourceIds = new[] { source } } } };
            string review = JsonSerializer.Serialize(new { contractVersion = packet.ContractVersion, packetId = packet.PacketId,
                strengths = section, areasForImprovement = section, mistakesObserved = section, progressVersusPreviousPeriod = section,
                actionPlan = new { insufficientEvidence = true, explanation = "No supported next step from this synthetic response.", sourceIds = new[] { "current:statistics" }, steps = Array.Empty<object>() } });
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                id = "chatcmpl-synthetic", model = GroqCoachingOptions.SupportedModel, @object = "chat.completion",
                choices = new[] { new { finish_reason = "stop", message = new { role = "assistant", content = review } } },
                usage = new { prompt_tokens = 4000, completion_tokens = 400, total_tokens = 4400 } })) };
        }
    }
    [Theory] [InlineData(ReviewPeriodKind.Week)] [InlineData(ReviewPeriodKind.Month)]
    public async Task BoundCommandSavesThenFreshWorkspaceDiscoversDeletedAccountSnapshot(ReviewPeriodKind kind)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = Guid.NewGuid();
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            context.TradingAccounts.Add(new TradingAccountRecord { Id = account, Name = "Synthetic scope", Currency = "USD", AccountType = TradingAccountType.Personal, IsActive = true, CreatedAtUtc = ReviewFixture.Now, UpdatedAtUtc = ReviewFixture.Now });
            await context.SaveChangesAsync();
        }
        var j = (await db.Repository.CreateAsync(new(ReviewFixture.Day, account, "Original snapshot observation"))).Journal!.Entry;
        var reader = db.Provider.GetRequiredService<IPeriodComparisonEvidenceReader>(); var store = db.Provider.GetRequiredService<IPeriodReviewRepository>();
        var provider = new PeriodFixture.ProviderFake(); var service = new GenerateAndSavePeriodReviewService(new(reader, provider, new(), TimeProvider.System), store, TimeProvider.System);
        var dialogs = new FakeDialogService();
        PeriodReviewViewModel Create() => new(kind, reader, store, db.Provider.GetRequiredService<ITradingAccountReader>(), dialogs,
            new PeriodFixture.Clock(ReviewFixture.Now), service.GenerateAsync, new PeriodFixture.CredentialsFake(), db.Repository);
        var vm = Create(); await vm.ActivateAsync(); vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account); await vm.LoadTask;
        Assert.Equal(0, provider.Calls); await vm.GenerateCommand.ExecuteAsync(null);
        var id = Assert.Single(vm.History).Source.Id; var saved = (await store.GetAsync(id))!;
        PeriodReport.Read(saved); // Surface deterministic saved-contract parsing failures, without logging content.
        Assert.True(vm.Report is not null, vm.Message);
        Assert.Equal(provider.Packet!.Json, saved.EvidenceJson); Assert.Contains("Original snapshot observation", saved.EvidenceJson);
        var journalCitation = Assert.Single(vm.Report.Cards[0].Findings).Citations.Single(c => c.HasJournal);
        await db.Repository.DeleteAsync(new(j.Id, j.Revision));
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        { await context.TradingAccounts.Where(a => a.Id == account).ExecuteDeleteAsync(); }
        Guid replacement = Guid.NewGuid();
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        { context.TradingAccounts.Add(new TradingAccountRecord { Id = replacement, Name = "Synthetic scope", Currency = "USD", AccountType = TradingAccountType.Personal, IsActive = true, CreatedAtUtc = ReviewFixture.Now, UpdatedAtUtc = ReviewFixture.Now }); await context.SaveChangesAsync(); }
        vm.Deactivate(); vm = Create(); await vm.ActivateAsync();
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account); await vm.LoadTask;
        Assert.True(vm.SelectedAccount.IsHistorical); Assert.False(vm.GenerateCommand.CanExecute(null));
        await vm.OpenCommand.ExecuteAsync(Assert.Single(vm.History)); Assert.NotNull(vm.Report); Assert.Equal(saved, await store.GetAsync(id));
        bool navigated = false; vm.OpenJournalAsync = _ => { navigated = true; return Task.CompletedTask; };
        await vm.OpenJournalCommand.ExecuteAsync(journalCitation); Assert.False(navigated); Assert.Contains("original Journal is unavailable", vm.Message);
        await vm.DeleteCommand.ExecuteAsync(vm.History.Single()); Assert.NotNull(await store.GetAsync(id));
        dialogs.ConfirmationResult = true; await vm.DeleteCommand.ExecuteAsync(vm.History.Single()); Assert.Null(await store.GetAsync(id));
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        { Assert.Single(await context.TradingAccounts.ToArrayAsync()); Assert.Empty(await context.PeriodReviews.ToArrayAsync()); Assert.Empty(await context.CoachingAnalyses.ToArrayAsync()); }
        Assert.Equal(1, provider.Calls); vm.Deactivate();
    }
}
