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
            new WriteTradingRuleUseCase(services.GetRequiredService<ITradingRuleStore>(), TimeProvider.System), dialogs);
        var account = new TradingAccount("Synthetic Rules account", TradingAccountType.Personal, null, null, "USD", 0, DateTimeOffset.UtcNow.AddDays(-2));
        account.Deactivate(DateTimeOffset.UtcNow.AddDays(-1));
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        var vm = Create(db.Provider); await vm.ActivateAsync(); vm.CreateCommand.Execute(null);

        vm.Fields[0].Text = "Synthetic SQLite rule"; vm.Fields[1].Text = "Wait for recorded confirmation"; vm.Fields[2].Text = "Trade and execution evidence";
        await vm.SaveCommand.ExecuteAsync(null); Assert.False(vm.IsEditing); Assert.Equal("Rule saved.", vm.Message);
        Guid id = vm.SelectedRule!.Id;
        var competing = Create(db.Provider); await competing.ActivateAsync(); competing.TrySelect(competing.Rules.Single()); await competing.LoadTask;
        vm.EditCommand.Execute(null); competing.EditCommand.Execute(null);
        vm.Fields[1].Text = "Updated criterion"; await vm.SaveCommand.ExecuteAsync(null);
        competing.Fields[1].Text = "Stale local text"; await competing.SaveCommand.ExecuteAsync(null);
        Assert.True(competing.IsEditing); Assert.Equal("Stale local text", competing.Fields[1].Text); Assert.Contains("changed", competing.Message);

        vm.Deactivate(); competing.Deactivate();
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        await using var restarted = new ServiceCollection().AddSingleton(TimeProvider.System).AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        var fresh = Create(restarted); await fresh.ActivateAsync(); fresh.TrySelect(fresh.Rules.Single()); await fresh.LoadTask;
        Assert.Equal(id, fresh.SelectedRule!.Id); Assert.Equal("Updated criterion", fresh.SelectedRule!.Details.Revision.Definition.Criterion);
        Assert.True(fresh.SelectedRule.Details.Revision.Definition.IsGlobal);
        Assert.Null(fresh.SelectedRule.Details.Revision.Definition.AccountScope.AccountId);
        var internalHistory = await restarted.GetRequiredService<ITradingRuleReader>().ReadRevisionsAsync(id);
        Assert.Equal(new[] { 2, 1 }, internalHistory.Select(r => r.Revision.Revision));
        Assert.Equal("Wait for recorded confirmation", internalHistory.Last().Revision.Definition.Criterion);
        Assert.Empty(await context.TradingRuleAssessments.ToArrayAsync());
        await fresh.DeleteCommand.ExecuteAsync(fresh.SelectedRule);
        Assert.Empty(fresh.Rules);
        Assert.Equal(3, await context.TradingRuleRevisions.CountAsync());
    }
}
