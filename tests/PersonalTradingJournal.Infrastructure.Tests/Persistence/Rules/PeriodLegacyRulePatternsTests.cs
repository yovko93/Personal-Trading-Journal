using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    [Fact]
    public async Task PeriodPatternsDoNotPromoteMigratedScopedRulesIntoCurrentGlobalCoverage()
    {
        await using var f = await Fixture.Create("20261012194059_AddDeterministicRuleChecks");
        var trade = await f.Seed(); var id = Guid.NewGuid(); var assessment = Guid.NewGuid();
        string targetKey = $"trade:{trade.Id:D}:{trade.TradingAccountId:D}";
        await using (var db = f.Context())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO TradingRuleRevisions
                (RuleId,Revision,ContractVersion,Name,Criterion,RequiredEvidence,Applicability,ScopeKind,AccountId,AccountNameAtRevision,EvaluationMode,IsActive,RecordedAtUtc)
                VALUES ({id},1,1,'Legacy scope','Original meaning','Recorded evidence',1,2,{trade.TradingAccountId},'Synthetic',1,1,{Now.UtcDateTime});
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO TradingRuleAssessments
                (Id,RuleId,RuleRevision,Sequence,TargetKey,TargetKind,TradeId,ScopeKind,AccountId,AccountNameAtAssessment,TargetFingerprint,Outcome,Source,Basis,EvidenceSufficiency,Explanation,EvidenceJson,RecordedAtUtc)
                VALUES ({assessment},{id},1,1,{targetKey},1,{trade.Id},2,{trade.TradingAccountId},'Synthetic','synthetic',3,1,1,2,'Missing evidence','[]',{Now.UtcDateTime});
                """);
            await db.Database.MigrateAsync();
        }
        await f.Restart();
        var query = new PeriodReviewQuery(ReviewPeriodKind.Month, DateOnly.FromDateTime(Now.AddDays(-1).UtcDateTime));
        var evidence = (await f.Get<IPeriodReviewEvidenceReader>().ReadAsync(query)).Evidence!;
        Assert.Empty(evidence.ApplicableRules);
        var p = PeriodReviewPatternsCalculator.Calculate(evidence).Patterns!;
        var row = Assert.Single(p.Rules);
        Assert.Equal(PeriodRuleVersionState.LegacyScoped, row.VersionState);
        Assert.Equal(PeriodRuleDenominator.RecordedTargetsOnly, row.Denominator);
        Assert.Equal(1, row.CannotEvaluate); Assert.Equal(0, row.NotAssessed);
        Assert.Equal(trade.TradingAccountId, row.Scope.AccountId);
        Assert.Equal(assessment, row.Targets[0].Latest!.Assessment.Id);
        Assert.False(row.Targets[0].HasAutomaticCapability);
        var before = System.Text.Json.JsonSerializer.Serialize(p.Rules);
        await f.Restart();
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(PeriodReviewPatternsCalculator.Calculate(
            (await f.Get<IPeriodReviewEvidenceReader>().ReadAsync(query)).Evidence!).Patterns!.Rules));
    }
}
