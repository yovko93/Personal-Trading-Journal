using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed class TradingSetupCatalogPersistenceTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReaderReturnsEmpty()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        Assert.Empty(await db.ServiceProvider.GetRequiredService<ITradingSetupReader>().GetAllAsync());
    }

    [Fact]
    public async Task ReaderProjectsAllFieldsAndOrdersActiveThenNameThenId()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        Guid a = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid b = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid c = Guid.Parse("10000000-0000-0000-0000-000000000003");
        await Seed(db, Record(b, "alpha", "B", true, Created), Record(c, "Aardvark", null, false, Created),
            Record(a, "Alpha", "A", true, Created.AddHours(1)));
        IReadOnlyList<TradingSetupListItem> rows = await db.ServiceProvider.GetRequiredService<ITradingSetupReader>().GetAllAsync();
        Assert.Equal([a, b, c], rows.Select(x => x.Id));
        Assert.Equal("A", rows[0].Description); Assert.Equal(Created.AddHours(1), rows[0].UpdatedAtUtc);
        Assert.False(rows[2].IsActive);
    }

    [Fact]
    public async Task ReaderProjectsDetailsAndMissingReturnsNull()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        Guid id = Guid.NewGuid();
        await Seed(db, Record(id, "Silver Bullet", "Timed model", false, Created.AddHours(1)));
        ITradingSetupReader reader = db.ServiceProvider.GetRequiredService<ITradingSetupReader>();

        TradingSetupDetails details = Assert.IsType<TradingSetupDetails>(
            await reader.GetByIdAsync(id));
        Assert.Equal(id, details.Id);
        Assert.Equal("Silver Bullet", details.Name);
        Assert.Equal("Timed model", details.Description);
        Assert.False(details.IsActive);
        Assert.Equal(Created, details.CreatedAtUtc);
        Assert.Equal(Created.AddHours(1), details.UpdatedAtUtc);
        Assert.Null(await reader.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task StoreAddsAndRehydratesAllFieldsAndMissingReturnsNull()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        ITradingSetupStore store = db.ServiceProvider.GetRequiredService<ITradingSetupStore>();
        var setup = new TradingSetup("  Silver Bullet ", " Model ", Created);
        await store.AddAsync(setup);
        TradingSetup loaded = Assert.IsType<TradingSetup>(await store.GetByIdAsync(setup.Id));
        Assert.Equal(setup.Id, loaded.Id); Assert.Equal("Silver Bullet", loaded.Name); Assert.Equal("Model", loaded.Description);
        Assert.Equal(Created, loaded.CreatedAtUtc); Assert.True(loaded.IsActive);
        Assert.Null(await store.GetByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task StoreUpdatePersistsCompleteAuthoritativeStateAndLeavesOtherRowsAlone()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        ITradingSetupStore store = db.ServiceProvider.GetRequiredService<ITradingSetupStore>();
        var changed = new TradingSetup("ORB", "Opening range", Created); var other = new TradingSetup("CRT", null, Created);
        await store.AddAsync(changed); await store.AddAsync(other);
        changed.UpdateDetails("  Opening Range Breakout  ", "  Revised model  ", Created.AddDays(1));
        changed.Deactivate(Created.AddDays(2));
        await store.UpdateAsync(changed);
        await using JournalDbContext context = await db.ContextFactory.CreateDbContextAsync();
        TradingSetupRecord record = await context.TradingSetups.AsNoTracking().SingleAsync(x => x.Id == changed.Id);
        TradingSetupRecord unchanged = await context.TradingSetups.AsNoTracking().SingleAsync(x => x.Id == other.Id);
        Assert.False(record.IsActive); Assert.Equal(Created.AddDays(2), record.UpdatedAtUtc);
        Assert.Equal("Opening Range Breakout", record.Name);
        Assert.Equal("Revised model", record.Description); Assert.Equal(Created, record.CreatedAtUtc); Assert.True(unchanged.IsActive);
    }

    [Theory]
    [InlineData("Silver Bullet", true)] [InlineData("silver bullet", true)]
    [InlineData("  Silver Bullet  ", true)] [InlineData("Breakout", false)]
    public async Task NameCheckerIsTrimmedCaseInsensitiveAndIncludesInactive(string name, bool expected)
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync(); Guid id = Guid.NewGuid();
        await Seed(db, Record(id, "Silver Bullet", null, false, Created));
        ITradingSetupNameChecker checker = db.ServiceProvider.GetRequiredService<ITradingSetupNameChecker>();
        Assert.Equal(expected, await checker.ExistsAsync(name));
        if (expected) Assert.False(await checker.ExistsAsync(name, id));
    }

    [Fact]
    public async Task OperationsPropagateCancellation()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        ITradingSetupStore store = db.ServiceProvider.GetRequiredService<ITradingSetupStore>();
        var setup = new TradingSetup("ORB", null, Created); await store.AddAsync(setup); setup.Deactivate(Created.AddDays(1));
        using var source = new CancellationTokenSource(); await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetByIdAsync(setup.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpdateAsync(setup, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.ServiceProvider.GetRequiredService<ITradingSetupReader>().GetAllAsync(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.ServiceProvider.GetRequiredService<ITradingSetupReader>().GetByIdAsync(setup.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.ServiceProvider.GetRequiredService<ITradingSetupNameChecker>().ExistsAsync("ORB", cancellationToken: source.Token));
    }

    private static TradingSetupRecord Record(Guid id, string name, string? description, bool active, DateTimeOffset updated) =>
        new() { Id = id, Name = name, Description = description, IsActive = active, CreatedAtUtc = Created, UpdatedAtUtc = updated };
    private static async Task Seed(ReaderTestDatabase db, params TradingSetupRecord[] records)
    { await using JournalDbContext context = await db.ContextFactory.CreateDbContextAsync(); context.TradingSetups.AddRange(records); await context.SaveChangesAsync(); }
}
