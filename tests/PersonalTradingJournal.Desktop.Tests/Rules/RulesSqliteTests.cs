using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class RulesSqliteTests
{
    [Fact]
    public async Task RealSqliteCreateReviseConflictArchiveReopenAndFreshServicesReadExactHistory()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var dialogs = new FakeDialogService { ConfirmationResult = true };
        RulesViewModel Create(IServiceProvider services) => new(services.GetRequiredService<ITradingRuleReader>(), services.GetRequiredService<ITradingRuleStore>(),
            services.GetRequiredService<ITradingAccountReader>(), new WriteTradingRuleUseCase(services.GetRequiredService<ITradingRuleStore>(), TimeProvider.System), dialogs);
        var account = new TradingAccount("Synthetic Rules account", TradingAccountType.Personal, null, null, "USD", 0, DateTimeOffset.UtcNow.AddDays(-2));
        account.Deactivate(DateTimeOffset.UtcNow.AddDays(-1));
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        var vm = Create(db.Provider); await vm.ActivateAsync(); vm.CreateCommand.Execute(null);
        vm.Account = vm.Accounts.Single(a => a.Id == account.Id); vm.Target = TradingRuleApplicability.NewYorkDay;
        vm.Fields[0].Text = "Synthetic SQLite rule"; vm.Fields[1].Text = "Wait for recorded confirmation"; vm.Fields[2].Text = "Trade and execution evidence";
        await vm.SaveCommand.ExecuteAsync(null); Assert.False(vm.IsEditing); Assert.Contains("Saved and verified revision 1", vm.Message);
        Guid id = vm.SelectedRule!.Id;
        var competing = Create(db.Provider); await competing.ActivateAsync(); competing.TrySelect(competing.Rules.Single()); await competing.LoadTask;
        vm.EditCommand.Execute(null); competing.EditCommand.Execute(null);
        vm.Fields[1].Text = "Updated criterion"; await vm.SaveCommand.ExecuteAsync(null);
        competing.Fields[1].Text = "Stale local text"; await competing.SaveCommand.ExecuteAsync(null);
        Assert.True(competing.IsEditing); Assert.Equal("Stale local text", competing.Fields[1].Text); Assert.Contains("changed", competing.Message);
        await vm.ArchiveCommand.ExecuteAsync(null); Assert.False(vm.SelectedRule.Details.Revision.IsActive);
        await vm.ArchiveCommand.ExecuteAsync(null); Assert.Equal(4, vm.SelectedRule.Details.Revision.Revision);
        vm.Deactivate(); competing.Deactivate();
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        await using var restarted = new ServiceCollection().AddSingleton(TimeProvider.System).AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        var fresh = Create(restarted); await fresh.ActivateAsync(); fresh.TrySelect(fresh.Rules.Single()); await fresh.LoadTask;
        Assert.Equal(id, fresh.SelectedRule!.Id); Assert.Equal("Updated criterion", fresh.Displayed!.Details.Revision.Definition.Criterion);
        Assert.Equal(account.Id, fresh.Displayed.Details.Revision.Definition.AccountScope.AccountId);
        Assert.Equal(TradingRuleApplicability.NewYorkDay, fresh.Displayed.Details.Revision.Definition.Applicability);
        Assert.Equal(new[] { 4, 3, 2, 1 }, fresh.Revisions.Select(r => r.Details.Revision.Revision));
        fresh.ViewRevisionCommand.Execute(fresh.Revisions.Last()); Assert.Equal("Wait for recorded confirmation", fresh.Displayed!.Details.Revision.Definition.Criterion);
        Assert.Empty(await context.TradingRuleAssessments.ToArrayAsync());
    }
}
