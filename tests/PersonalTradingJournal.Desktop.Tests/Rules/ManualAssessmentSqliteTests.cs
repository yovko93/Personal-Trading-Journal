using Microsoft.EntityFrameworkCore;
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

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class ManualAssessmentSqliteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealDayWorkflowSavesAllOutcomesCorrectionsExactScopeAndRestart(bool aggregate)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var account = new TradingAccount("Synthetic assessment account", TradingAccountType.Personal, null, null, "USD", 0, DateTimeOffset.UtcNow.AddDays(-2));
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        var scope = aggregate ? TradingRuleAccountScope.AllAccounts : TradingRuleAccountScope.ForAccount(account.Id);
        var date = new DateOnly(2026, 11, 1);
        var journal = (await db.Repository.CreateAsync(new(date, aggregate ? null : account.Id, "Synthetic user observation"))).Journal!.Entry;
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Review reflection", "Evaluate the recorded observation", "A saved daily Journal"), DateTimeOffset.UtcNow.AddDays(-1));
        Assert.True(await db.Provider.GetRequiredService<ITradingRuleStore>().TryAppendAsync(rule, 0));
        RuleAssessmentViewModel Create(IServiceProvider services) => new(services.GetRequiredService<IManualRuleAssessmentReader>(), services.GetRequiredService<ITradingRuleReader>(),
            services.GetRequiredService<ITradingRuleStore>(), services.GetRequiredService<ITradingRuleAssessmentStore>(),
            new(services.GetRequiredService<ITradingRuleStore>(), services.GetRequiredService<ITradingRuleAssessmentStore>(), TimeProvider.System), new FakeDialogService { ConfirmationResult = true });
        var target = TradingRuleAssessmentTarget.ForNewYorkDay(date, scope); var vm = Create(db.Provider); await vm.OpenAsync(target);
        Assert.True(vm.CanRecord); Assert.Contains(aggregate ? "aggregate" : account.Name, vm.TargetLabel);
        await vm.EvidenceKindCommand.ExecuteAsync(vm.Kinds.Single(k => k.Value == TradingRuleEvidenceKind.Journal));
        Assert.Equal(journal.Id, Assert.Single(vm.Evidence).Option.Reference.SourceId);
        foreach (var outcome in new[] { TradingRuleAssessmentOutcome.CannotEvaluate, TradingRuleAssessmentOutcome.NoViolation, TradingRuleAssessmentOutcome.ConfirmedViolation })
        {
            vm.Explanation = "Synthetic evaluation of recorded reflection"; vm.Outcome = outcome;
            vm.Evidence[0].Selected = true; vm.Sufficient = outcome != TradingRuleAssessmentOutcome.CannotEvaluate;
            await vm.SaveCommand.ExecuteAsync(null); Assert.Contains("saved and verified", vm.Message);
        }
        Assert.Equal(3, vm.History.Count); Assert.Equal(1, vm.History[0].Details.Assessment.RuleRevision.Revision);
        var stale = Create(db.Provider); await stale.OpenAsync(target); stale.Explanation = "Retain local correction";
        vm.Explanation = "Newer correction"; await vm.SaveCommand.ExecuteAsync(null); await stale.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Retain local correction", stale.Explanation); Assert.Contains("changed", stale.Message);
        await using var context = await db.ContextFactory.CreateDbContextAsync(); Assert.Equal(4, await context.TradingRuleAssessments.CountAsync());
        var root = Path.GetDirectoryName(Path.GetDirectoryName(context.Database.GetDbConnection().DataSource))!;
        await using var restart = new ServiceCollection().AddPersistence(new LocalApplicationPaths(root)).BuildServiceProvider();
        var fresh = Create(restart); await fresh.OpenAsync(null, rule.RuleId);
        Assert.Equal(4, fresh.History.Count); Assert.False(fresh.HasTarget);
        await fresh.OpenTargetCommand.ExecuteAsync(fresh.History[0]); Assert.Equal(target, fresh.Target);
        Assert.Equal(scope.Kind, fresh.Target!.AccountScope.Kind); Assert.Equal("Newer correction", fresh.History[0].Details.Assessment.Explanation);
        vm.Deactivate(); stale.Deactivate(); fresh.Deactivate();
    }
}
