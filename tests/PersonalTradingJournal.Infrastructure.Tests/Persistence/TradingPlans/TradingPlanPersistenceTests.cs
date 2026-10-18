using System.Data.Common;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Infrastructure.Tests.Backups;
using PersonalTradingJournal.Infrastructure.TradingPlans;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

public sealed class TradingPlanPersistenceTests
{
    [Fact]
    public async Task ExactReadyAndHistoryReadsRejectMisattributedOwnerWithoutConsultingCatalogs()
    {
        await using var f = await Fixture.Create(); var p = await f.Ready(await f.CreatePlan());
        await using (var db = f.Context()) await db.TradingPlans.ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountId, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.LoadReadyAsync(p.LatestReadyRevision!.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.ReadyHistoryAsync(p.Id));
    }
    [Theory] [InlineData("missing-levels")] [InlineData("null-levels")] [InlineData("missing-risk")] [InlineData("missing-confirmed")]
    public async Task IncompleteNestedStoredValuesAreRejectedInsteadOfDefaulted(string defect)
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        var node = JsonNode.Parse(TradingPlanJson.WriteDraft(p.WorkingDraft!))!;
        var content = node["Content"]!;
        if (defect == "missing-levels") content["Instruments"]![0]!.AsObject().Remove("Levels");
        if (defect == "null-levels") content["Instruments"]![0]!["Levels"] = null;
        if (defect == "missing-risk") content.AsObject().Remove("RiskLimits");
        if (defect == "missing-confirmed") content.AsObject().Remove("Confirmed");
        string invalid = node.ToJsonString();
        await using (var db = f.Context()) await db.TradingPlans.ExecuteUpdateAsync(s => s.SetProperty(x => x.DraftJson, invalid));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.LoadAsync(f.Key));
    }
    [Fact]
    public async Task CancellationAfterCommitReturnsTheActualSavedRevision()
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        p = await f.Success(TradingPlanWriteOperation.ConfirmChecklist, p, Change(p.WorkingDraft!.Content));
        using var cts = new CancellationTokenSource();
        var store = new TradingPlanStore(f.Factory(new AfterCommit(cts)), f.Clock);
        var result = await store.WriteAsync(Command(TradingPlanWriteOperation.MarkReady, p), cts.Token);
        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(TradingPlanWriteStatus.Success, result.Status);
        Assert.Equal(1, result.Plan!.LatestReadyRevision!.Id.Revision);
        Assert.Single(await f.Store.ReadyHistoryAsync(p.Id));
    }

    [Fact]
    public async Task ReferenceWriterCannotSlipBetweenFreshValidationAndSave()
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        p = await f.Success(TradingPlanWriteOperation.ConfirmChecklist, p, Change(p.WorkingDraft!.Content));
        bool blocked = false;
        var store = new TradingPlanStore(f.Factory(new BeforeSave(() =>
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = f.Paths.DatabasePath, Pooling = false }.ToString());
            connection.Open(); using var command = connection.CreateCommand(); command.CommandTimeout = 1;
            command.CommandText = "UPDATE TradingAccounts SET Currency='EUR'";
            var error = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
            Assert.Equal(5, error.SqliteErrorCode); blocked = true;
        })), f.Clock);
        var result = await store.WriteAsync(Command(TradingPlanWriteOperation.MarkReady, p));
        Assert.True(blocked); Assert.Equal(TradingPlanWriteStatus.Success, result.Status);
        Assert.Equal("USD", result.Plan!.LatestReadyRevision!.References.Account.Currency);
    }

    [Fact]
    public async Task AllLifecycleStatesRoundTripWithExactTextDecimalsDatesAndFrozenHistoryAfterRestart()
    {
        await using var f = await Fixture.Create();
        var draft = await f.CreatePlan();
        Assert.Equal(TradingPlanPreparationState.DraftOnly, draft.PreparationState);
        Assert.Equal(TradingPlanChecklist.None, draft.WorkingDraft!.Content.Confirmed);
        Assert.Equal("  NQ focus\r\nЯсно  ", draft.WorkingDraft.Content.Focus);
        Assert.Equal(20000.250000000123456789m, draft.WorkingDraft.Content.Instruments[0].Levels.PreviousDayHigh);
        Assert.Null(draft.WorkingDraft.Content.Instruments[0].Levels.AsiaHigh);
        Assert.Null(draft.WorkingDraft.Content.RiskLimits.DailyLossLimit);
        draft = await f.Success(TradingPlanWriteOperation.ConfirmChecklist, draft, Change(draft.WorkingDraft.Content));
        await f.Restart();
        var read = (await f.Store.LoadAsync(f.Key))!;
        Assert.Equal(draft.Version, read.Version); Assert.Equal(TradingPlanChecklist.All, read.WorkingDraft!.Content.Confirmed);
        var ready = await f.Success(TradingPlanWriteOperation.MarkReady, read);
        string original = TradingPlanJson.WriteReady(ready.LatestReadyRevision!);
        Assert.Equal(TradingPlanPreparationState.Ready, ready.PreparationState);
        Assert.Equal(f.Key, ready.Key); Assert.Equal(f.Now, ready.LatestReadyRevision!.ReadyAtUtc);
        var next = await f.Success(TradingPlanWriteOperation.StartDraft, ready, source: ready.LatestReadyRevision.Id);
        Assert.Equal(TradingPlanPreparationState.DraftAfterReady, next.PreparationState);
        Assert.Equal(TradingPlanChecklist.None, next.WorkingDraft!.Content.Confirmed);
        next = await f.Success(TradingPlanWriteOperation.SaveDraft, next, Change(next.WorkingDraft.Content, "Second"));
        next = await f.Ready(next);
        Assert.Equal(2, next.LatestReadyRevision!.Id.Revision);
        next = await f.Success(TradingPlanWriteOperation.StartDraft, next, source: ready.LatestReadyRevision.Id);
        Assert.Equal(ready.LatestReadyRevision.Id, next.WorkingDraft!.DerivedFromRevision);
        Assert.Equal("  NQ focus\r\nЯсно  ", next.WorkingDraft.Content.Focus);
        await f.Restart();
        Assert.Equal(TradingPlanPreparationState.DraftAfterReady, (await f.Store.LoadAsync(f.Key))!.PreparationState);
        Assert.Equal(original, TradingPlanJson.WriteReady((await f.Store.LoadReadyAsync(ready.LatestReadyRevision.Id))!));
        Assert.Equal(new[] { 2, 1 }, (await f.Store.ReadyHistoryAsync(next.Id)).Select(x => x.Id.Revision));
        Assert.Equal(1, Assert.Single(await f.Store.ReadyHistoryAsync(next.Id, 2, 1)).Id.Revision);
    }

    [Fact]
    public async Task DuplicateCreateStaleSaveAndConcurrentReadyPublishOnlyOneRevision()
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        Assert.Equal(TradingPlanWriteStatus.TargetAlreadyExists, (await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, f.Key, f.Content))).Status);
        var confirmed = await f.Success(TradingPlanWriteOperation.ConfirmChecklist, p, Change(p.WorkingDraft!.Content));
        Assert.Equal(TradingPlanWriteStatus.Conflict, (await f.Store.WriteAsync(Command(TradingPlanWriteOperation.SaveDraft, p))).Status);
        var command = Command(TradingPlanWriteOperation.MarkReady, confirmed);
        var results = await Task.WhenAll(Task.Run(() => f.Store.WriteAsync(command)), Task.Run(() => f.Store.WriteAsync(command)));
        Assert.Single(results, x => x.Status == TradingPlanWriteStatus.Success);
        Assert.Single(results, x => x.Status == TradingPlanWriteStatus.Conflict);
        Assert.Single(await f.Store.ReadyHistoryAsync(p.Id));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CancellationOrStorageFailureAfterSqlWritesRollsBackBothRecords(bool cancel)
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        p = await f.Success(TradingPlanWriteOperation.ConfirmChecklist, p, Change(p.WorkingDraft!.Content));
        using var cts = new CancellationTokenSource();
        var factory = f.Factory(new AfterSave(() => { if (cancel) cts.Cancel(); else throw new IOException("Synthetic storage failure"); }));
        var store = new TradingPlanStore(factory, f.Clock);
        var result = await store.WriteAsync(Command(TradingPlanWriteOperation.MarkReady, p), cts.Token);
        Assert.Equal(cancel ? TradingPlanWriteStatus.Cancelled : TradingPlanWriteStatus.StorageFailure, result.Status);
        Assert.Empty(await f.Store.ReadyHistoryAsync(p.Id));
        Assert.Equal(p.Version, (await f.Store.LoadAsync(f.Key))!.Version);
    }

    [Fact]
    public async Task PreCancelledOperationsWriteNothingAndIncompleteReadinessDoesNotAdvanceState()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(TradingPlanWriteStatus.Cancelled, (await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, f.Key, f.Content), new(true))).Status);
        Assert.Null(await f.Store.LoadAsync(f.Key));
        var p = await f.CreatePlan();
        var failure = await f.Store.WriteAsync(Command(TradingPlanWriteOperation.MarkReady, p));
        Assert.Equal(TradingPlanWriteStatus.Invalid, failure.Status); Assert.NotEmpty(failure.Issues);
        Assert.Empty(await f.Store.ReadyHistoryAsync(p.Id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Store.LoadAsync(f.Key, new(true)));
    }

    [Theory]
    [InlineData("currency")] [InlineData("instrument")] [InlineData("cosmetic")]
    public async Task ChangedReferenceNeedsExplicitRefreshWhichClearsConfirmations(string change)
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        p = await f.Success(TradingPlanWriteOperation.ConfirmChecklist, p, Change(p.WorkingDraft!.Content));
        await using (var db = f.Context())
        {
            if (change == "currency") await db.TradingAccounts.Where(x => x.Id == f.Account.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Currency, "EUR"));
            else if (change == "cosmetic") await db.TradingAccounts.Where(x => x.Id == f.Account.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Renamed"));
            else await db.Instruments.Where(x => x.Id == f.Instrument.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.TickValue, 20m));
        }
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, (await f.Store.WriteAsync(Command(TradingPlanWriteOperation.MarkReady, p))).Status);
        Assert.Equal(p.Version, (await f.Store.LoadAsync(f.Key))!.Version);
        if (change != "instrument")
            Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, (await f.Store.WriteAsync(Command(TradingPlanWriteOperation.RefreshReferences, p))).Status);
        string reviewedCurrency;
        await using (var context = f.Context()) reviewedCurrency = (await context.TradingAccounts.SingleAsync()).Currency;
        var refresh = await f.Store.WriteAsync(new(TradingPlanWriteOperation.RefreshReferences, p.Key, p.WorkingDraft!.Content,
            new(p.Id, p.Version), expectedAccountCurrency: reviewedCurrency));
        Assert.Equal(TradingPlanWriteStatus.Success, refresh.Status); p = refresh.Plan!;
        Assert.Equal(TradingPlanChecklist.None, p.WorkingDraft!.Content.Confirmed);
        p = await f.Ready(p); Assert.NotNull(p.LatestReadyRevision);
    }

    [Theory] [InlineData("account")] [InlineData("instrument")] [InlineData("setup")] [InlineData("rule")]
    public async Task InactiveReferencesBlockNewPreparationButPreserveFrozenReady(string kind)
    {
        await using var f = await Fixture.Create(); var p = await f.Ready(await f.CreatePlan());
        var frozen = TradingPlanJson.WriteReady(p.LatestReadyRevision!);
        p = await f.Success(TradingPlanWriteOperation.StartDraft, p, source: p.LatestReadyRevision!.Id);
        await using (var db = f.Context())
        {
            if (kind == "account") await db.TradingAccounts.Where(x => x.Id == f.Account.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            if (kind == "instrument") await db.Instruments.Where(x => x.Id == f.Instrument.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            if (kind == "setup") await db.TradingSetups.Where(x => x.Id == f.Setup.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            if (kind == "rule") Assert.True(await f.Get<ITradingRuleStore>().TryAppendAsync(f.Rule.Archive(1, f.Now), 1));
        }
        var result = await f.Store.WriteAsync(Command(TradingPlanWriteOperation.SaveDraft, p));
        Assert.Equal(TradingPlanWriteStatus.ReferenceUnavailable, result.Status);
        await f.Restart();
        Assert.Equal(frozen, TradingPlanJson.WriteReady((await f.Store.LoadReadyAsync(p.LatestReadyRevision!.Id))!));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task NewSetupOrRuleRevisionCannotSilentlyReplaceConfirmedSelection(bool rule)
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        if (rule) Assert.True(await f.Get<ITradingRuleStore>().TryAppendAsync(f.Rule.Revise(1, new("Changed rule"), f.Now), 1));
        else Assert.True(await f.Get<ITradingSetupPlaybookStore>().TryAppendAsync(f.Playbook.Revise(f.Setup, 1,
            new("Changed", "e", "c", "i", "x"), f.Now), 1, f.Setup.UpdatedAtUtc));
        Assert.Equal(TradingPlanWriteStatus.ReferenceChanged, (await f.Store.WriteAsync(Command(TradingPlanWriteOperation.SaveDraft, p))).Status);
        Assert.Equal(p.Version, (await f.Store.LoadAsync(f.Key))!.Version);
    }

    [Fact]
    public async Task DeletedSameNameReplacementsDoNotRetargetOldPlanOrEraseReady()
    {
        await using var f = await Fixture.Create(); var p = await f.Ready(await f.CreatePlan());
        await using (var db = f.Context())
        {
            await db.TradingAccounts.Where(x => x.Id == f.Account.Id).ExecuteDeleteAsync();
            await db.Instruments.Where(x => x.Id == f.Instrument.Id).ExecuteDeleteAsync();
            db.TradingAccounts.Add(TradingAccountPersistenceMapper.ToRecord(new(f.Account.Name, TradingAccountType.Demo, null, null, "USD", null, f.Now)));
            db.Instruments.Add(InstrumentPersistenceMapper.ToRecord(new(f.Instrument.Symbol, f.Instrument.DisplayName, AssetClass.Futures, null, "USD", 1, 1, f.Now)));
            await db.SaveChangesAsync();
        }
        await f.Restart();
        var old = (await f.Store.LoadAsync(f.Key))!;
        Assert.Equal(f.Account.Id, old.LatestReadyRevision!.References.Account.AccountId);
        Assert.Equal(f.Instrument.Id, Assert.Single(old.LatestReadyRevision.References.Instruments).InstrumentId);
        Assert.Equal(TradingPlanWriteStatus.ReferenceUnavailable, (await f.Store.WriteAsync(Command(TradingPlanWriteOperation.StartDraft, old, source: p.LatestReadyRevision!.Id))).Status);
    }

    [Fact]
    public async Task CopyUsesAuthoritativeReadyResetsDailyContextAndConcurrentTargetCreateNeverOverwrites()
    {
        await using var f = await Fixture.Create(); var p = await f.Ready(await f.CreatePlan());
        var target = new TradingPlanKey(f.Account.Id, f.Key.NewYorkDate.AddDays(1));
        var cmd = new TradingPlanWriteCommand(TradingPlanWriteOperation.CopyForward, target, new(focus: "Ignore forged input"), sourceRevision: p.LatestReadyRevision!.Id);
        var writes = await Task.WhenAll(Task.Run(() => f.Store.WriteAsync(cmd)), Task.Run(() => f.Store.WriteAsync(cmd)));
        var copy = Assert.Single(writes, x => x.Status == TradingPlanWriteStatus.Success).Plan!;
        Assert.Single(writes, x => x.Status == TradingPlanWriteStatus.TargetAlreadyExists);
        Assert.NotEqual(p.Id, copy.Id); Assert.Equal(f.Content.Focus, copy.WorkingDraft!.Content.Focus);
        Assert.Equal(TradingPlanChecklist.None, copy.WorkingDraft.Content.Confirmed);
        Assert.Null(copy.WorkingDraft.Content.Instruments[0].Bias); Assert.Equal("", copy.WorkingDraft.Content.Instruments[0].MarketThesis);
        Assert.Null(copy.WorkingDraft.Content.Instruments[0].Levels.PreviousDayHigh); Assert.Equal("", copy.WorkingDraft.Content.NewsAndRestrictions);
        Assert.Single(await f.Store.ReadyHistoryAsync(p.Id)); Assert.Empty(await f.Store.ReadyHistoryAsync(copy.Id));
    }

    [Fact]
    public async Task HistoryUsesBoundedExactAccountDateKeysetsAndCurrentLifecycle()
    {
        await using var f = await Fixture.Create();
        for (int i = 0; i < 5; i++) Assert.Equal(TradingPlanWriteStatus.Success, (await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft,
            new(f.Account.Id, f.Key.NewYorkDate.AddDays(i)), new()))).Status);
        var q = new TradingPlanHistoryQuery(f.Account.Id, f.Key.NewYorkDate, f.Key.NewYorkDate.AddDays(9), limit: 2);
        var a = await f.Store.HistoryAsync(q); var b = await f.Store.HistoryAsync(q, a[^1].Key.NewYorkDate);
        Assert.Equal(4, a.Concat(b).Select(x => x.Token.PlanId).Distinct().Count());
        Assert.Equal(f.Key.NewYorkDate.AddDays(4), a[0].Key.NewYorkDate);
        Assert.Empty(await f.Store.HistoryAsync(new(Guid.NewGuid(), q.FirstDate, q.LastDate)));
        Assert.Empty(await f.Store.HistoryAsync(new(f.Account.Id, q.FirstDate, q.LastDate, TradingPlanStatus.Ready)));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Store.ReadyHistoryAsync(a[0].Token.PlanId, limit: 101));
    }

    [Fact]
    public async Task ReadyRowsRejectUpdateDeleteAndSkippedRevisionAtDatabaseBoundary()
    {
        await using var f = await Fixture.Create(); var p = await f.Ready(await f.CreatePlan());
        await using var db = f.Context();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE TradingPlanReadyRevisions SET SnapshotJson='invalid'"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM TradingPlanReadyRevisions"));
        db.TradingPlanReadyRevisions.Add(new() { PlanId = p.Id, Revision = 3, SnapshotJson = "{}", ReadyAtUtc = f.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Single(await f.Store.ReadyHistoryAsync(p.Id));
    }

    [Fact]
    public async Task MalformedStoredDraftFailsReadAndWritesWithoutRepair()
    {
        await using var f = await Fixture.Create(); var p = await f.CreatePlan();
        await using (var db = f.Context()) await db.TradingPlans.ExecuteUpdateAsync(s => s.SetProperty(x => x.DraftJson, "{}"));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.LoadAsync(f.Key));
        Assert.Equal(TradingPlanWriteStatus.StorageFailure, (await f.Store.WriteAsync(Command(TradingPlanWriteOperation.SaveDraft, p))).Status);
        await using var check = f.Context(); Assert.Equal("{}", (await check.TradingPlans.SingleAsync()).DraftJson);
    }

    [Fact]
    public async Task PreviousSchemaMigrationKeepsExistingCatalogAndCreatesNoFabricatedPlans()
    {
        await using var f = await Fixture.Create("20261015091425_AddSavedPeriodReviews");
        await using var db = f.Context(); var before = await db.TradingAccounts.AsNoTracking().SingleAsync();
        await db.Database.MigrateAsync();
        var after = await db.TradingAccounts.AsNoTracking().SingleAsync();
        Assert.Equal(before.Id, after.Id); Assert.Equal(before.Currency, after.Currency);
        Assert.Empty(await db.TradingPlans.ToArrayAsync()); Assert.Empty(await db.TradingPlanReadyRevisions.ToArrayAsync());
        Assert.NotNull(await f.CreatePlan());
    }

    internal static TradingPlanContent Change(TradingPlanContent c, string? focus = null, TradingPlanChecklist confirmed = TradingPlanChecklist.All) =>
        new(c.Session, focus ?? c.Focus, c.Intent, c.Instruments, c.Scenarios, c.NewsAndRestrictions, c.StandAside, c.RiskLimits, c.Rules, confirmed);
    internal static TradingPlanWriteCommand Command(TradingPlanWriteOperation op, TradingPlan p, TradingPlanContent? content = null, TradingPlanRevisionId? source = null) =>
        new(op, p.Key, content ?? p.WorkingDraft?.Content ?? p.LatestReadyRevision!.Content, new(p.Id, p.Version), source);
    private sealed class AfterSave(Action action) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        { action(); return ValueTask.FromResult(result); }
    }
    private sealed class BeforeSave(Action action) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { action(); return ValueTask.FromResult(result); }
    }
    private sealed class AfterCommit(CancellationTokenSource source) : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        { source.Cancel(); return Task.CompletedTask; }
    }
    internal sealed class TestClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero); }
    private sealed class ContextFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    { public JournalDbContext CreateDbContext() => new(options); }
    internal sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "ptj-plan-test-" + Guid.NewGuid().ToString("N"));
        internal LocalApplicationPaths Paths { get; }
        internal TestClock Clock { get; } = new();
        internal DateTimeOffset Now => Clock.GetUtcNow();
        private ServiceProvider services;
        internal TradingAccount Account { get; }
        internal Instrument Instrument { get; }
        internal TradingSetup Setup { get; }
        internal TradingSetupPlaybookRevision Playbook { get; }
        internal TradingRuleRevision Rule { get; }
        internal TradingPlanContent Content { get; }
        internal TradingPlanKey Key => new(Account.Id, new(2026, 11, 1));
        private Fixture()
        {
            Paths = new(Root); Paths.EnsureDirectoriesExist(); services = Build();
            var time = Now.AddDays(-1);
            Account = new("Synthetic USD", TradingAccountType.Demo, null, null, "USD", null, time);
            Instrument = new("NQ", "Synthetic Nasdaq", AssetClass.Futures, null, "USD", .25m, 5m, time);
            Setup = new("Reclaim", "Synthetic guidance", time);
            Playbook = TradingSetupPlaybookRevision.Create(Setup, new("Range", "Entry", "Confirm", "Invalidate", "Execute"), time);
            Rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Wait", "For retest", automaticCriterion: new(RuleProfitBasis.StrictNet, 50.0000001m, "USD")), time);
            Content = new(TradingPlanSession.NewYork, "  NQ focus\r\nЯсно  ", instruments: [new(Instrument.Id, TradingPlanBias.Conditional,
                "Range", new(PreviousDayHigh: 20000.250000000123456789m, PreviousDayLow: -1m, LondonHigh: 0m))],
                scenarios: [new(Guid.NewGuid(), Instrument.Id, "Retest", direction: TradeDirection.Long, condition: "Reclaim",
                    response: "Wait", invalidation: "Lost level", setup: new(Setup.Id, 1, time))], newsAndRestrictions: "Synthetic news",
                riskLimits: new(12.1234567890123456789m, null, 0, 2), rules: [new(Rule.RuleId, 1)]);
        }
        private ServiceProvider Build() => new ServiceCollection().AddSingleton<TimeProvider>(Clock).AddPersistence(Paths).BuildServiceProvider();
        internal T Get<T>() where T : notnull => services.GetRequiredService<T>();
        internal ITradingPlanStore Store => Get<ITradingPlanStore>();
        internal JournalDbContext Context() => Get<IDbContextFactory<JournalDbContext>>().CreateDbContext();
        internal IDbContextFactory<JournalDbContext> Factory(IInterceptor interceptor) => new ContextFactory(new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = Paths.DatabasePath, Pooling = false, ForeignKeys = true }.ToString()).AddInterceptors(interceptor).Options);
        internal static async Task<Fixture> Create(string? schema = null)
        {
            var f = new Fixture();
            try
            {
                if (schema is null) await MigratedBackupTestDatabase.CreateAsync(f.Paths.DatabasePath);
                await using var db = f.Context(); if (schema is not null) await db.GetService<IMigrator>().MigrateAsync(schema);
                db.TradingAccounts.Add(TradingAccountPersistenceMapper.ToRecord(f.Account)); db.Instruments.Add(InstrumentPersistenceMapper.ToRecord(f.Instrument));
                db.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(f.Setup)); await db.SaveChangesAsync();
                Assert.True(await f.Get<ITradingSetupPlaybookStore>().TryAppendAsync(f.Playbook, 0, f.Setup.UpdatedAtUtc));
                Assert.True(await f.Get<ITradingRuleStore>().TryAppendAsync(f.Rule, 0));
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        internal async Task<TradingPlan> CreatePlan()
        { var r = await Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, Key, Content)); Assert.Equal(TradingPlanWriteStatus.Success, r.Status); return r.Plan!; }
        internal async Task<TradingPlan> Success(TradingPlanWriteOperation op, TradingPlan p, TradingPlanContent? content = null, TradingPlanRevisionId? source = null)
        { var r = await Store.WriteAsync(Command(op, p, content, source)); Assert.Equal(TradingPlanWriteStatus.Success, r.Status); return r.Plan!; }
        internal async Task<TradingPlan> Ready(TradingPlan p) => await Success(TradingPlanWriteOperation.MarkReady,
            await Success(TradingPlanWriteOperation.ConfirmChecklist, p, Change(p.WorkingDraft!.Content)));
        internal async Task Stop() { await services.DisposeAsync(); JournalDataSession.ClearOwnedPools(Paths); }
        internal async Task Restart() { await Stop(); services = Build(); }
        public async ValueTask DisposeAsync() { await Stop(); Directory.Delete(Root, true); }
    }
}
