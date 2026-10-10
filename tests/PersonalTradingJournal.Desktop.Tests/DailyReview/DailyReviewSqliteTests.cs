using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class DailyReviewSqliteTests
{
    [Theory]
    [InlineData(200, false)]
    [InlineData(200, true)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    public async Task SettingsCredentialThroughGenerateCommandValidatesSavesAndDisplaysOnlySuccessfulResponse(int status, bool invalidCitation)
    {
        await using var database = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        await database.Repository.CreateAsync(new(ReviewFixture.Day, null, "Synthetic journal for HTTP boundary test."));
        using var secret = new Settings.CoachingCredentialsTests.SecretFixture();
        using var settings = Settings.CoachingCredentialsTests.Settings(secret.Store);
        using var handler = new SyntheticHttpHandler(); using var client = new HttpClient(handler);
        var reader = database.Provider.GetRequiredService<IDailyReviewEvidenceReader>();
        var packet = CoachingEvidencePacketBuilder.Build(await reader.GetAsync(new(ReviewFixture.Day))).Packet!;
        handler.Response = status != 200
            ? JsonSerializer.Serialize(new { error = new { code = "model_not_found", type = "invalid_request_error", message = "private raw text" } })
            : JsonSerializer.Serialize(new { id = "resp_synthetic", model = OpenAiCoachingOptions.SupportedModel, status = "completed",
                output = new[] { new { type = "message", role = "assistant", status = "completed", content = new[] { new { type = "output_text",
                    text = DailyReviewGenerationTests.Provider.Json(packet, source: invalidCitation ? "not-supplied" : "calculated:day") } } } } });
        handler.Status = status;
        var repository = database.Provider.GetRequiredService<ICoachingAnalysisRepository>();
        var vm = new DailyReviewViewModel(reader, repository, database.Provider.GetRequiredService<ITradingAccountReader>(),
            new FakeDialogService(), new FixedTimeProvider(), new(new(new OpenAiCoachingProvider(client, new(), secret.Store.Resolve), new(), TimeProvider.System),
                repository, TimeProvider.System), secret.Store);
        settings.SaveKey("synthetic-integration-key");
        Assert.Equal(0, handler.Calls);
        vm.SelectedDate = ReviewFixture.Day.ToDateTime(TimeOnly.MinValue);
        try
        {
            await vm.ActivateAsync(); Assert.Equal(0, handler.Calls);
            await vm.GenerateCommand.ExecuteAsync(null);
            Assert.Equal(1, handler.Calls); Assert.True(handler.KeyMatched);
            bool saved = status == 200 && !invalidCitation;
            await using var db = await database.ContextFactory.CreateDbContextAsync();
            Assert.Equal(saved ? 1 : 0, await db.CoachingAnalyses.CountAsync());
            Assert.Equal(saved, vm.Snapshot is not null);
            Assert.Contains($"HTTP: {status}", vm.GenerationDiagnostics);
            Assert.Contains(saved ? "Phase: Saved" : status == 200 ? "Phase: ResponseValidation" : "error.code: model_not_found", vm.GenerationDiagnostics);
            Assert.DoesNotContain("private", vm.GenerationDiagnostics);
            if (saved) Assert.Equal(packet.Json, (await repository.GetAsync(Assert.Single(vm.Analyses).Source.Id))!.EvidenceJson);
            else Assert.Empty(vm.Analyses);
        }
        finally { vm.Deactivate(); }
    }

    private sealed class SyntheticHttpHandler : HttpMessageHandler
    {
        internal int Status; internal string Response = ""; internal int Calls; internal bool KeyMatched;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; KeyMatched = request.Headers.Authorization?.Parameter == "synthetic-integration-key";
            var response = new HttpResponseMessage((HttpStatusCode)Status) { Content = new StringContent(Response) };
            response.Headers.Add("x-request-id", "req_synthetic"); return Task.FromResult(response);
        }
    }
    [Fact]
    public async Task ExplicitGenerationAtomicallySavesOneSnapshotAndFailureLeavesDatabaseUnchanged()
    {
        await using var database = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        await database.Repository.CreateAsync(new(ReviewFixture.Day, null, "Journal-only day, no Trades."));
        var provider = new DailyReviewGenerationTests.Provider();
        var repository = database.Provider.GetRequiredService<ICoachingAnalysisRepository>();
        var vm = new DailyReviewViewModel(database.Provider.GetRequiredService<IDailyReviewEvidenceReader>(),
            repository, database.Provider.GetRequiredService<ITradingAccountReader>(), new FakeDialogService(),
            new FixedTimeProvider(), new(new(provider, new(), TimeProvider.System), repository, TimeProvider.System));
        vm.SelectedDate = ReviewFixture.Day.ToDateTime(TimeOnly.MinValue);
        try
        {
            await vm.ActivateAsync();
            await vm.GenerateCommand.ExecuteAsync(null);
            var saved = await repository.GetAsync(Assert.Single(vm.Analyses).Source.Id);
            Assert.NotNull(saved);
            Assert.Equal(provider.Packet!.Json, saved.EvidenceJson);
            Assert.Equal("Journal-only day, no Trades.", Assert.Single(vm.Snapshot!.Evidence.Journals).Source.Text);
            provider.Handler = (_, _) => Task.FromResult(new CoachingProviderReply(CoachingGenerationStatus.RateLimited, null, null));
            await vm.GenerateCommand.ExecuteAsync(null);
            await using var db = await database.ContextFactory.CreateDbContextAsync();
            Assert.Single(await db.CoachingAnalyses.AsNoTracking().ToArrayAsync());
            Assert.Single(await db.DailyJournals.AsNoTracking().ToArrayAsync());
            Assert.Empty(await db.Trades.AsNoTracking().ToArrayAsync());
            Assert.Equal(2, provider.Calls);
        }
        finally { vm.Deactivate(); }
    }

    [Fact]
    public async Task RestartDiscoversDeletedScopeBySavedNameAndIdWithoutSubstitutingSameNameAccountOrAggregate()
    {
        await using var database = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var store = database.Provider.GetRequiredService<ITradingAccountStore>();
        var original = new TradingAccount("P 21", TradingAccountType.Personal, null, null, "USD", 0m, ReviewFixture.Now);
        await store.AddAsync(original);
        var journal = (await database.Repository.CreateAsync(new(ReviewFixture.Day, original.Id, "Original evidence"))).Journal!.Entry;
        var reader = database.Provider.GetRequiredService<IDailyReviewEvidenceReader>();
        var repository = database.Provider.GetRequiredService<ICoachingAnalysisRepository>();
        async Task<SavedCoachingAnalysis> Save(Guid? id)
        {
            var packet = CoachingEvidencePacketBuilder.Build(await reader.GetAsync(new(ReviewFixture.Day, id))).Packet!;
            return await repository.SaveAsync(CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
                new(packet.ContractVersion, packet.PacketId, new("Saved facts.", ["calculated:day"]), [], [], [], []),
                new("Fake", "test-model", "client-test", null, null, null), "Not shown"), ReviewFixture.Now));
        }
        var exact = await Save(original.Id);
        var aggregate = await Save(null);
        await database.Repository.DeleteAsync(new(journal.Id, journal.Revision));
        Assert.Equal(DeleteTradingAccountResult.Deleted, await new DeleteTradingAccountUseCase(store,
            database.Provider.GetRequiredService<ITradingAccountDeletionStore>()).ExecuteAsync(original.Id));
        var replacement = new TradingAccount("P 21", TradingAccountType.Personal, null, null, "USD", 0m, ReviewFixture.Now);
        await store.AddAsync(replacement);
        await database.Repository.CreateAsync(new(ReviewFixture.Day, replacement.Id, "Replacement evidence"));
        var replacementAnalysis = await Save(replacement.Id);
        string databasePath;
        await using (var context = await database.ContextFactory.CreateDbContextAsync())
            databasePath = context.Database.GetDbConnection().DataSource;
        // No live Account object, cached selector, or old repository survives this service restart.
        await database.Provider.DisposeAsync();
        await using var restarted = new ServiceCollection().AddPersistence(
            new LocalApplicationPaths(Path.GetDirectoryName(Path.GetDirectoryName(databasePath))!)).BuildServiceProvider();
        var vm = new DailyReviewViewModel(restarted.GetRequiredService<IDailyReviewEvidenceReader>(),
            restarted.GetRequiredService<ICoachingAnalysisRepository>(), restarted.GetRequiredService<ITradingAccountReader>(),
            new FakeDialogService(), new FixedTimeProvider());
        vm.SelectedDate = ReviewFixture.Day.ToDateTime(TimeOnly.MinValue);
        try
        {
            await vm.ActivateAsync();
            Assert.Null(vm.ScopeError);
            Assert.Equal(aggregate.Summary.Id, Assert.Single(vm.Analyses).Source.Id);
            var historical = Assert.Single(vm.Accounts, a => a.Id == original.Id);
            Assert.Contains("P 21", historical.Label);
            Assert.Contains(original.Id.ToString(), historical.Label);
            Assert.Contains("historical / unavailable", historical.Label);
            Assert.False(vm.Accounts.Single(a => a.Id == replacement.Id).IsHistorical);
            vm.SelectedAccount = historical; // Same property used by the compiled ComboBox.
            await vm.LoadTask;
            Assert.Equal(original.Id, vm.SelectedAccount.Id);
            Assert.Empty(vm.Current!.Journals);
            Assert.Empty(vm.Current.Trades);
            Assert.Contains("original ID", vm.AccountNotice);
            Assert.Equal(exact.Summary.Id, Assert.Single(vm.Analyses).Source.Id);
            await vm.OpenAnalysisCommand.ExecuteAsync(vm.Analyses.Single());
            var source = Assert.Single(vm.Snapshot!.Evidence.Journals).Source;
            Assert.Equal(original.Id, source.TradingAccountId);
            Assert.Equal("Original evidence", source.Text);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == replacement.Id);
            await vm.LoadTask;
            Assert.Equal(replacementAnalysis.Summary.Id, Assert.Single(vm.Analyses).Source.Id);
            Assert.Equal("Replacement evidence", Assert.Single(vm.Current!.Journals).Source.Text);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null);
            await vm.LoadTask;
            Assert.Equal(aggregate.Summary.Id, Assert.Single(vm.Analyses).Source.Id);
            await vm.OpenAnalysisCommand.ExecuteAsync(vm.Analyses.Single());
            Assert.Equal(original.Id, Assert.Single(vm.Snapshot!.Evidence.Journals).Source.TradingAccountId);
        }
        finally { vm.Deactivate(); await vm.LoadTask; }
    }

    [Fact]
    public async Task MigratedWorkspaceReadsExactAndAggregateSourcesThenRetainsSavedSnapshotAfterJournalDeletion()
    {
        await using var database = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var account = new TradingAccount("P 21", TradingAccountType.Personal, null, null, "USD", 0m, ReviewFixture.Now);
        account.Deactivate(ReviewFixture.Now.AddHours(1));
        await database.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        var date = ReviewFixture.Day;
        var global = (await database.Repository.CreateAsync(new(date, null, "All accounts reflection"))).Journal!.Entry;
        var scoped = (await database.Repository.CreateAsync(new(date, account.Id, "Original scoped text"))).Journal!.Entry;
        var reader = database.Provider.GetRequiredService<IDailyReviewEvidenceReader>();
        var history = database.Provider.GetRequiredService<ICoachingAnalysisRepository>();
        var evidence = await reader.GetAsync(new(date));
        // Save through the real validated snapshot boundary, not a raw persistence record.
        var packet = CoachingEvidencePacketBuilder.Build(evidence).Packet!;
        var snapshot = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Saved reflection.", ["calculated:day"]), [], [], [], []),
            new("Fake", "test-model", "client-test", null, null, null), "Not shown"), ReviewFixture.Now);
        await history.SaveAsync(snapshot);
        var dialogs = new FakeDialogService { ConfirmationResult = true };
        var vm = new DailyReviewViewModel(reader, history, database.Provider.GetRequiredService<ITradingAccountReader>(), dialogs, new FixedTimeProvider());
        vm.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
        try
        {
            await vm.ActivateAsync();
            Assert.Equal(2, vm.Current!.Journals.Count);
            Assert.Empty(vm.Current.Trades);
            Assert.Single(vm.Analyses);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account.Id);
            await vm.LoadTask;
            Assert.Equal(scoped.Id, Assert.Single(vm.Current!.Journals).Source.JournalId);
            Assert.Empty(vm.Analyses); // Exact history cannot inherit the aggregate analysis.
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null);
            await vm.LoadTask;
            await database.Repository.DeleteAsync(new(scoped.Id, scoped.Revision));
            await vm.RefreshCommand.ExecuteAsync(null);
            Assert.Equal(global.Id, Assert.Single(vm.Current!.Journals).Source.JournalId);
            await vm.OpenAnalysisCommand.ExecuteAsync(vm.Analyses.Single());
            Assert.Equal(2, vm.Snapshot!.Evidence.Journals.Count);
            Assert.Contains(vm.Snapshot.Evidence.Journals, j => j.Source.Text == "Original scoped text" && j.Source.JournalId == scoped.Id);
            Assert.Contains("unknown", vm.Snapshot.Metadata);
            await vm.DeleteAnalysisCommand.ExecuteAsync(vm.Analyses.Single());
            Assert.Empty(vm.Analyses);
            Assert.Equal("All accounts reflection", (await database.Repository.GetAsync(date))!.Entry.Text);
            Assert.Single(await database.Repository.GetHistoryAsync(global.Id));
            await using var db = await database.ContextFactory.CreateDbContextAsync();
            Assert.Empty(await db.Trades.AsNoTracking().ToArrayAsync());
            Assert.Empty(await db.CoachingAnalyses.AsNoTracking().ToArrayAsync());
            Assert.Single(await db.DailyJournals.AsNoTracking().ToArrayAsync());
        }
        finally { vm.Deactivate(); await vm.LoadTask; }
    }
}
