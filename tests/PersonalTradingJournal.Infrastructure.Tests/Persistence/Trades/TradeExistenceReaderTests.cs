using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

[Collection(ScreenshotPersistenceCollection.Name)]
public sealed class TradeExistenceReaderTests
{
    [Fact]
    public async Task ExistsAsyncRejectsEmptyTradeId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeExistenceReader reader = GetReader(database);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => reader.ExistsAsync(Guid.Empty));

        Assert.Equal("tradeId", exception.ParamName);
    }

    [Fact]
    public async Task ExistsAsyncReturnsTrueForExistingTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);

        bool exists = await GetReader(database).ExistsAsync(tradeId);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsyncReturnsFalseForMissingTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();

        bool exists = await GetReader(database).ExistsAsync(Guid.NewGuid());

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsyncUsesFreshContextAndSeesLaterCommit()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeExistenceReader reader = GetReader(database);
        Guid tradeId = Guid.NewGuid();
        Assert.False(await reader.ExistsAsync(tradeId));
        await ScreenshotPersistenceTestData.PersistTradeAsync(database, tradeId: tradeId);

        bool exists = await reader.ExistsAsync(tradeId);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsyncPropagatesPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeExistenceReader reader = GetReader(database);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.ExistsAsync(Guid.NewGuid(), cancellationSource.Token));
    }

    private static ITradeExistenceReader GetReader(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeExistenceReader>();
}
