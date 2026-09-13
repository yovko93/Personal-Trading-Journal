using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

[Collection(ScreenshotPersistenceCollection.Name)]
public sealed class TradeScreenshotDeletionStoreTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteAsyncRejectsEmptyIdentifiersBeforeDatabaseWork(
        bool emptyTradeId)
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        ITradeScreenshotDeletionStore store = GetDeletionStore(database);

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => store.DeleteAsync(
                emptyTradeId ? Guid.Empty : Guid.NewGuid(),
                emptyTradeId ? Guid.NewGuid() : Guid.Empty));
    }

    [Fact]
    public async Task DeleteAsyncRemovesOnlyMatchingIdAndReturnsExactOpaqueInfo()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        Guid deletedId = Guid.Parse("00000000-0000-0000-0000-000000000201");
        Guid retainedId = Guid.Parse("00000000-0000-0000-0000-000000000202");
        const string deletedStorageKey = "opaque/provider/deleted.webp";
        const string duplicateFileName = "same-original-name.png";
        ITradeScreenshotStore metadataStore = GetMetadataStore(database);
        await metadataStore.AddAsync(ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            deletedId,
            TradeScreenshotType.Entry,
            deletedStorageKey,
            duplicateFileName));
        await metadataStore.AddAsync(ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            retainedId,
            TradeScreenshotType.Exit,
            "opaque/provider/retained.png",
            duplicateFileName));

        TradeScreenshotDeletionInfo? result =
            await GetDeletionStore(database).DeleteAsync(tradeId, deletedId);

        Assert.NotNull(result);
        Assert.Equal(deletedId, result.Id);
        Assert.Equal(tradeId, result.TradeId);
        Assert.Equal(deletedStorageKey, result.StorageKey);
        await using JournalDbContext freshContext =
            await database.ContextFactory.CreateDbContextAsync();
        List<TradeScreenshotRecord> records = await freshContext.TradeScreenshots
            .AsNoTracking()
            .ToListAsync();
        TradeScreenshotRecord retained = Assert.Single(records);
        Assert.Equal(retainedId, retained.Id);
        Assert.Equal(duplicateFileName, retained.FileName);
    }

    [Fact]
    public async Task DeleteAsyncReturnsNullForScreenshotOwnedByDifferentTrade()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Guid ownerTradeId =
            await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        Guid otherTradeId =
            await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        TradeScreenshot screenshot =
            ScreenshotPersistenceTestData.CreateScreenshot(ownerTradeId);
        await GetMetadataStore(database).AddAsync(screenshot);

        TradeScreenshotDeletionInfo? result = await GetDeletionStore(database)
            .DeleteAsync(otherTradeId, screenshot.Id);

        Assert.Null(result);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradeScreenshots
            .AsNoTracking()
            .AnyAsync(record => record.Id == screenshot.Id));
    }

    [Fact]
    public async Task DeleteAsyncReturnsNullForMissingScreenshot()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);

        TradeScreenshotDeletionInfo? result = await GetDeletionStore(database)
            .DeleteAsync(tradeId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsyncPropagatesPreCancelledDatabaseOperation()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        TradeScreenshot screenshot =
            ScreenshotPersistenceTestData.CreateScreenshot(tradeId);
        await GetMetadataStore(database).AddAsync(screenshot);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GetDeletionStore(database).DeleteAsync(
                tradeId,
                screenshot.Id,
                cancellationSource.Token));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradeScreenshots
            .AsNoTracking()
            .AnyAsync(record => record.Id == screenshot.Id));
    }

    private static ITradeScreenshotDeletionStore GetDeletionStore(
        ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeScreenshotDeletionStore>();

    private static ITradeScreenshotStore GetMetadataStore(
        ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeScreenshotStore>();
}
