using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class DailyReviewSqliteTests
{
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
