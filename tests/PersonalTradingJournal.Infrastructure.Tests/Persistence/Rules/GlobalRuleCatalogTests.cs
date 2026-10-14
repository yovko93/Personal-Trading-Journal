using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    [Fact]
    public async Task OptionalCriterionMigrationKeepsHistoricalTextAssessmentsAndTypedChecks()
    {
        await using var f = await Fixture.Create("20261012214801_OptionalGlobalRuleEvidence");
        var trade = await f.Seed(); var rule = await f.Rule(AutomaticDefinition());
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var run = await f.Get<IAutomaticRuleChecks>().RunAsync(target);
        var assessmentId = Assert.Single(run.AssessmentIds);
        var before = (await f.Reader.ReadAssessmentAsync(assessmentId))!;
        await using (var db = f.Context()) await db.Database.MigrateAsync();
        await f.Restart();
        var after = (await f.Reader.ReadAssessmentAsync(assessmentId))!;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Assessment), System.Text.Json.JsonSerializer.Serialize(after.Assessment));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Evaluation), System.Text.Json.JsonSerializer.Serialize(after.Evaluation));
        var revised = rule.Revise(1, new(rule.Definition.Name, "", rule.Definition.RequiredEvidence, rule.Definition.AutomaticCriterion), Now);
        Assert.True(await f.Rules.TryAppendAsync(revised, 1));
        var manual = await f.Rule(new TradingRuleDefinition("Rule without Criterion"));
        await f.Restart();
        Assert.Empty((await f.Rules.GetLatestAsync(manual.RuleId))!.Definition.Criterion);
        var history = await f.Reader.ReadRevisionsAsync(rule.RuleId);
        Assert.Equal(new[] { "", rule.Definition.Criterion }, history.Select(r => r.Revision.Definition.Criterion));
        Assert.All(history, r => Assert.Equal(rule.Definition.AutomaticCriterion, r.Revision.Definition.AutomaticCriterion));
        Assert.Equal(rule.Definition.Criterion, (await f.Reader.ReadAssessmentAsync(assessmentId))!.Assessment.RuleRevision.Definition.Criterion);
        var newRun = await f.Get<IAutomaticRuleChecks>().RunAsync(target);
        Assert.Single(newRun.AssessmentIds);
        Assert.Equal(before.Evaluation!.Calculation, (await f.Reader.ReadAssessmentAsync(newRun.AssessmentIds[0]))!.Evaluation!.Calculation);
        await using var check = f.Context();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => check.Database.ExecuteSqlRawAsync("UPDATE TradingRuleRevisions SET Criterion = 'changed'"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => check.Database.ExecuteSqlRawAsync("DELETE FROM TradingRuleAssessments"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => check.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261012214801_OptionalGlobalRuleEvidence"));
        Assert.Equal(3, await check.TradingRuleRevisions.CountAsync()); Assert.Equal(2, await check.TradingRuleAssessments.CountAsync());
    }

    [Fact]
    public async Task OptionalEvidenceMigrationPreservesTypedRulesAssessmentsAndAllImmutableGuards()
    {
        await using var f = await Fixture.Create("20261012205801_GlobalTradingRuleCatalog");
        var trade = await f.Seed(); var rule = await f.Rule(AutomaticDefinition());
        var run = await f.Get<IAutomaticRuleChecks>().RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
        var assessmentId = Assert.Single(run.AssessmentIds);
        var before = (await f.Reader.ReadAssessmentAsync(assessmentId))!;
        await using (var db = f.Context()) await db.Database.MigrateAsync();
        await f.Restart();
        var after = (await f.Reader.ReadAssessmentAsync(assessmentId))!;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Assessment), System.Text.Json.JsonSerializer.Serialize(after.Assessment));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Evaluation), System.Text.Json.JsonSerializer.Serialize(after.Evaluation));
        var manual = await f.Rule(new TradingRuleDefinition("General principle", "Wait for confirmation"));
        Assert.Empty((await f.Rules.GetLatestAsync(manual.RuleId))!.Definition.RequiredEvidence);
        Assert.Equal(1, (await f.Get<IAutomaticRuleChecks>().RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId))).Unchanged);
        await using var check = f.Context();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => check.Database.ExecuteSqlRawAsync("UPDATE TradingRuleRevisions SET Criterion = 'changed'"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => check.Database.ExecuteSqlRawAsync("DELETE FROM TradingRuleRevisions"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => check.Database.ExecuteSqlRawAsync("DELETE FROM TradingRuleAssessments"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => check.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261012205801_GlobalTradingRuleCatalog"));
        Assert.Equal(2, await check.TradingRuleRevisions.CountAsync());
        Assert.Single(await check.TradingRuleAssessments.ToArrayAsync());
    }

    [Fact]
    public async Task MigrationRetainsScopedMeaningAndAssessmentsWithoutPromotingLegacyRules()
    {
        await using var f = await Fixture.Create("20261012194059_AddDeterministicRuleChecks");
        var trade = await f.Seed(); var id = Guid.NewGuid(); var assessment = Guid.NewGuid();
        string targetKey = $"trade:{trade.Id:D}:{trade.TradingAccountId:D}";
        await using (var db = f.Context())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO TradingRuleRevisions
                (RuleId,Revision,ContractVersion,Name,Criterion,RequiredEvidence,Applicability,ScopeKind,AccountId,AccountNameAtRevision,EvaluationMode,IsActive,RecordedAtUtc)
                VALUES ({id},1,1,'Legacy scoped principle','Original scoped statement','Required facts',1,2,{trade.TradingAccountId},'Synthetic',1,1,{Now.UtcDateTime});
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO TradingRuleAssessments
                (Id,RuleId,RuleRevision,Sequence,TargetKey,TargetKind,TradeId,ScopeKind,AccountId,AccountNameAtAssessment,TargetFingerprint,Outcome,Source,Basis,EvidenceSufficiency,Explanation,EvidenceJson,RecordedAtUtc)
                VALUES ({assessment},{id},1,1,{targetKey},1,{trade.Id},2,{trade.TradingAccountId},'Synthetic','synthetic',3,1,1,2,'Missing recorded evidence','[]',{Now.UtcDateTime});
                """);
            await db.Database.MigrateAsync();
        }
        await f.Restart();
        var legacy = (await f.Rules.GetLatestAsync(id))!;
        Assert.False(legacy.Definition.IsGlobal);
        Assert.Equal(trade.TradingAccountId, legacy.Definition.AccountScope.AccountId);
        Assert.Empty((await f.Reader.ReadCatalogAsync()).Items);
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var history = await f.Get<IManualRuleAssessmentReader>().ReadRulesAsync(target);
        Assert.Equal(id, Assert.Single(history.Items).Revision.RuleId);
        Assert.Equal("Original scoped statement", (await f.Reader.ReadAssessmentAsync(assessment))!.Assessment.RuleRevision.Definition.Criterion);
        Assert.Empty((await f.Get<IAutomaticRuleChecks>().RunAsync(target)).AssessmentIds);
        Assert.False(await f.Rules.TryAppendAsync(legacy.Revise(1, Definition(), Now), 1));
        Assert.Equal(TradingRuleWriteStatus.InvalidState, (await f.Get<WriteTradingRuleUseCase>().ExecuteAsync(new(id, 1, TradingRuleAuthoringAction.Revise, Definition()))).Status);
        await using var check = f.Context();
        Assert.Single(await check.TradingRuleRevisions.ToArrayAsync());
        Assert.Single(await check.TradingRuleAssessments.ToArrayAsync());
    }

    [Fact]
    public async Task GlobalChecksSelectCapabilityAcrossAccountsWhileAssessmentScopesStayExact()
    {
        await using var f = await Fixture.Create(); var first = await f.Seed(); var other = await f.Seed("Other");
        var tradeRule = await f.Rule(AutomaticDefinition());
        var dayRule = await f.Rule(AutomaticDefinition(target: TradingRuleApplicability.NewYorkDay));
        foreach (var trade in new[] { first, other })
        {
            var run = await f.Get<IAutomaticRuleChecks>().RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
            var saved = (await f.Reader.ReadAssessmentAsync(Assert.Single(run.AssessmentIds)))!;
            Assert.Equal(tradeRule.RuleId, saved.Assessment.RuleRevision.RuleId);
            Assert.Equal(trade.TradingAccountId, saved.Assessment.Target.AccountScope.AccountId);
        }
        var day = TradingRuleAssessmentTarget.ForNewYorkDay(new(2026, 11, 1), TradingRuleAccountScope.AllAccounts);
        var daily = await f.Get<IAutomaticRuleChecks>().RunAsync(day);
        Assert.Equal(dayRule.RuleId, (await f.Reader.ReadAssessmentAsync(Assert.Single(daily.AssessmentIds)))!.Assessment.RuleRevision.RuleId);
        Assert.Equal(2, (await f.Reader.ReadCatalogAsync()).TotalCount);
    }

    [Fact]
    public async Task DeleteRacesWithAssessmentWithoutLosingHistoryAndStaleDeleteWritesNothing()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var first = Assess(rule, target); Assert.True(await f.Assessments.TryAppendAsync(first, 1, null));
        var updated = rule.Revise(1, Definition(name: "Updated"), Now); Assert.True(await f.Rules.TryAppendAsync(updated, 1));
        var writer = f.Get<WriteTradingRuleUseCase>();
        Assert.Equal(TradingRuleWriteStatus.Conflict, (await writer.ExecuteAsync(new(rule.RuleId, 1, TradingRuleAuthoringAction.Delete, null))).Status);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleting = Task.Run(async () => { await gate.Task; return await writer.ExecuteAsync(new(rule.RuleId, 2, TradingRuleAuthoringAction.Delete, null)); });
        var appending = Task.Run(async () => { await gate.Task; return await f.Assessments.TryAppendAsync(Assess(updated, target, first.Id), 2, first.Id); });
        gate.SetResult(); await Task.WhenAll(deleting, appending);
        Assert.Equal(TradingRuleWriteStatus.Saved, (await deleting).Status);
        Assert.Empty((await f.Reader.ReadCatalogAsync()).Items);
        await f.Restart();
        var history = await f.Reader.ReadAssessmentsAsync(rule.RuleId, target);
        Assert.Equal(await appending ? 2 : 1, history.Count);
        Assert.Equal("Synthetic NQ rule", history.Last().Assessment.RuleRevision.Definition.Name);
        Assert.False(await f.Rules.TryAppendAsync((await f.Rules.GetLatestAsync(rule.RuleId))!.Reopen(3, Now), 3));
        await using var db = f.Context(); Assert.Equal(3, await db.TradingRuleRevisions.CountAsync());
    }
}
