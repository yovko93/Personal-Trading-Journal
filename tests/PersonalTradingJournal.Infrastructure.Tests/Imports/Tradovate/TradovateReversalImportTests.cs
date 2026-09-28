using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

public sealed class TradovateReversalImportTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PreviewConfirmationAndReplayConserveFillAcrossTwoTrades(bool startsShort, bool tiedClosures)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var workflow = await PrepareAsync(database, startsShort, tiedClosures);
        decimal sign = startsShort ? -1m : 1m;

        Assert.True(workflow.Preview.IsReadyForConfirmation);
        Assert.Equal(4, workflow.Reconstruction.Executions.Count);
        Assert.Equal(5, workflow.Preparation.PreparedCandidates.Sum(item => item.OrderedExecutions.Count));
        Assert.Equal([20m, 1m], workflow.Preview.Trades.Select(item => item.OpeningQuantity));
        Assert.Equal([100m, 102m], workflow.Preview.Trades.Select(item => item.WeightedAverageEntryPrice));
        Assert.Equal(new decimal?[] { 101.95m, 101m },
            workflow.Preview.Trades.Select(item => item.WeightedAverageExitPrice));
        Assert.Equal(new decimal?[] { sign * 78m, sign * 2m }, workflow.Preview.Trades.Select(item => item.SourceReportedPnL));
        Assert.Equal(startsShort ? [TradeDirection.Short, TradeDirection.Long] : [TradeDirection.Long, TradeDirection.Short],
            workflow.Preview.Trades.Select(item => item.Direction));

        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            Assert.Empty(await context.Trades.ToArrayAsync());
            Assert.Empty(await context.Instruments.ToArrayAsync());
        }

        ITradovateImportStore store = database.ServiceProvider.GetRequiredService<ITradovateImportStore>();
        var useCase = new ImportTradovateTradesUseCase(workflow.Service, store, new FixedTimeProvider());
        TradovateImportResult imported = await useCase.ImportAsync(workflow.Reconstruction,
            workflow.Preparation.InstrumentResolution, workflow.AccountId);

        Assert.Equal(TradovateImportStatus.Imported, imported.Status);
        Assert.Equal(2, imported.ImportedTradeCount);
        Assert.Equal(1, imported.CreatedInstrumentCount);

        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            TradeBrowseRecord[] trades = await context.TradeBrowse.OrderBy(item => item.OpenedAtUtc).ToArrayAsync();
            Assert.Equal(new decimal?[] { sign * 78m, sign * 2m }, trades.Select(item => item.GrossPnL));
            Assert.Equal(new decimal?[] { 101.95m, 101m }, trades.Select(item => item.AverageExitPrice));
            Assert.All(trades, trade =>
            {
                Assert.Equal(TradeStatus.Closed, trade.Status);
                Assert.Equal(0m, trade.OpenQuantity);
                Assert.Null(trade.NetPnL);
                Assert.Null(trade.TotalCosts);
            });
            TradeExecutionRecord[] executions = await context.TradeExecutions.ToArrayAsync();
            Assert.Equal(5, executions.Length);
            Assert.All(executions, execution =>
            {
                Assert.Null(execution.Commission);
                Assert.Null(execution.Fees);
            });
            TradovateImportedExecutionRecord[] ledger = await context.TradovateImportedExecutions.ToArrayAsync();
            foreach (TradovateReconstructedExecution source in workflow.Reconstruction.Executions)
            {
                TradovateImportedExecutionRecord[] parts = ledger.Where(item =>
                    item.ExternalExecutionId == source.ExternalFillId && item.Side == source.Side).ToArray();
                Assert.Equal(source.Quantity, parts.Sum(item => item.AllocatedQuantity));
                Assert.All(parts, part => Assert.Equal(source.Quantity, part.SourceFillQuantity));
            }
            TradovateImportedExecutionRecord[] crossing = ledger.Where(item => item.ExternalExecutionId == "CROSS")
                .OrderBy(item => item.AllocationIndex).ToArray();
            Assert.Equal(new decimal?[] { 19m, 1m }, crossing.Select(item => item.AllocatedQuantity));
            Assert.Equal([0, 1], crossing.Select(item => item.AllocationIndex));
            Assert.Equal(2, crossing.Select(item => item.TradeId).Distinct().Count());
            Assert.All(crossing, item => Assert.Equal(102m, item.SourceFillPrice));
            Assert.Equal(crossing[0].SourceFillExecutedAtUtc, crossing[1].SourceFillExecutedAtUtc);
        }

        // Rebuild against the now-existing Instrument, as Desktop does on a new file selection.
        TradovateInstrumentResolutionResult resolution = await new TradovateInstrumentResolver(
            database.ServiceProvider.GetRequiredService<IInstrumentReader>()).ResolveAsync(workflow.Reconstruction);
        TradovateImportResult replay = await useCase.ImportAsync(workflow.Reconstruction, resolution, workflow.AccountId);
        Assert.Equal(TradovateImportStatus.NoChanges, replay.Status);
        Assert.Equal(2, replay.SkippedDuplicateTradeCount);
        await using JournalDbContext fresh = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await fresh.Trades.CountAsync());
        Assert.Equal(5, await fresh.TradeExecutions.CountAsync());
        Assert.Equal(5, await fresh.TradovateImportedExecutions.CountAsync());
        Assert.Equal(imported.ImportedTradeIds.Order(), (await fresh.Trades.Select(item => item.Id).ToArrayAsync()).Order());
    }

    [Fact]
    public async Task IncompleteReversalPairIsBlockedWithoutPersistingAnything()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var workflow = await PrepareAsync(database);
        var partial = WithCandidates(workflow.Preparation, [workflow.Preparation.PreparedCandidates[0]]);
        TradovateImportResult result = await database.ServiceProvider.GetRequiredService<ITradovateImportStore>()
            .ImportAsync(new TradovateImportRequest(partial, Now));
        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(TradovateImportConflictCodes.DeduplicationConflict, result.ConflictCode);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
        Assert.Empty(await context.TradovateImportedExecutions.ToArrayAsync());
        Assert.Empty(await context.Instruments.ToArrayAsync());
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("time")]
    public async Task ChangedSourceFillReplayIsBlocked(string change)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var workflow = await PrepareAsync(database);
        ITradovateImportStore store = database.ServiceProvider.GetRequiredService<ITradovateImportStore>();
        await store.ImportAsync(new TradovateImportRequest(workflow.Preparation, Now));
        TradovatePreparedTradeCandidate[] changed = workflow.Preparation.PreparedCandidates.Select(candidate => candidate with
        {
            OrderedExecutions = candidate.OrderedExecutions.Select(execution => execution.ExternalFillId != "CROSS"
                ? execution
                : change switch
                {
                    "quantity" => execution with { Quantity = execution.Quantity * 2m, SourceFillQuantity = 40m },
                    "price" => execution with { Price = execution.Price + 1m },
                    _ => execution with { ExecutedAtUtc = execution.ExecutedAtUtc.AddSeconds(1) },
                }).ToArray(),
        }).ToArray();
        TradovateImportResult result = await store.ImportAsync(new TradovateImportRequest(
            WithCandidates(workflow.Preparation, changed), Now));
        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(TradovateImportConflictCodes.DeduplicationConflict, result.ConflictCode);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.Trades.CountAsync());
        Assert.Equal(5, await context.TradovateImportedExecutions.CountAsync());
    }

    [Fact]
    public async Task AllocationWriteFailureRollsBackBothTradesAndProposedInstrument()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var workflow = await PrepareAsync(database);
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectOpeningAllocation BEFORE INSERT ON TradovateImportedExecutions
                WHEN NEW.AllocationIndex = 1
                BEGIN SELECT RAISE(ABORT, 'Synthetic allocation failure'); END;
                """);
        }
        ITradovateImportStore store = database.ServiceProvider.GetRequiredService<ITradovateImportStore>();
        await Assert.ThrowsAsync<DbUpdateException>(() => store.ImportAsync(
            new TradovateImportRequest(workflow.Preparation, Now)));
        await using JournalDbContext fresh = await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await fresh.Trades.ToArrayAsync());
        Assert.Empty(await fresh.TradeExecutions.ToArrayAsync());
        Assert.Empty(await fresh.TradeBrowse.ToArrayAsync());
        Assert.Empty(await fresh.TradovateImportedExecutions.ToArrayAsync());
        Assert.Empty(await fresh.Instruments.ToArrayAsync());
    }

    [Fact]
    public async Task ReplayAfterDeletingOnlyOneReversalTradeBlocksPartialFillOverlap()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var workflow = await PrepareAsync(database);
        ITradovateImportStore store = database.ServiceProvider.GetRequiredService<ITradovateImportStore>();
        var request = new TradovateImportRequest(workflow.Preparation, Now);
        TradovateImportResult imported = await store.ImportAsync(request);
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            context.Trades.Remove(await context.Trades.SingleAsync(trade => trade.Id == imported.ImportedTradeIds[1]));
            await context.SaveChangesAsync();
        }
        TradovateImportResult replay = await store.ImportAsync(request);
        Assert.Equal(TradovateImportStatus.Blocked, replay.Status);
        Assert.Equal(TradovateImportConflictCodes.DeduplicationConflict, replay.ConflictCode);
        await using JournalDbContext fresh = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await fresh.Trades.CountAsync());
        Assert.Equal(3, await fresh.TradeExecutions.CountAsync());
        Assert.Equal(3, await fresh.TradovateImportedExecutions.CountAsync());
    }

    private static TradovateImportPreparationResult WithCandidates(
        TradovateImportPreparationResult original, IReadOnlyList<TradovatePreparedTradeCandidate> candidates) =>
        new(original.AccountSnapshot, original.InstrumentResolution, original.PreparedExecutions,
            candidates, original.Diagnostics, original.Status);

    private static async Task<(Guid AccountId, TradovateExecutionReconstructionResult Reconstruction,
        TradovateImportPreparationService Service, TradovateImportPreparationResult Preparation,
        TradovateImportPreview Preview)> PrepareAsync(ReaderTestDatabase database, bool startsShort = false, bool tiedClosures = false)
    {
        var account = new TradingAccount("Reversal test", TradingAccountType.Personal,
            "Tradovate", "SYNTHETIC", "USD", 10000m, Now.AddDays(-1));
        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        string csv = startsShort ? TradovateCsvFixtures.ShortReversal : TradovateCsvFixtures.Reversal;
        if (tiedClosures) csv = csv.Replace("16:10:00", "16:30:21", StringComparison.Ordinal);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        TradovateCsvParseResult parsed = await new TradovateCsvParser().ParseAsync(stream);
        TradovateExecutionReconstructionResult reconstruction = new TradovateExecutionReconstructor().Reconstruct(parsed);
        Assert.True(reconstruction.IsEligibleForAutomaticImport);
        var resolver = new TradovateInstrumentResolver(database.ServiceProvider.GetRequiredService<IInstrumentReader>());
        TradovateInstrumentResolutionResult resolution = await resolver.ResolveAsync(reconstruction);
        var service = new TradovateImportPreparationService(database.ServiceProvider.GetRequiredService<ITradingAccountReader>());
        TradovateImportPreparationResult preparation = await service.PrepareAsync(reconstruction, resolution, account.Id);
        TradovateImportPreview preview = new TradovateImportPreviewBuilder().Build("synthetic-reversal.csv", parsed, reconstruction, preparation);
        return (account.Id, reconstruction, service, preparation, preview);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
