using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

public sealed class TradovateImportStoreTests
{
    private static readonly DateTimeOffset ImportedAt =
        new(2026, 9, 23, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportAsyncPersistsCompleteGraphUnknownCostsAndDurableIdentities()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        TradovateImportPreparationResult preparation = Preparation(accountId);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(preparation, ImportedAt));

        Assert.Equal(TradovateImportStatus.Imported, result.Status);
        Assert.Equal(1, result.ImportedTradeCount);
        Assert.Equal(1, result.CreatedInstrumentCount);
        Guid tradeId = Assert.Single(result.ImportedTradeIds);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        TradeRecord trade = await context.Trades.AsNoTracking().SingleAsync();
        InstrumentRecord instrument = await context.Instruments.AsNoTracking().SingleAsync();
        TradeExecutionRecord[] executions = await context.TradeExecutions
            .AsNoTracking().OrderBy(item => item.Sequence).ToArrayAsync();
        TradeBrowseRecord browse = await context.TradeBrowse.AsNoTracking().SingleAsync();
        TradovateImportedExecutionRecord[] identities = await context
            .TradovateImportedExecutions.AsNoTracking()
            .OrderBy(item => item.ExternalExecutionId).ToArrayAsync();

        Assert.Equal(tradeId, trade.Id);
        Assert.Equal(accountId, trade.TradingAccountId);
        Assert.Equal(instrument.Id, trade.InstrumentId);
        Assert.Equal(2m, trade.PricingPointValue);
        Assert.Equal("USD", trade.PricingCurrency);
        Assert.Equal(ImportedAt, trade.CreatedAtUtc);
        Assert.Equal(ImportedAt, trade.UpdatedAtUtc);
        Assert.Equal(2, executions.Length);
        Assert.All(executions, execution =>
        {
            Assert.Null(execution.Commission);
            Assert.Null(execution.Fees);
            Assert.Equal("MNQU6", execution.BrokerSymbol);
        });
        Assert.Equal("BUY-1", executions[0].ExternalExecutionId);
        Assert.Equal("SELL-1", executions[1].ExternalExecutionId);
        Assert.Null(browse.TotalCosts);
        Assert.Equal(2m, browse.GrossPnL);
        Assert.Null(browse.NetPnL);
        Assert.Equal(2, identities.Length);
        Assert.All(identities, identity =>
        {
            Assert.Equal(accountId, identity.TradingAccountIdAtImport);
            Assert.Equal(tradeId, identity.TradeId);
            Assert.Equal(ImportedAt, identity.ImportedAtUtc);
        });

        ITradeDetailReader detailReader = database.ServiceProvider
            .GetRequiredService<ITradeDetailReader>();
        TradeDetail detail = Assert.IsType<TradeDetail>(
            await detailReader.GetByIdAsync(tradeId));
        Assert.Equal(2m, detail.GrossPnL);
        Assert.Null(detail.TotalCosts);
        Assert.Null(detail.NetPnL);
        Assert.All(detail.Executions, execution =>
        {
            Assert.Null(execution.Commission);
            Assert.Null(execution.Fees);
            Assert.Null(execution.TotalCosts);
        });

