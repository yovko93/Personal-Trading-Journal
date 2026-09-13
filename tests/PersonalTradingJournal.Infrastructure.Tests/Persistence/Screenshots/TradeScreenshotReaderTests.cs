using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

[Collection(ScreenshotPersistenceCollection.Name)]
public sealed class TradeScreenshotReaderTests
{
    [Fact]
    public async Task GetForTradeAsyncRejectsEmptyTradeId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => GetReader(database).GetForTradeAsync(Guid.Empty));

        Assert.Equal("tradeId", exception.ParamName);
    }

    [Fact]
    public async Task GetForTradeAsyncReturnsEmptyForMissingTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();

        IReadOnlyList<TradeScreenshotListItem> items =
            await GetReader(database).GetForTradeAsync(Guid.NewGuid());

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetForTradeAsyncReturnsEmptyForExistingTradeWithoutScreenshots()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);

        IReadOnlyList<TradeScreenshotListItem> items =
            await GetReader(database).GetForTradeAsync(tradeId);

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetForTradeAsyncProjectsExactMetadataWithoutStorageAddressing()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        Guid screenshotId = Guid.Parse("00000000-0000-0000-0000-000000000111");
        DateTimeOffset capturedAtUtc =
            new(2026, 8, 14, 13, 45, 0, TimeSpan.Zero);
        DateTimeOffset createdAtUtc =
            new(2026, 9, 13, 11, 0, 0, TimeSpan.Zero);
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            screenshotId,
            TradeScreenshotType.PostTrade,
            "opaque/provider/internal-key.jpeg",
            "review.jpeg",
            capturedAtUtc,
            "15m",
            "Post-trade market context.",
            createdAtUtc,
            createdAtUtc.AddMinutes(5));
        await GetStore(database).AddAsync(screenshot);

        TradeScreenshotListItem item = Assert.Single(
            await GetReader(database).GetForTradeAsync(tradeId));

        Assert.Equal(screenshotId, item.Id);
        Assert.Equal(tradeId, item.TradeId);
        Assert.Equal(TradeScreenshotType.PostTrade, item.Type);
        Assert.Equal("review.jpeg", item.FileName);
        Assert.Equal(capturedAtUtc, item.CapturedAtUtc);
        Assert.Equal("15m", item.Timeframe);
        Assert.Equal("Post-trade market context.", item.Description);
        Assert.Equal(createdAtUtc, item.CreatedAtUtc);
        Assert.Null(typeof(TradeScreenshotListItem).GetProperty("StorageKey"));
        Assert.Null(typeof(TradeScreenshotListItem).GetProperty("UpdatedAtUtc"));
    }

    [Fact]
    public async Task GetForTradeAsyncOrdersCapturedFirstThenUncapturedChronology()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        Guid a = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        Guid b = Guid.Parse("00000000-0000-0000-0000-00000000000b");
        Guid c = Guid.Parse("00000000-0000-0000-0000-00000000000c");
        Guid d = Guid.Parse("00000000-0000-0000-0000-00000000000d");
        ITradeScreenshotStore store = GetStore(database);
        await store.AddAsync(CreateAt(tradeId, a, Utc(10, 5), Utc(12, 0)));
        await store.AddAsync(CreateAt(tradeId, b, null, Utc(9, 0)));
        await store.AddAsync(CreateAt(tradeId, c, Utc(9, 45), Utc(12, 30)));
        await store.AddAsync(CreateAt(tradeId, d, null, Utc(8, 0)));

        IReadOnlyList<TradeScreenshotListItem> items =
            await GetReader(database).GetForTradeAsync(tradeId);

        Assert.Equal([c, a, d, b], items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetForTradeAsyncUsesCreatedTimeThenIdAsTieBreakers()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        Guid earlyCreated = Guid.Parse("00000000-0000-0000-0000-000000000003");
        Guid lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        DateTimeOffset capturedAtUtc = Utc(10, 0);
        DateTimeOffset sharedCreatedAtUtc = Utc(12, 0);
        ITradeScreenshotStore store = GetStore(database);
        await store.AddAsync(
            CreateAt(tradeId, higherId, capturedAtUtc, sharedCreatedAtUtc));
        await store.AddAsync(
            CreateAt(tradeId, earlyCreated, capturedAtUtc, Utc(11, 0)));
        await store.AddAsync(
            CreateAt(tradeId, lowerId, capturedAtUtc, sharedCreatedAtUtc));

        IReadOnlyList<TradeScreenshotListItem> items =
            await GetReader(database).GetForTradeAsync(tradeId);

        Assert.Equal(
            [earlyCreated, lowerId, higherId],
            items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetForTradeAsyncIncludesScreenshotsWithInactiveReferences()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(
            database,
            referencesAreActive: false);
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(tradeId);
        await GetStore(database).AddAsync(screenshot);

        TradeScreenshotListItem item = Assert.Single(
            await GetReader(database).GetForTradeAsync(tradeId));

        Assert.Equal(screenshot.Id, item.Id);
    }

    [Fact]
    public async Task GetForTradeAsyncUsesFreshContextAndSeesLaterCommit()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        ITradeScreenshotReader reader = GetReader(database);
        Assert.Empty(await reader.GetForTradeAsync(tradeId));
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(tradeId);
        await GetStore(database).AddAsync(screenshot);

        TradeScreenshotListItem item = Assert.Single(
            await reader.GetForTradeAsync(tradeId));

        Assert.Equal(screenshot.Id, item.Id);
    }

    [Fact]
    public async Task GetForTradeAsyncPropagatesPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GetReader(database).GetForTradeAsync(
                Guid.NewGuid(),
                cancellationSource.Token));
    }

    private static TradeScreenshot CreateAt(
        Guid tradeId,
        Guid screenshotId,
        DateTimeOffset? capturedAtUtc,
        DateTimeOffset createdAtUtc)
    {
        return ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            screenshotId,
            capturedAtUtc: capturedAtUtc,
            createdAtUtc: createdAtUtc);
    }

    private static DateTimeOffset Utc(int hour, int minute) =>
        new(2026, 9, 13, hour, minute, 0, TimeSpan.Zero);

    private static ITradeScreenshotReader GetReader(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeScreenshotReader>();

    private static ITradeScreenshotStore GetStore(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeScreenshotStore>();
}
