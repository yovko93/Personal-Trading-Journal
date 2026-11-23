using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Trades;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeWorkspacePersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DescriptionAndMultipleScreenshotsCommitTogetherWithoutChangingEconomics()
    {
        await using var fixture = await ReaderTestDatabase.CreateAsync();
        var command = await Prepare(fixture);
        var id = await UseCase(fixture).ExecuteAsync(command);
        var detail = await fixture.ServiceProvider.GetRequiredService<ITradeDetailReader>().GetByIdAsync(id);
        Assert.Equal("Optional Trade description 🙂\nSecond line", detail!.Description);
        Assert.Equal(10m, detail.GrossPnL); Assert.Equal(7m, detail.NetPnL);
        var store = fixture.ServiceProvider.GetRequiredService<ITradeMutationStore>();
        var trade = (await store.GetByIdAsync(id))!;
        trade.SetDescription("Edited independently", Now);
        await store.SaveAsync(trade);
        Assert.Equal("Edited independently", (await store.GetByIdAsync(id))!.Description);
        var screenshots = await fixture.ServiceProvider.GetRequiredService<ITradeScreenshotReader>().GetForTradeAsync(id);
        Assert.Equal(2, screenshots.Count);
        Assert.All(screenshots, s => { Assert.Equal(id, s.TradeId); Assert.Null(s.Description); Assert.Null(s.CapturedAtUtc); });
        foreach (var screenshot in screenshots)
        {
            var content = await fixture.ServiceProvider.GetRequiredService<ITradeScreenshotContentReader>().OpenAsync(screenshot.Id);
            await using var stream = content!.Content;
            using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
            Assert.Equal(new byte[] { 1, 2, 3 }, bytes.ToArray());
        }
        await using var db = fixture.ContextFactory.CreateDbContext();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScreenshotInsertFailureOrCancellationLeavesNoTradeMetadataOrFiles(bool cancel)
    {
        await using var fixture = await ReaderTestDatabase.CreateAsync();
        var command = await Prepare(fixture);
        var files = new TrackingFiles(fixture.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>());
        using var cancellation = new CancellationTokenSource();
        await using var db = fixture.ContextFactory.CreateDbContext();
        if (!cancel)
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectSyntheticScreenshot BEFORE INSERT ON TradeScreenshots WHEN NEW.Timeframe = '15m' BEGIN SELECT RAISE(ABORT, 'synthetic screenshot failure'); END;");
        var atomic = new CancellationStore(new TradeStore(fixture.ContextFactory), cancellation, cancel);
        var useCase = UseCase(fixture, files, atomic);
        await Assert.ThrowsAnyAsync<Exception>(() => useCase.ExecuteAsync(command, cancellation.Token));
        Assert.Equal(2, files.Keys.Count); Assert.Equal(files.Keys, files.Deleted);
        foreach (string key in files.Keys) Assert.Null(await files.Inner.OpenReadAsync(key));
        Assert.Empty(await db.Trades.ToArrayAsync()); Assert.Empty(await db.TradeExecutions.ToArrayAsync());
        Assert.Empty(await db.TradeScreenshots.ToArrayAsync()); Assert.Empty(await db.TradeBrowse.ToArrayAsync());
    }

    [Fact]
    public async Task NoDescriptionOrScreenshotsPreservesOldManualCreateBehavior()
    {
        await using var fixture = await ReaderTestDatabase.CreateAsync();
        var command = (await Prepare(fixture)) with { Description = null, Screenshots = null };
        var id = await UseCase(fixture).ExecuteAsync(command);
        Assert.Null((await fixture.ServiceProvider.GetRequiredService<ITradeDetailReader>().GetByIdAsync(id))!.Description);
        Assert.Empty(await fixture.ServiceProvider.GetRequiredService<ITradeScreenshotReader>().GetForTradeAsync(id));
    }

    private static CreateManualTradeUseCase UseCase(ReaderTestDatabase f, ITradeScreenshotFileStorage? files = null, IManualTradeScreenshotStore? atomic = null) =>
        new(f.ServiceProvider.GetRequiredService<ITradingAccountStore>(), f.ServiceProvider.GetRequiredService<IInstrumentStore>(),
            f.ServiceProvider.GetRequiredService<ITradingSetupStore>(), f.ServiceProvider.GetRequiredService<ITradeStore>(),
            new Clock(), files ?? f.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>(),
            atomic ?? f.ServiceProvider.GetRequiredService<IManualTradeScreenshotStore>());

    private static async Task<CreateManualTradeCommand> Prepare(ReaderTestDatabase f)
    {
        var account = new TradingAccount("Synthetic only", TradingAccountType.Demo, null, null, "USD", 1000, Now);
        var instrument = new Instrument("SYN", "Synthetic", AssetClass.Futures, null, "USD", 1, 1, Now);
        await f.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await f.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        return new(account.Id, instrument.Id, null, TradeDirection.Long, 1,
            new(Now, 100, 1, 0), new(Now.AddMinutes(1), 110, 2, 0), "Optional Trade description 🙂\nSecond line",
            [new(TradeScreenshotType.Entry, "5m", "entry.png", [1, 2, 3]), new(TradeScreenshotType.Exit, "15m", "exit.png", [1, 2, 3])]);
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class CancellationStore(IManualTradeScreenshotStore inner, CancellationTokenSource source, bool cancel) : IManualTradeScreenshotStore
    {
        public Task AddAsync(Trade trade, IReadOnlyList<TradeScreenshot> screenshots, CancellationToken token = default)
        { if (cancel) source.Cancel(); return inner.AddAsync(trade, screenshots, token); }
    }
    private sealed class TrackingFiles(ITradeScreenshotFileStorage inner) : ITradeScreenshotFileStorage
    {
        public ITradeScreenshotFileStorage Inner => inner;
        public List<string> Keys { get; } = [];
        public List<string> Deleted { get; } = [];
        public async Task<string> StoreAsync(Stream content, string extension, CancellationToken token = default)
        { var key = await inner.StoreAsync(content, extension, token); Keys.Add(key); return key; }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken token = default) => inner.OpenReadAsync(key, token);
        public async Task DeleteIfExistsAsync(string key, CancellationToken token = default)
        { await inner.DeleteIfExistsAsync(key, token); Deleted.Add(key); }
    }
}
