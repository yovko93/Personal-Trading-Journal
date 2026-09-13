using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

[Collection(ScreenshotPersistenceCollection.Name)]
public sealed class TradeScreenshotStoreTests
{
    [Fact]
    public async Task AddAsyncPersistsExactScreenshotThroughFreshContext()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        Guid screenshotId = Guid.Parse("00000000-0000-0000-0000-000000000101");
        DateTimeOffset capturedAtUtc =
            new(2026, 8, 14, 13, 45, 0, TimeSpan.Zero);
        DateTimeOffset createdAtUtc =
            new(2026, 9, 13, 10, 30, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddMinutes(5);
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            screenshotId,
            TradeScreenshotType.Management,
            "opaque/provider/key with spaces.webp",
            "NQ management.webp",
            capturedAtUtc,
            "2m",
            "Managing the open position.",
            createdAtUtc,
            updatedAtUtc);

        await GetStore(database).AddAsync(screenshot);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        TradeScreenshotRecord record = await context.TradeScreenshots
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(screenshotId, record.Id);
        Assert.Equal(tradeId, record.TradeId);
        Assert.Equal(TradeScreenshotType.Management, record.Type);
        Assert.Equal("opaque/provider/key with spaces.webp", record.StorageKey);
        Assert.Equal("NQ management.webp", record.FileName);
        Assert.Equal(capturedAtUtc, record.CapturedAtUtc);
        Assert.Equal("2m", record.Timeframe);
        Assert.Equal("Managing the open position.", record.Description);
        Assert.Equal(createdAtUtc, record.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public async Task AddAsyncRejectsNullScreenshot()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => GetStore(database).AddAsync(null!));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.TradeScreenshots.AnyAsync());
    }

    [Fact]
    public async Task AddAsyncPropagatesForeignKeyFailureForMissingTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(
            Guid.NewGuid());

        await Assert.ThrowsAsync<DbUpdateException>(
            () => GetStore(database).AddAsync(screenshot));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.TradeScreenshots.AnyAsync());
    }

    [Fact]
    public async Task AddAsyncPropagatesPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(tradeId);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GetStore(database).AddAsync(screenshot, cancellationSource.Token));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.TradeScreenshots.AnyAsync());
    }

    [Fact]
    public async Task AddAsyncAllowsDuplicateOriginalFileNameAndMetadata()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        Guid firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        TradeScreenshot first = ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            screenshotId: firstId);
        TradeScreenshot second = ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            screenshotId: secondId);
        ITradeScreenshotStore store = GetStore(database);

        await store.AddAsync(first);
        await store.AddAsync(second);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        List<TradeScreenshotRecord> records = await context.TradeScreenshots
            .AsNoTracking()
            .OrderBy(record => record.Id)
            .ToListAsync();
        Assert.Equal([firstId, secondId], records.Select(record => record.Id));
        Assert.All(records, record => Assert.Equal("nq-entry.png", record.FileName));
        Assert.All(
            records,
            record => Assert.Equal("opaque/screenshots/chart-01.png", record.StorageKey));
    }

    private static ITradeScreenshotStore GetStore(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeScreenshotStore>();
}
