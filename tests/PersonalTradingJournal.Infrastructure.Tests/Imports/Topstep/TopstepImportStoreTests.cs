using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Accounts;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Topstep;

public sealed class TopstepImportStoreTests
{
    private static readonly DateTimeOffset ImportedAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static string Csv(params string[] rows) => TopstepCsvFixtures.WithRows(rows.Length == 0 ? [TopstepCsvFixtures.Row] : rows);
    private static TopstepImportConfirmation Review(TopstepImportPreview preview) => new(preview.SnapshotFingerprint, preview.CreationProposals.Select(r => r.CanonicalSymbol).ToArray());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportPersistsClosedRowProvenanceExactEconomicsAndReplaySkips(bool proposal)
    {
        await using Fixture f = await Fixture.Create(proposal);
        TopstepImportPreview preview = await f.Preview(Csv());
        await f.AssertCounts(0, proposal ? 0 : 1);
        TopstepImportResult result = await f.Import(preview, Csv());
        Assert.Equal(TopstepImportStatus.Imported, result.Status);
        Assert.Equal(1, result.ImportedTradeCount);
        Assert.Equal(proposal ? 1 : 0, result.CreatedInstrumentCount);
        Assert.Equal(1, f.Changes.TradesVersion);
        Assert.Equal(proposal ? 1 : 0, f.Changes.InstrumentsVersion);
        await using JournalDbContext context = await f.Database.ContextFactory.CreateDbContextAsync();
        TradeRecord record = await context.Trades.SingleAsync();
        TradeExecutionRecord[] executions = await context.TradeExecutions.OrderBy(e => e.Sequence).ToArrayAsync();
        Trade trade = TradePersistenceMapper.ToDomain(record, executions);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(2m, executions[0].Quantity);
        Assert.Equal(20000.125m, trade.AverageEntryPrice);
        Assert.Equal(20001.375m, trade.AverageExitPrice);
        Assert.Equal(5m, trade.GrossPnL);
        Assert.Equal(2.56m, trade.NetPnL);
        Assert.Equal(0m, executions[0].Commission);
        Assert.Equal(0m, executions[0].Fees);
        Assert.Equal(1m, executions[1].Commission);
        Assert.Equal(1.44m, executions[1].Fees);
        Assert.All(executions, e => { Assert.Null(e.ExternalExecutionId); Assert.Null(e.ExternalOrderId); Assert.Equal("MNQZ6", e.BrokerSymbol); });
        TopstepImportedRowRecord ledger = await context.TopstepImportedRows.SingleAsync();
        Assert.Equal(f.AccountId, ledger.TradingAccountIdAtImport);
        Assert.Equal("000SYNTH01", ledger.SourceId);
        Assert.Equal("TopstepClosedRowDerivedEntryExitV1", ledger.Representation);
        Assert.Equal(executions[0].Id, ledger.DerivedEntryExecutionId);
        Assert.Equal(executions[1].Id, ledger.DerivedExitExecutionId);
        Assert.Equal(preview.SnapshotFingerprint, ledger.PreviewFingerprint);
        Assert.Equal(preview.SourceIdentity.ContentSha256, ledger.SourceContentSha256);
        Assert.Equal(preview.Candidates[0].SourceRow, System.Text.Json.JsonSerializer.Deserialize<TopstepSourceRow>(ledger.SourceRowJson));
        Assert.Equal(2.56m, (await context.TradeBrowse.SingleAsync()).NetPnL);
        // Newly created proposal changes the catalog; subsequent replay uses a fresh reviewed preview.
        TopstepImportResult replay = await f.Import(await f.Preview(Csv()), Csv());
        Assert.Equal(TopstepImportStatus.NoChanges, replay.Status);
        Assert.Equal(1, replay.SkippedDuplicateTradeCount);
        Assert.Equal(result.ImportedTradeIds, replay.DuplicateTradeIds);
        Assert.Equal(1, f.Changes.TradesVersion);
        await f.AssertCounts(1, 1);
    }

    [Fact]
    public async Task MixedNewAndDuplicateRowsImportOnlyNewRowsAtomicallyIndependentOfFileOrder()
    {
        await using Fixture f = await Fixture.Create();
        await f.Import(await f.Preview(Csv()), Csv());
        string mixed = Csv(TopstepCsvFixtures.Replace(0, "SYNTH-2"), TopstepCsvFixtures.Row);
        TopstepImportResult result = await f.Import(await f.Preview(mixed), mixed);
        Assert.Equal(TopstepImportStatus.Imported, result.Status);
        Assert.Equal(1, result.ImportedTradeCount);
        Assert.Equal(1, result.SkippedDuplicateTradeCount);
        Assert.Equal(0, result.CreatedInstrumentCount);
        await f.AssertCounts(2, 1);
    }

