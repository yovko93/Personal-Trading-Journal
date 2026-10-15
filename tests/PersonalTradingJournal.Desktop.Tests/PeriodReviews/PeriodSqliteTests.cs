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

namespace PersonalTradingJournal.Desktop.Tests.PeriodReviews;

public sealed class PeriodSqliteTests
{
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
