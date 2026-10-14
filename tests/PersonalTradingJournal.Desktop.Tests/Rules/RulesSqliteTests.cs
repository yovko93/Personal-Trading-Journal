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
    public async Task EmptyCriterionCreateClearAndFreshServicesReloadKeepPriorTextAndTypedCondition()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        RulesViewModel Create(IServiceProvider services) => new(services.GetRequiredService<ITradingRuleReader>(),
            services.GetRequiredService<ITradingRuleStore>(), new(services.GetRequiredService<ITradingRuleStore>(), TimeProvider.System), new FakeDialogService());
        var vm = Create(db.Provider); await vm.ActivateAsync(); vm.CreateCommand.Execute(null);
        vm.Fields[0].Text = "Required Rule\nwith two lines";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.IsEditing); Assert.Equal("Rule saved.", vm.Message);
        var emptyId = vm.SelectedRule!.Id; Assert.False(Assert.Single(vm.Rules).HasStatement);
        var store = db.Provider.GetRequiredService<ITradingRuleStore>();
        var typed = TradingRuleRevision.Create(Guid.NewGuid(), new("Typed rule", "Keep historical prose", "Preserve evidence",
            new(RuleProfitBasis.StrictNet, 25m, "EUR")), DateTimeOffset.UtcNow.AddDays(-1));
        Assert.True(await store.TryAppendAsync(typed, 0));
        await vm.RefreshCommand.ExecuteAsync(null); await vm.EditRowCommand.ExecuteAsync(vm.Rules.Single(r => r.Id == typed.RuleId));
        vm.Fields[1].Text = ""; await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.IsEditing); Assert.Equal(typed.Definition.AutomaticCriterion, vm.SelectedRule!.Details.Revision.Definition.AutomaticCriterion);
        var rowsBeforeMove = vm.Rules.ToArray(); var listBeforeMove = vm.Rules;
        await vm.MoveBeforeAsync(typed.RuleId, emptyId);
        Assert.Same(listBeforeMove, vm.Rules);
        Assert.All(rowsBeforeMove, row => Assert.Same(row, vm.Rules.Single(r => r.Id == row.Id)));
        Assert.Equal(new[] { typed.RuleId, emptyId }, vm.Rules.Select(r => r.Id));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(new[] { typed.RuleId, emptyId }, vm.Rules.Select(r => r.Id));
        vm.Deactivate();
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        await using var restarted = new ServiceCollection().AddSingleton(TimeProvider.System).AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        var fresh = Create(restarted); await fresh.ActivateAsync();
        Assert.Equal(new[] { typed.RuleId, emptyId }, fresh.Rules.Select(r => r.Id));
        Assert.Equal(new[] { 1, 2 }, fresh.Rules.Select(r => r.DisplayNumber));
        Assert.All(fresh.Rules, r => { Assert.Empty(r.Statement); Assert.False(r.HasStatement); });
        await fresh.EditRowCommand.ExecuteAsync(fresh.Rules.Single(r => r.Id == emptyId));
        Assert.Equal("Required Rule\nwith two lines", fresh.Fields[0].Text); Assert.Empty(fresh.Fields[1].Text);
        var history = await restarted.GetRequiredService<ITradingRuleReader>().ReadRevisionsAsync(typed.RuleId);
        Assert.Equal(new[] { "", "Keep historical prose" }, history.Select(r => r.Revision.Definition.Criterion));
        Assert.All(history, r => { Assert.Equal(typed.Definition.AutomaticCriterion, r.Revision.Definition.AutomaticCriterion); Assert.Equal("Preserve evidence", r.Revision.Definition.RequiredEvidence); });
        fresh.Deactivate();
    }

    [Fact]
    public async Task ProseEditRetainsExactTypedCriterionEvidenceAndHistoryAfterFreshServices()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var store = db.Provider.GetRequiredService<ITradingRuleStore>();
        var criterion = new AutomaticRuleCriterion(RuleProfitBasis.StrictNet, 123.456m, "EUR", capability: RuleCheckCapability.ClosedDayLoss);
        var original = TradingRuleRevision.Create(Guid.NewGuid(), new("Existing typed rule", "Original statement", "Original required facts", criterion), DateTimeOffset.UtcNow.AddDays(-1));
        Assert.True(await store.TryAppendAsync(original, 0));
        var vm = new RulesViewModel(db.Provider.GetRequiredService<ITradingRuleReader>(), store, new(store, TimeProvider.System), new FakeDialogService());
        await vm.ActivateAsync(); await vm.EditRowCommand.ExecuteAsync(Assert.Single(vm.Rules));
        Assert.Equal(2, vm.Fields.Count); vm.Fields[1].Text = "Revised prose";
        await vm.SaveCommand.ExecuteAsync(null); Assert.False(vm.IsEditing);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        await using var restarted = new ServiceCollection().AddSingleton(TimeProvider.System).AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        var history = await restarted.GetRequiredService<ITradingRuleReader>().ReadRevisionsAsync(original.RuleId);
        Assert.Equal(new[] { 2, 1 }, history.Select(r => r.Revision.Revision));
        Assert.All(history, r => { Assert.Equal(criterion, r.Revision.Definition.AutomaticCriterion); Assert.Equal("Original required facts", r.Revision.Definition.RequiredEvidence); });
        Assert.Equal("Original statement", history[1].Revision.Definition.Criterion);
        Assert.Equal("Revised prose", history[0].Revision.Definition.Criterion);
    }

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

        vm.Fields[0].Text = "Synthetic SQLite rule"; vm.Fields[1].Text = "Wait for recorded confirmation";
        await vm.SaveCommand.ExecuteAsync(null); Assert.False(vm.IsEditing); Assert.Equal("Rule saved.", vm.Message);
        Guid id = vm.SelectedRule!.Id;
        Assert.Empty(vm.SelectedRule.Details.Revision.Definition.RequiredEvidence);
        Assert.Null(vm.SelectedRule.Details.Revision.Definition.AutomaticCriterion);
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