    [Fact]
    public async Task ChangedEconomicContentUnderExistingIdBlocksWholeMixedBatch()
    {
        await using Fixture f = await Fixture.Create();
        await f.Import(await f.Preview(Csv()), Csv());
        string changed = TopstepCsvFixtures.Replace(5, "20002.125");
        changed = TopstepCsvFixtures.Replace(7, "8", changed);
        string mixed = Csv(TopstepCsvFixtures.Replace(0, "NEW-ROW"), changed);
        TopstepImportResult result = await f.Import(await f.Preview(mixed), mixed);
        Assert.Equal(TopstepImportConflictCodes.SourceIdentityConflict, result.ConflictCode);
        Assert.DoesNotContain("000SYNTH01", result.Message!);
        Assert.Equal(1, f.Changes.TradesVersion);
        await f.AssertCounts(1, 1);
        await using JournalDbContext context = await f.Database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2.56m, (await context.TradeBrowse.SingleAsync()).NetPnL);
    }

    [Theory]
    [InlineData("deletedAccount")]
    [InlineData("provider")]
    [InlineData("currency")]
    [InlineData("accountName")]
    [InlineData("deletedInstrument")]
    [InlineData("pricing")]
    [InlineData("ambiguous")]
    [InlineData("activity")]
    public async Task ReferenceChangesAfterPreviewBlockWithoutImportWrites(string change)
    {
        await using Fixture f = await Fixture.Create();
        TopstepImportPreview preview = await f.Preview(Csv());
        await using (JournalDbContext context = await f.Database.ContextFactory.CreateDbContextAsync())
        {
            TradingAccountRecord account = await context.TradingAccounts.SingleAsync();
            InstrumentRecord instrument = await context.Instruments.SingleAsync();
            switch (change)
            {
                case "deletedAccount": context.TradingAccounts.Remove(account); break;
                case "provider": account.ProviderName = "Other"; break;
                case "currency": account.Currency = "EUR"; break;
                case "accountName": account.Name = "Changed"; break;
                case "deletedInstrument": context.Instruments.Remove(instrument); break;
                case "pricing": instrument.TickValue = 1m; break;
                case "ambiguous": context.Instruments.Add(Fixture.Instrument(active: false)); break;
                case "activity": instrument.IsActive = false; break;
            }
            await context.SaveChangesAsync();
        }
        TopstepImportResult result = await f.Import(preview, Csv());
        Assert.Equal(TopstepImportConflictCodes.ReferenceDataChanged, result.ConflictCode);
        await f.AssertCounts(0, change == "deletedInstrument" ? 0 : change == "ambiguous" ? 2 : 1);
        Assert.Equal(0, f.Changes.TradesVersion);
    }

    [Fact]
    public async Task InactiveReferencesApprovedBeforePreviewAreKeptInactive()
    {
        await using Fixture f = await Fixture.Create();
        await using (JournalDbContext context = await f.Database.ContextFactory.CreateDbContextAsync())
        {
            (await context.Instruments.SingleAsync()).IsActive = false;
            (await context.TradingAccounts.SingleAsync()).IsActive = false;
            await context.SaveChangesAsync();
        }
        Assert.Equal(TopstepImportStatus.Imported, (await f.Import(await f.Preview(Csv()), Csv())).Status);
        await using JournalDbContext verify = await f.Database.ContextFactory.CreateDbContextAsync();
        Assert.False((await verify.Instruments.SingleAsync()).IsActive);
        Assert.False((await verify.TradingAccounts.SingleAsync()).IsActive);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("stale")]
    [InlineData("proposal")]
    [InlineData("source")]
    [InlineData("blocked")]
    public async Task InvalidReviewBlockedPreviewOrChangedSourceCannotWrite(string reason)
    {
        await using Fixture f = await Fixture.Create(proposal: true);
        string csv = reason == "blocked" ? Csv(TopstepCsvFixtures.Replace(7, "99")) : Csv();
        TopstepImportPreview preview = await f.Preview(csv);
        TopstepImportConfirmation review = Review(preview);
        if (reason == "none") review = new(preview.SnapshotFingerprint, []);
        if (reason == "stale") review = review with { SnapshotFingerprint = "OTHER" };
        if (reason == "proposal") review = new(preview.SnapshotFingerprint, ["WRONG-SYMBOL"]);
        TopstepImportResult result = await f.Import(preview, reason == "source" ? csv + "\n" : csv, review);
        Assert.Equal(reason == "source" ? TopstepImportConflictCodes.SourceChanged : TopstepImportConflictCodes.ReviewRequired, result.ConflictCode);
        await f.AssertCounts(0, 0);
    }

    [Fact]
    public async Task SameSourceIdIsIndependentAcrossExplicitDestinationAccountsAndCaseSensitiveWithinOne()
    {
        await using Fixture f = await Fixture.Create();
        await f.Import(await f.Preview(Csv()), Csv());
        Guid otherId = Guid.NewGuid();
        await using (JournalDbContext context = await f.Database.ContextFactory.CreateDbContextAsync())
        {
            context.TradingAccounts.Add(Fixture.Account(otherId));
            await context.SaveChangesAsync();
        }
        Assert.Equal(TopstepImportStatus.Imported, (await f.Import(await f.Preview(Csv(), otherId), Csv())).Status);
        string caseVariant = Csv(TopstepCsvFixtures.Replace(0, "000synth01"));
        Assert.Equal(TopstepImportStatus.Imported, (await f.Import(await f.Preview(caseVariant), caseVariant)).Status);
        await f.AssertCounts(3, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentConfirmationNeverCreatesDuplicateOrPartialGraphs(bool proposal)
    {
        await using Fixture f = await Fixture.Create(proposal);
        TopstepImportPreview preview = await f.Preview(Csv());
        using var start = new ManualResetEventSlim(false);
        Task<TopstepImportResult> One() => Task.Run(async () => { start.Wait(); return await f.Import(preview, Csv()); });
        Task<TopstepImportResult>[] calls = [One(), One()];
        start.Set();
        TopstepImportResult[] results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(45));
        Assert.Single(results, r => r.Status == TopstepImportStatus.Imported);
        Assert.Single(results, r => r.Status == (proposal ? TopstepImportStatus.Blocked : TopstepImportStatus.NoChanges));
        if (proposal) Assert.Contains(results, r => r.ConflictCode == TopstepImportConflictCodes.ReferenceDataChanged);
        await f.AssertCounts(1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterSqlWritesRollsBackTradesCostsLedgerAndProposedInstrument(bool cancel)
    {
        await using Fixture f = await Fixture.Create(proposal: true);
        string csv = Csv(TopstepCsvFixtures.Row, TopstepCsvFixtures.Replace(0, "SYNTH-2"));
        TopstepImportPreview preview = await f.Preview(csv);
        using var cancellation = new CancellationTokenSource();
        var fault = new AfterFlushFault(cancel ? cancellation : null);
        await using JournalDbContext sample = await f.Database.ContextFactory.CreateDbContextAsync();
        var factory = new FaultFactory(new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(sample.Database.GetDbConnection().ConnectionString).AddInterceptors(fault).Options);
        var useCase = new ImportTopstepTradesUseCase(new TopstepImportStore(factory), new FixedTime(), f.Changes);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ImportAsync(preview, Review(preview), "synthetic.csv", stream, cancellation.Token));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ImportAsync(preview, Review(preview), "synthetic.csv", stream));
        Assert.Equal(2, fault.TradesObservedInsideTransaction);
        Assert.Equal(2, fault.LedgerRowsObservedInsideTransaction);
        await f.AssertCounts(0, 0);
        Assert.Equal(0, f.Changes.TradesVersion);
        Assert.Equal(0, f.Changes.InstrumentsVersion);
        Assert.Equal(TopstepImportStatus.Imported, (await f.Import(preview, csv)).Status);
    }

    [Fact]
    public async Task PreCancelledConfirmationMakesNoWrites()
    {
        await using Fixture f = await Fixture.Create(true);
        TopstepImportPreview preview = await f.Preview(Csv());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Import(preview, Csv(), token: cancellation.Token));
        await f.AssertCounts(0, 0);
    }

    [Fact]
    public async Task DecimalRepresentationChangesReplayButChangedCostsConflict()
    {
        await using Fixture f = await Fixture.Create();
        await f.Import(await f.Preview(Csv()), Csv());
        string rescaled = Csv(TopstepCsvFixtures.Replace(8, "2.000", TopstepCsvFixtures.Replace(6, "1.44000")));
        Assert.Equal(TopstepImportStatus.NoChanges, (await f.Import(await f.Preview(rescaled), rescaled)).Status);
        string changedCosts = Csv(TopstepCsvFixtures.Replace(6, "2.44"));
        Assert.Equal(TopstepImportConflictCodes.SourceIdentityConflict, (await f.Import(await f.Preview(changedCosts), changedCosts)).ConflictCode);
        await f.AssertCounts(1, 1);
    }

    [Fact]
    public async Task DatabaseEnforcesAccountScopedSourceUniquenessAndTradeDeletionCascadesLedger()
    {
        await using Fixture f = await Fixture.Create();
        string csv = Csv(TopstepCsvFixtures.Row, TopstepCsvFixtures.Replace(0, "SYNTH-2"));
        await f.Import(await f.Preview(csv), csv);
        await using (JournalDbContext context = await f.Database.ContextFactory.CreateDbContextAsync())
        {
            TopstepImportedRowRecord[] rows = await context.TopstepImportedRows.OrderBy(r => r.SourceId).ToArrayAsync();
            rows[1].SourceId = rows[0].SourceId;
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        await f.AssertCounts(2, 1);
        await using (JournalDbContext context = await f.Database.ContextFactory.CreateDbContextAsync())
        {
            context.Trades.RemoveRange(await context.Trades.ToArrayAsync());
            await context.SaveChangesAsync();
        }
        await f.AssertCounts(0, 1);
        Assert.Equal(2, (await f.Import(await f.Preview(csv), csv)).ImportedTradeCount);
    }

    private sealed class AfterFlushFault(CancellationTokenSource? cancellation) : SaveChangesInterceptor
    {
        public int TradesObservedInsideTransaction { get; private set; }
        public int LedgerRowsObservedInsideTransaction { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var context = (JournalDbContext)eventData.Context!;
            TradesObservedInsideTransaction = await context.Trades.CountAsync(cancellationToken);
            LedgerRowsObservedInsideTransaction = await context.TopstepImportedRows.CountAsync(cancellationToken);
            if (cancellation is not null) { cancellation.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
            throw new InvalidOperationException("Synthetic failure after SQL flush, before transaction commit.");
        }
    }
    private sealed class FaultFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    { public JournalDbContext CreateDbContext() => new(options); }
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => ImportedAt; }

    private sealed class Fixture(ReaderTestDatabase database, Guid accountId) : IAsyncDisposable
    {
        public ReaderTestDatabase Database => database;
        public Guid AccountId => accountId;
        public TopstepImportChangeTracker Changes { get; } = new();
        public static async Task<Fixture> Create(bool proposal = false)
        {
            ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
            Guid id = Guid.NewGuid();
            await using JournalDbContext context = await db.ContextFactory.CreateDbContextAsync();
            context.TradingAccounts.Add(Account(id));
            if (!proposal) context.Instruments.Add(Instrument());
            await context.SaveChangesAsync();
            return new(db, id);
        }
        public static TradingAccountRecord Account(Guid id) => new() { Id = id, Name = "Synthetic Topstep", AccountType = TradingAccountType.PropFunded,
            ProviderName = "Topstep", Currency = "USD", IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch };
        public static InstrumentRecord Instrument(bool active = true) => new() { Id = Guid.NewGuid(), Symbol = "MNQ", DisplayName = "Micro E-mini Nasdaq-100",
            AssetClass = AssetClass.Futures, Exchange = "CME", Currency = "USD", TickSize = .25m, TickValue = .5m, IsActive = active,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch };
        public async Task<TopstepImportPreview> Preview(string csv, Guid? selected = null)
        {
            var builder = new TopstepImportPreviewBuilder(new TopstepCsvParser(), new TopstepTradeCandidateReconstructor(),
                new(new InstrumentReader(database.ContextFactory), new TradingAccountReader(database.ContextFactory)));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            return await builder.BuildAsync("synthetic.csv", stream, selected ?? accountId, TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd);
        }
        public async Task<TopstepImportResult> Import(TopstepImportPreview preview, string csv, TopstepImportConfirmation? review = null, CancellationToken token = default)
        {
            var useCase = new ImportTopstepTradesUseCase(database.ServiceProvider.GetRequiredService<ITopstepImportStore>(), new FixedTime(), Changes);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            return await useCase.ImportAsync(preview, review ?? Review(preview), "synthetic.csv", stream, token);
        }
        public async Task AssertCounts(int trades, int instruments)
        {
            await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
            Assert.Equal(trades, await context.Trades.CountAsync());
            Assert.Equal(trades * 2, await context.TradeExecutions.CountAsync());
            Assert.Equal(trades, await context.TopstepImportedRows.CountAsync());
            Assert.Equal(trades, await context.TradeBrowse.CountAsync());
            Assert.Equal(instruments, await context.Instruments.CountAsync());
            Assert.False(await context.TradovateImportedExecutions.AnyAsync());
        }
        public ValueTask DisposeAsync() => database.DisposeAsync();
    }
}
