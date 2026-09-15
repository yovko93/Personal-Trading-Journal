using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradeMistakeStoreTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsyncPersistsAndGetByIdAsyncRehydratesEveryField()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await TradeMistakeReaderTests.PersistTradeAsync(database);
        Guid catalogId = Guid.NewGuid();
        await TradeMistakeReaderTests.SeedAsync(
            database,
            [TradeMistakeReaderTests.Mistake(catalogId, "FOMO", true)],
            []);
        Guid associationId = Guid.NewGuid();
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(5);
        TradeMistake association = TradeMistake.Rehydrate(
            associationId,
            tradeId,
            catalogId,
            "  Entered without confirmation.  ",
            CreatedAtUtc,
            updatedAtUtc);
        ITradeMistakeStore store = GetStore(database);

        await store.AddAsync(association);
        TradeMistake loaded = Assert.IsType<TradeMistake>(
            await store.GetByIdAsync(association.Id));

        Assert.Equal(associationId, loaded.Id);
        Assert.Equal(tradeId, loaded.TradeId);
        Assert.Equal(catalogId, loaded.TradingMistakeId);
        Assert.Equal("Entered without confirmation.", loaded.Note);
        Assert.Equal(CreatedAtUtc, loaded.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, loaded.UpdatedAtUtc);
    }

    [Fact]
    public async Task ExistsAsyncMatchesOnlyExactTradeAndCatalogPair()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await TradeMistakeReaderTests.PersistTradeAsync(database);
        Guid catalogId = Guid.NewGuid();
        await TradeMistakeReaderTests.SeedAsync(
            database,
            [TradeMistakeReaderTests.Mistake(catalogId, "FOMO", true)],
            [TradeMistakeReaderTests.Association(
                Guid.NewGuid(), tradeId, catalogId, null)]);
        ITradeMistakeStore store = GetStore(database);

        Assert.True(await store.ExistsAsync(tradeId, catalogId));
        Assert.False(await store.ExistsAsync(Guid.NewGuid(), catalogId));
        Assert.False(await store.ExistsAsync(tradeId, Guid.NewGuid()));
    }

    [Fact]
    public async Task DatabaseUniqueConstraintRejectsDuplicateTradeCatalogPair()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await TradeMistakeReaderTests.PersistTradeAsync(database);
        Guid catalogId = Guid.NewGuid();
        await TradeMistakeReaderTests.SeedAsync(
            database,
            [TradeMistakeReaderTests.Mistake(catalogId, "FOMO", true)],
            []);
        ITradeMistakeStore store = GetStore(database);
        await store.AddAsync(new TradeMistake(
            tradeId, catalogId, null, CreatedAtUtc));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => store.AddAsync(new TradeMistake(
                tradeId, catalogId, "Duplicate", CreatedAtUtc.AddMinutes(1))));
    }

    [Fact]
    public async Task RemoveAsyncDeletesOnlyTargetAssociation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await TradeMistakeReaderTests.PersistTradeAsync(database);
        Guid firstCatalogId = Guid.NewGuid();
        Guid secondCatalogId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        Guid remainingId = Guid.NewGuid();
        await TradeMistakeReaderTests.SeedAsync(
            database,
            [
                TradeMistakeReaderTests.Mistake(firstCatalogId, "FOMO", true),
                TradeMistakeReaderTests.Mistake(secondCatalogId, "Overtrading", false),
            ],
            [
                TradeMistakeReaderTests.Association(
                    targetId, tradeId, firstCatalogId, null),
                TradeMistakeReaderTests.Association(
                    remainingId, tradeId, secondCatalogId, null),
            ]);

        await GetStore(database).RemoveAsync(targetId);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        List<Guid> ids = await context.TradeMistakes
            .AsNoTracking()
            .Select(record => record.Id)
            .ToListAsync();
        Assert.Equal([remainingId], ids);
    }

    [Fact]
    public async Task GetByIdAndRemoveHandleMissingAssociation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeMistakeStore store = GetStore(database);
        Guid missingId = Guid.NewGuid();

        Assert.Null(await store.GetByIdAsync(missingId));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.RemoveAsync(missingId));
    }

    [Fact]
    public async Task OperationsPropagatePreCancelledTokenWithoutPersistingChanges()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await TradeMistakeReaderTests.PersistTradeAsync(database);
        Guid catalogId = Guid.NewGuid();
        Guid associationId = Guid.NewGuid();
        await TradeMistakeReaderTests.SeedAsync(
            database,
            [TradeMistakeReaderTests.Mistake(catalogId, "FOMO", true)],
            [TradeMistakeReaderTests.Association(
                associationId, tradeId, catalogId, null)]);
        ITradeMistakeStore store = GetStore(database);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.ExistsAsync(tradeId, catalogId, cancellationSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetByIdAsync(associationId, cancellationSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.AddAsync(
                new TradeMistake(
                    tradeId, catalogId, "Duplicate", CreatedAtUtc),
                cancellationSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.RemoveAsync(associationId, cancellationSource.Token));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradeMistakes.AnyAsync(
            record => record.Id == associationId));
    }

    private static ITradeMistakeStore GetStore(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeMistakeStore>();
}