        ITradeListReader listReader = database.ServiceProvider
            .GetRequiredService<ITradeListReader>();
        TradeListItem listItem = Assert.Single((await listReader.GetPageAsync(
            new TradeListQuery(
                1,
                20,
                TradeListSortColumn.OpenedAtUtc,
                TradeListSortDirection.Descending))).Items);
        Assert.Equal(2m, listItem.GrossPnL);
        Assert.Null(listItem.TotalCosts);
        Assert.Null(listItem.NetPnL);
    }

    [Fact]
    public async Task ExactRetryReturnsNoChangesWithoutCreatingAnotherInstrument()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        TradovateImportPreparationResult preparation = Preparation(accountId);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();
        TradovateImportResult first = await store.ImportAsync(
            new TradovateImportRequest(preparation, ImportedAt));

        TradovateImportResult retry = await store.ImportAsync(
            new TradovateImportRequest(preparation, ImportedAt.AddMinutes(1)));

        Assert.Equal(TradovateImportStatus.NoChanges, retry.Status);
        Assert.Equal(0, retry.ImportedTradeCount);
        Assert.Equal(1, retry.SkippedDuplicateTradeCount);
        Assert.Equal(first.ImportedTradeIds, retry.DuplicateTradeIds);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.Trades.CountAsync());
        Assert.Equal(1, await context.Instruments.CountAsync());
        Assert.Equal(2, await context.TradovateImportedExecutions.CountAsync());
    }

    [Fact]
    public async Task PartialIdentityOverlapBlocksAndRollsBackEntireRequest()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();
        await store.ImportAsync(new TradovateImportRequest(
            Preparation(accountId), ImportedAt));
        TradovateImportPreparationResult overlap = Preparation(
            accountId,
            exitExternalId: "DIFFERENT-SELL");

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(overlap, ImportedAt.AddMinutes(1)));

        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(
            TradovateImportConflictCodes.DeduplicationConflict,
            result.ConflictCode);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.Trades.CountAsync());
        Assert.Equal(2, await context.TradovateImportedExecutions.CountAsync());
    }

    [Fact]
    public async Task ProposalRaceReusesOneCurrentInstrumentWithMatchingEconomics()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        Guid existingInstrumentId = await SeedInstrumentAsync(database, 0.50m);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(Preparation(accountId), ImportedAt));

        Assert.Equal(TradovateImportStatus.Imported, result.Status);
        Assert.Equal(0, result.CreatedInstrumentCount);
        Assert.Empty(result.CreatedInstrumentIds);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.Instruments.CountAsync());
        Assert.Equal(
            existingInstrumentId,
            (await context.Trades.AsNoTracking().SingleAsync()).InstrumentId);
    }

    [Fact]
    public async Task ProposalRaceWithConflictingEconomicsBlocksWithoutWrites()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        await SeedInstrumentAsync(database, 1.25m);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(Preparation(accountId), ImportedAt));

        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(
            TradovateImportConflictCodes.ReferenceDataChanged,
            result.ConflictCode);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
        Assert.Empty(await context.TradovateImportedExecutions.ToArrayAsync());
        Assert.Equal(1, await context.Instruments.CountAsync());
    }

    [Fact]
    public async Task ExistingInstrumentMaterialChangeAfterPreviewBlocksImport()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        Guid instrumentId = await SeedInstrumentAsync(database, 1.25m);
        TradovateImportPreparationResult stale = ExistingPreparation(
            Preparation(accountId), instrumentId, expectedTickValue: 0.50m);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(stale, ImportedAt));

        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(
            TradovateImportConflictCodes.ReferenceDataChanged,
            result.ConflictCode);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Fact]
    public async Task AccountRemovedAfterPreviewReturnsTypedBlockedResult()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        TradovateImportPreparationResult preparation = Preparation(accountId);
        await using (JournalDbContext deleteContext =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            deleteContext.TradingAccounts.Remove(
                await deleteContext.TradingAccounts.SingleAsync());
            await deleteContext.SaveChangesAsync();
        }
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(preparation, ImportedAt));

        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(
            TradovateImportConflictCodes.TradingAccountNotFound,
            result.ConflictCode);
    }

    [Fact]
    public async Task SameBrokerIdentitiesCanBeImportedToDifferentAccounts()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountA = await SeedAccountAsync(database);
        Guid accountB = await SeedAccountAsync(database);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();

        TradovateImportResult first = await store.ImportAsync(
            new TradovateImportRequest(Preparation(accountA), ImportedAt));
        TradovateImportResult second = await store.ImportAsync(
            new TradovateImportRequest(Preparation(accountB), ImportedAt.AddMinutes(1)));

        Assert.Equal(TradovateImportStatus.Imported, first.Status);
        Assert.Equal(TradovateImportStatus.Imported, second.Status);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.Trades.CountAsync());
        Assert.Equal(4, await context.TradovateImportedExecutions.CountAsync());
        Assert.Equal(1, await context.Instruments.CountAsync());
    }

    [Fact]
    public async Task MixedDuplicateAndNewCandidatesSkipsAndImportsAtomically()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();
        TradovateImportPreparationResult candidateA = Preparation(accountId);
        TradovateImportPreparationResult candidateB = Preparation(
            accountId, "BUY-2", "SELL-2");
        await store.ImportAsync(new TradovateImportRequest(candidateA, ImportedAt));
        var mixed = new TradovateImportPreparationResult(
            candidateA.AccountSnapshot,
            candidateA.InstrumentResolution,
            candidateA.PreparedExecutions.Concat(candidateB.PreparedExecutions),
            candidateA.PreparedCandidates.Concat(candidateB.PreparedCandidates),
            [],
            TradovateImportPreparationStatus.ReadyForPreview);

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(mixed, ImportedAt.AddMinutes(1)));

        Assert.Equal(TradovateImportStatus.Imported, result.Status);
        Assert.Equal(1, result.ImportedTradeCount);
        Assert.Equal(1, result.SkippedDuplicateTradeCount);
        Assert.Equal(0, result.CreatedInstrumentCount);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.Trades.CountAsync());
        Assert.Equal(4, await context.TradovateImportedExecutions.CountAsync());
    }

    [Fact]
    public async Task CandidateIdentitiesAcrossMultiplePersistedTradesBlockImport()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();
        TradovateImportPreparationResult candidateA = Preparation(
            accountId, "BUY-A", "SELL-A");
        TradovateImportPreparationResult candidateB = Preparation(
            accountId, "BUY-B", "SELL-B");
        await store.ImportAsync(new TradovateImportRequest(candidateA, ImportedAt));
        await store.ImportAsync(new TradovateImportRequest(
            candidateB, ImportedAt.AddMinutes(1)));
        TradovatePreparedExecution entry = candidateA.PreparedExecutions[0];
        TradovatePreparedExecution exit = candidateB.PreparedExecutions[1];
        TradovatePreparedTradeCandidate crossTrade = candidateA.PreparedCandidates[0] with
        {
            OrderedExecutions = [entry, exit],
        };
        var conflicting = new TradovateImportPreparationResult(
            candidateA.AccountSnapshot,
            candidateA.InstrumentResolution,
            [entry, exit],
            [crossTrade],
            [],
            TradovateImportPreparationStatus.ReadyForPreview);

        TradovateImportResult result = await store.ImportAsync(
            new TradovateImportRequest(conflicting, ImportedAt.AddMinutes(2)));

        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(
            TradovateImportConflictCodes.DeduplicationConflict,
            result.ConflictCode);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.Trades.CountAsync());
        Assert.Equal(4, await context.TradovateImportedExecutions.CountAsync());
    }

    [Fact]
    public async Task DeletingImportedTradeCascadesIdentitiesAndAllowsReimport()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        TradovateImportPreparationResult preparation = Preparation(accountId);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();
        TradovateImportResult first = await store.ImportAsync(
            new TradovateImportRequest(preparation, ImportedAt));

        ITradeDeletionStore deletionStore = database.ServiceProvider
            .GetRequiredService<ITradeDeletionStore>();
        Assert.NotNull(await deletionStore.DeleteAsync(first.ImportedTradeIds[0]));

        TradovateImportResult reimport = await store.ImportAsync(
            new TradovateImportRequest(preparation, ImportedAt.AddMinutes(1)));

        Assert.Equal(TradovateImportStatus.Imported, reimport.Status);
        Assert.Equal(0, reimport.CreatedInstrumentCount);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.Trades.CountAsync());
        Assert.Equal(2, await context.TradovateImportedExecutions.CountAsync());
    }

    [Fact]
    public async Task PreCancelledTokenPreventsImport()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ImportAsync(
            new TradovateImportRequest(Preparation(accountId), ImportedAt),
            source.Token));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Fact]
    public async Task PersistenceFailureAfterStagingRollsBackEveryImportOwnedWrite()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database);
        await using (JournalDbContext triggerContext =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            await triggerContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER ForceTradovateIdentityFailure
                BEFORE INSERT ON TradovateImportedExecutions
                BEGIN
                    SELECT RAISE(ABORT, 'forced test failure');
                END;
                """);
        }
        ITradovateImportStore store = database.ServiceProvider
            .GetRequiredService<ITradovateImportStore>();

        await Assert.ThrowsAsync<DbUpdateException>(() => store.ImportAsync(
            new TradovateImportRequest(Preparation(accountId), ImportedAt)));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Instruments.ToArrayAsync());
        Assert.Empty(await context.Trades.ToArrayAsync());
        Assert.Empty(await context.TradeExecutions.ToArrayAsync());
        Assert.Empty(await context.TradeBrowse.ToArrayAsync());
        Assert.Empty(await context.TradovateImportedExecutions.ToArrayAsync());
    }

    private static async Task<Guid> SeedAccountAsync(ReaderTestDatabase database)
    {
        Guid accountId = Guid.NewGuid();
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = accountId,
            Name = "Tradovate",
            AccountType = TradingAccountType.Personal,
            ProviderName = "Tradovate",
            ExternalAccountId = "SIM-1",
            Currency = "USD",
            IsActive = true,
            CreatedAtUtc = ImportedAt.AddDays(-1),
            UpdatedAtUtc = ImportedAt.AddDays(-1),
        });
        await context.SaveChangesAsync();
        return accountId;
    }

    private static async Task<Guid> SeedInstrumentAsync(
        ReaderTestDatabase database,
        decimal tickValue)
    {
        Guid instrumentId = Guid.NewGuid();
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.Instruments.Add(new InstrumentRecord
        {
            Id = instrumentId,
            Symbol = "MNQ",
            DisplayName = "Current MNQ",
            AssetClass = AssetClass.Futures,
            Exchange = "Current exchange",
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = tickValue,
            IsActive = false,
            CreatedAtUtc = ImportedAt.AddDays(-1),
            UpdatedAtUtc = ImportedAt.AddDays(-1),
        });
        await context.SaveChangesAsync();
        return instrumentId;
    }

    private static TradovateImportPreparationResult Preparation(
        Guid accountId,
        string entryExternalId = "BUY-1",
        string exitExternalId = "SELL-1")
    {
        var proposal = new TradovateInstrumentCreationProposal(
            "MNQ", "Micro Nasdaq", AssetClass.Futures, "CME", "USD",
            0.25m, 0.50m, ["MNQU6"], "Verified test profile");
        var resolution = new TradovateInstrumentResolutionResult(
            [new TradovateBrokerSymbolMapping(
                "MNQU6", "MNQ", TradovateInstrumentResolutionStatus.ProposedCreation,
                null, null, [])],
            [new TradovateCanonicalInstrumentResolution(
                "MNQ", ["MNQU6"], 0.25m,
                TradovateInstrumentResolutionStatus.ProposedCreation,
                null, null, [], proposal, [], "USD")],
            [],
            TradovateInstrumentResolutionOverallStatus.ReadyForPreview);
        TradovatePreparedExecution entry = Prepared(
            ExecutionSide.Buy, entryExternalId, 100m, ImportedAt.AddHours(-1));
        TradovatePreparedExecution exit = Prepared(
            ExecutionSide.Sell, exitExternalId, 101m, ImportedAt.AddMinutes(-59));
        var account = new TradovateImportAccountSnapshot(
            accountId, "Tradovate", TradingAccountType.Personal,
            "Tradovate", "SIM-1", "USD", true);
        var candidate = new TradovatePreparedTradeCandidate(
            "MNQU6", "MNQ", null, proposal, accountId, TradeDirection.Long,
            [entry, exit], entry.ExecutedAtUtc, exit.ExecutedAtUtc,
            entry.ExecutedAtUtc.ToOffset(TimeSpan.FromHours(-4)),
            exit.ExecutedAtUtc.ToOffset(TimeSpan.FromHours(-4)), [1]);
        return new TradovateImportPreparationResult(
            account, resolution, [entry, exit], [candidate], [],
            TradovateImportPreparationStatus.ReadyForPreview);
    }

    private static TradovateImportPreparationResult ExistingPreparation(
        TradovateImportPreparationResult proposed,
        Guid instrumentId,
        decimal expectedTickValue)
    {
        var snapshot = new TradovateExistingInstrumentSnapshot(
            "Preview MNQ",
            AssetClass.Futures,
            "Preview exchange",
            "USD",
            0.25m,
            expectedTickValue);
        var resolution = new TradovateInstrumentResolutionResult(
            [new TradovateBrokerSymbolMapping(
                "MNQU6", "MNQ", TradovateInstrumentResolutionStatus.ExistingInstrument,
                instrumentId, false, [])],
            [new TradovateCanonicalInstrumentResolution(
                "MNQ", ["MNQU6"], 0.25m,
                TradovateInstrumentResolutionStatus.ExistingInstrument,
                instrumentId, false, [instrumentId], null, [], "USD", snapshot)],
            [],
            TradovateInstrumentResolutionOverallStatus.ReadyForPreview);
        TradovatePreparedTradeCandidate candidate = proposed.PreparedCandidates[0] with
        {
            ExistingInstrumentId = instrumentId,
            InstrumentCreationProposal = null,
        };
        return new TradovateImportPreparationResult(
            proposed.AccountSnapshot,
            resolution,
            proposed.PreparedExecutions,
            [candidate],
            [],
            TradovateImportPreparationStatus.ReadyForPreview);
    }

    private static TradovatePreparedExecution Prepared(
        ExecutionSide side,
        string externalId,
        decimal price,
        DateTimeOffset utc) =>
        new(
            "MNQU6", side, externalId, 1m, price,
            DateTime.SpecifyKind(utc.DateTime, DateTimeKind.Unspecified),
            TradingTimePolicy.TradovateSourceTimeZoneId,
            utc,
            utc.ToOffset(TimeSpan.FromHours(-4)).DateTime,
            TimeSpan.FromHours(-4),
            TradingTimePolicy.TradingTimeZoneId,
            [1], [2]);
}
