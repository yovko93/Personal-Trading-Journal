using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Rules;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private const string PreviousSchema = "20261011220636_AllowUnreferencedPlaybookRevisionDeletion";
    private static TradingRuleDefinition Definition(Guid? account = null, TradingRuleApplicability applicability = TradingRuleApplicability.Trade,
        string name = "Synthetic NQ rule") => new(name, "Wait for documented confirmation", "Executions and user observations");
    private static TradingRuleAssessment Assess(TradingRuleRevision rule, TradingRuleAssessmentTarget target,
        Guid? prior = null, params TradingRuleEvidenceReference[] sources) => TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
            TradingRuleAssessmentOutcome.CannotEvaluate, TradingRuleAssessmentSource.Manual, TradingRuleAssessmentBasis.RecordedEvidence,
            TradingRuleEvidenceSufficiency.Missing, "Recorded commission is unknown; no inference about compliance.", sources, prior, Now.AddDays(1));

    [Fact]
    public async Task PreviousSchemaUpgradePreservesTradeEconomicsAndAddsEmptyRulesWithoutBackfill()
    {
        await using var f = await Fixture.Create(PreviousSchema); var trade = await f.Seed();
        await using (var db = f.Context())
        {
            Assert.DoesNotContain("20261012163222_AddTradingRules", await db.Database.GetAppliedMigrationsAsync());
            await db.Database.MigrateAsync();
            Assert.Empty(await db.TradingRuleRevisions.ToArrayAsync()); Assert.Empty(await db.TradingRuleAssessments.ToArrayAsync());
            var execution = await db.TradeExecutions.OrderBy(e => e.Sequence).FirstAsync();
            Assert.Null(execution.Commission); Assert.Equal(trade.Id, execution.TradeId);
            Assert.Equal("20261020180223_AddNotebookOwnedImageFiles", (await db.Database.GetAppliedMigrationsAsync()).Last());
        }
        Assert.Equal(TradingRuleWriteStatus.Saved, (await f.Get<WriteTradingRuleUseCase>().ExecuteAsync(
            new(Guid.NewGuid(), 0, TradingRuleAuthoringAction.Create, Definition(trade.TradingAccountId)))).Status);
    }

    [Fact]
    public async Task RestartRetainsEveryFieldExactRuleRevisionAndAppendOnlyAssessmentTransitions()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed();
        var rule = await f.Rule(Definition(trade.TradingAccountId));
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var first = Assess(rule, target, null, new(TradingRuleEvidenceKind.Trade, trade.Id),
            new(TradingRuleEvidenceKind.Execution, trade.Executions[0].Id));
        Assert.True(await f.Assessments.TryAppendAsync(first, 1, null));
        var second = TradingRuleAssessment.Record(Guid.NewGuid(), rule, target, TradingRuleAssessmentOutcome.ConfirmedViolation,
            TradingRuleAssessmentSource.Manual, TradingRuleAssessmentBasis.UserEnteredViolation, TradingRuleEvidenceSufficiency.Missing,
            "Explicit synthetic user observation", [], first.Id, Now.AddDays(1));
        Assert.True(await f.Assessments.TryAppendAsync(second, 1, first.Id));
        var revised = rule.Revise(1, Definition(trade.TradingAccountId, name: "Renamed rule"), Now.AddMinutes(1));
        Assert.True(await f.Rules.TryAppendAsync(revised, 1));
        Assert.True(await f.Rules.TryAppendAsync(revised.Archive(2, Now.AddMinutes(2)), 2));
        await f.Restart();
        var history = await f.Reader.ReadAssessmentsAsync(rule.RuleId, target);
        Assert.Equal(new[] { 2, 1 }, history.Select(a => a.Sequence));
        Assert.Equal(first.Id, history[0].Assessment.SupersedesAssessmentId);
        Assert.All(history, a => Assert.Equal(1, a.Assessment.RuleRevision.Revision));
        Assert.Equal("Synthetic NQ rule", history[0].Assessment.RuleRevision.Definition.Name);
        Assert.Equal(first.Explanation, history[1].Assessment.Explanation);
        Assert.Equal(first.RecordedAtUtc, history[1].Assessment.RecordedAtUtc);
        Assert.Equal(2, history[1].Evidence.Count);
        Assert.All(history[1].Evidence, s => { Assert.Equal("USD", s.Currency); Assert.Equal(TradingRuleSourceState.Available, s.State); });
        Assert.False((await f.Rules.GetLatestAsync(rule.RuleId))!.IsActive);
        var current = (await f.Rules.GetLatestAsync(rule.RuleId))!;
        Assert.False(await f.Rules.TryAppendAsync(current.Reopen(3, Now.AddMinutes(3)), 3));
        Assert.Equal(new[] { 3, 2, 1 }, (await f.Reader.ReadRevisionsAsync(rule.RuleId)).Select(r => r.Revision.Revision));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentWritersCommitOnlyOneRevisionOrAssessment(bool assessment)
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> Write() => Task.Run(async () =>
        {
            await start.Task;
            return assessment ? await f.Assessments.TryAppendAsync(Assess(rule,
                TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId)), 1, null)
                : await f.Rules.TryAppendAsync(rule.Revise(1, Definition(name: Guid.NewGuid().ToString()), Now.AddMinutes(1)), 1);
        });
        var a = Write(); var b = Write(); start.SetResult();
        bool[] results = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Single(results, x => x); Assert.Single(results, x => !x);
        await using var db = f.Context();
        Assert.Equal(assessment ? 1 : 2, await db.TradingRuleRevisions.CountAsync());
        Assert.Equal(assessment ? 1 : 0, await db.TradingRuleAssessments.CountAsync());
    }

    [Fact]
    public async Task StaleRuleAssessmentPriorAndForgedDefinitionAreRejectedWithoutWrites()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var first = Assess(rule, target); Assert.True(await f.Assessments.TryAppendAsync(first, 1, null));
        Assert.False(await f.Assessments.TryAppendAsync(Assess(rule, target), 1, null));
        var forged = TradingRuleRevision.Rehydrate(rule.RuleId, 1, Definition(name: "forged"), true, rule.RecordedAtUtc);
        Assert.False(await f.Assessments.TryAppendAsync(Assess(forged, target, first.Id), 1, first.Id));
        Assert.True(await f.Rules.TryAppendAsync(rule.Revise(1, Definition(name: "new"), Now), 1));
        Assert.False(await f.Assessments.TryAppendAsync(Assess(rule, target, first.Id), 1, first.Id));
        Assert.False(await f.Rules.TryAppendAsync(rule.Revise(1, Definition(), Now), 1));
        await using var db = f.Context(); Assert.Equal(2, await db.TradingRuleRevisions.CountAsync()); Assert.Single(await db.TradingRuleAssessments.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterInsertRollsBackRuleOrAssessmentAndDoesNotConsumeNumber(bool assessment)
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        using var cancel = new CancellationTokenSource();
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite($"Data Source={f.Paths.DatabasePath};Foreign Keys=True")
            .AddInterceptors(new CancelSaved(cancel)).Options;
        var store = new TradingRuleRepository(new Factory(options));
        var item = Assess(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => assessment
            ? store.TryAppendAsync(item, 1, null, cancel.Token)
            : store.TryAppendAsync(rule.Revise(1, Definition(), Now), 1, cancel.Token));
        await using var db = f.Context(); Assert.Single(await db.TradingRuleRevisions.ToArrayAsync()); Assert.Empty(await db.TradingRuleAssessments.ToArrayAsync());
        Assert.True(assessment ? await f.Assessments.TryAppendAsync(item, 1, null) : await f.Rules.TryAppendAsync(rule.Revise(1, Definition(), Now), 1));
    }

    [Fact]
    public async Task StorageFailureRollsBackAndSqlRejectsMutationDeletionAndNotAssessed()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition());
        await using var db = f.Context();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER SyntheticRuleFailure AFTER INSERT ON TradingRuleAssessments BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        var assessment = Assess(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Assessments.TryAppendAsync(assessment, 1, null));
        Assert.Empty(await db.TradingRuleAssessments.ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER SyntheticRuleFailure;");
        Assert.True(await f.Assessments.TryAppendAsync(assessment, 1, null));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE TradingRuleRevisions SET Name='changed';"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM TradingRuleRevisions;"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE TradingRuleAssessments SET Outcome=0;"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM TradingRuleAssessments;"));
        var invalid = await db.TradingRuleAssessments.AsNoTracking().SingleAsync(); invalid.Id = Guid.NewGuid(); invalid.Sequence = 2;
        invalid.SupersedesAssessmentId = assessment.Id; invalid.Outcome = 0;
        db.TradingRuleAssessments.Add(invalid);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Single(await db.TradingRuleAssessments.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task PagingIsBoundedStableAndScopeFiltersDoNotMergeAllAccountsWithExactScopes()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var other = await f.Seed("Other", "EUR");
        for (int i = 0; i < 11; i++) await f.Rule(Definition(i % 2 == 0 ? trade.TradingAccountId : null, name: "Rule " + i));
        await f.Rule(Definition(other.TradingAccountId));
        var first = await f.Reader.ReadCurrentAsync(pageSize: 10);
        var second = await f.Reader.ReadCurrentAsync(page: 2, pageSize: 10);
        Assert.Equal(12, first.TotalCount); Assert.Equal(10, first.Items.Count); Assert.Equal(2, second.Items.Count);
        Assert.Equal(12, first.Items.Concat(second.Items).Select(r => r.Revision.RuleId).Distinct().Count());
        Assert.Equal(first.Items.Select(r => r.Revision.RuleId), (await f.Reader.ReadCurrentAsync(pageSize: 10)).Items.Select(r => r.Revision.RuleId));
        Assert.Equal(12, (await f.Reader.ReadCatalogAsync()).TotalCount);
        Assert.Equal(0, (await f.Reader.ReadCurrentAsync(TradingRuleAccountScope.ForAccount(trade.TradingAccountId))).TotalCount);
        var rule = first.Items[0].Revision;
        for (int i = 1; i < 4; i++) { var next = rule.Revise(rule.Revision, rule.Definition, Now); Assert.True(await f.Rules.TryAppendAsync(next, rule.Revision)); rule = next; }
        Assert.Equal(new[] { 4, 3 }, (await f.Reader.ReadRevisionsAsync(rule.RuleId, limit: 2)).Select(r => r.Revision.Revision));
        Assert.Equal(new[] { 2, 1 }, (await f.Reader.ReadRevisionsAsync(rule.RuleId, beforeRevision: 3, limit: 2)).Select(r => r.Revision.Revision));
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        rule = await f.Rule(Definition()); Guid? prior = null;
        for (int i = 0; i < 4; i++) { var a = Assess(rule, target, prior); Assert.True(await f.Assessments.TryAppendAsync(a, 1, prior)); prior = a.Id; }
        Assert.Equal(new[] { 4, 3 }, (await f.Reader.ReadAssessmentsAsync(rule.RuleId, target, limit: 2)).Select(a => a.Sequence));
        Assert.Equal(new[] { 2, 1 }, (await f.Reader.ReadAssessmentsAsync(rule.RuleId, target, beforeSequence: 3, limit: 2)).Select(a => a.Sequence));
        Assert.Empty(await f.Reader.ReadAssessmentsAsync(rule.RuleId, TradingRuleAssessmentTarget.ForTrade(other.Id, other.TradingAccountId)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => f.Reader.ReadCurrentAsync(pageSize: 101));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Reader.ReadCurrentAsync(cancellationToken: new(true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleAndBulkTradeDeletionAndAccountDeletionRetainHistoricalIdentity(bool bulk)
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed(); var rule = await f.Rule(Definition(trade.TradingAccountId));
        var target = TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId);
        var assessment = Assess(rule, target, null, new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Execution, trade.Executions[0].Id));
        Assert.True(await f.Assessments.TryAppendAsync(assessment, 1, null));
        if (bulk)
        {
            var deletion = f.Get<IAccountTradeDeletionStore>(); var plan = (await deletion.PrepareAsync(trade.TradingAccountId))!;
            Assert.Equal(AccountTradeDeletionStatus.Deleted, (await deletion.DeleteAsync(plan)).Status);
        }
        else Assert.NotNull(await f.Get<ITradeDeletionStore>().DeleteAsync(trade.Id));
        await f.Get<ITradingAccountDeletionStore>().DeleteAsync(trade.TradingAccountId);
        var replacement = await f.Seed(); // Same display name, different identity.
        await f.Restart();
        var historical = (await f.Reader.ReadAssessmentAsync(assessment.Id))!;
        Assert.Equal(trade.TradingAccountId, historical.Assessment.Target.AccountScope.AccountId);
        Assert.NotEqual(replacement.TradingAccountId, historical.Assessment.Target.AccountScope.AccountId);
        Assert.Equal("Synthetic", historical.AccountNameAtAssessment);
        Assert.Equal(TradingRuleSourceState.Unavailable, historical.TargetState);
        Assert.Equal(TradingRuleSourceState.Unavailable, Assert.Single(historical.Evidence).State);
        Assert.True(Assert.Single((await f.Reader.ReadCatalogAsync()).Items).AccountAvailable);
        Assert.True(await f.Rules.TryAppendAsync(rule.Archive(1, Now), 1));
        Assert.False(await f.Rules.TryAppendAsync((await f.Rules.GetLatestAsync(rule.RuleId))!.Reopen(2, Now), 2));
    }

    private sealed class Factory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    { public JournalDbContext CreateDbContext() => new(options); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class CancelSaved(CancellationTokenSource source) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        { source.Cancel(); return ValueTask.FromResult(result); }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "ptj-rules-" + Guid.NewGuid().ToString("N"));
        internal LocalApplicationPaths Paths { get; }
        private ServiceProvider services;
        private Fixture() { Paths = new(Root); Paths.EnsureDirectoriesExist(); services = Build(); }
        private ServiceProvider Build() => new ServiceCollection().AddSingleton<TimeProvider>(new Clock()).AddPersistence(Paths).BuildServiceProvider();
        internal T Get<T>() where T : notnull => services.GetRequiredService<T>();
        internal JournalDbContext Context() => Get<IDbContextFactory<JournalDbContext>>().CreateDbContext();
        internal ITradingRuleStore Rules => Get<ITradingRuleStore>();
        internal ITradingRuleAssessmentStore Assessments => Get<ITradingRuleAssessmentStore>();
        internal ITradingRuleReader Reader => Get<ITradingRuleReader>();
        internal static async Task<Fixture> Create(string? schema = null)
        {
            var f = new Fixture();
            try { await using var db = f.Context(); await db.GetService<IMigrator>().MigrateAsync(schema); return f; }
            catch { await f.DisposeAsync(); throw; }
        }
        internal async Task Stop() { await services.DisposeAsync(); JournalDataSession.ClearOwnedPools(Paths); }
        internal async Task Restart() { await Stop(); services = Build(); }
        internal async Task<TradingRuleRevision> Rule(TradingRuleDefinition definition)
        { var rule = TradingRuleRevision.Create(Guid.NewGuid(), definition, Now); Assert.True(await Rules.TryAppendAsync(rule, 0)); return rule; }
        internal async Task<Trade> Seed(string name = "Synthetic", string currency = "USD", DateTimeOffset? entry = null, DateTimeOffset? close = null)
        {
            Guid account = Guid.NewGuid(), instrument = Guid.NewGuid();
            await using (var db = Context())
            {
                db.TradingAccounts.Add(new() { Id = account, Name = name, AccountType = TradingAccountType.Demo, Currency = currency, IsActive = true, CreatedAtUtc = Now.AddDays(-10), UpdatedAtUtc = Now.AddDays(-10) });
                db.Instruments.Add(new() { Id = instrument, Symbol = "SYN" + instrument.ToString("N"), DisplayName = "Synthetic", AssetClass = AssetClass.Futures, Currency = currency, TickSize = 1, TickValue = 1, IsActive = true, CreatedAtUtc = Now.AddDays(-10), UpdatedAtUtc = Now.AddDays(-10) });
                await db.SaveChangesAsync();
            }
            var trade = Trade.Start(account, instrument, new(1, currency),
                new(Guid.NewGuid(), 1, entry ?? Now.AddDays(-1).AddMinutes(-1), ExecutionSide.Buy, 1, 100, null, 0, null, null, null), Now);
            trade.AddExecution(new(trade.Id, 2, close ?? Now.AddDays(-1), ExecutionSide.Sell, 1, 110, 1, 0, null, null, null), Now);
            await Get<ITradeStore>().AddAsync(trade); return trade;
        }
        public async ValueTask DisposeAsync() { await Stop(); Directory.Delete(Root, true); }
    }
}
