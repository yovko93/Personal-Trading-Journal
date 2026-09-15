using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradingMistakeCatalogPersistenceTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReaderReturnsEmptyForEmptyDatabase()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Assert.Empty(await database.ServiceProvider.GetRequiredService<ITradingMistakeReader>().GetAllAsync());
    }

    [Fact]
    public async Task ReaderProjectsAllFieldsAndOrdersActiveThenNameThenId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid inactive = Guid.Parse("10000000-0000-0000-0000-000000000003");
        await Seed(database, Record(second, "fomo", "Second", true, Created),
            Record(inactive, "Entered Early", null, false, Created),
            Record(first, "FOMO", "First", true, Created.AddHours(1)));

        IReadOnlyList<TradingMistakeListItem> result =
            await database.ServiceProvider.GetRequiredService<ITradingMistakeReader>().GetAllAsync();
        Assert.Equal([first, second, inactive], result.Select(item => item.Id));
        Assert.Equal("First", result[0].Description); Assert.Equal(Created.AddHours(1), result[0].UpdatedAtUtc);
        Assert.False(result[2].IsActive);
    }

    [Fact]
    public async Task StoreAddsAndRehydratesEveryFieldAndReturnsNullWhenMissing()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingMistakeStore store = database.ServiceProvider.GetRequiredService<ITradingMistakeStore>();
        var mistake = new TradingMistake("  Moved Stop ", " Execution error ", Created);
        await store.AddAsync(mistake);
        TradingMistake loaded = Assert.IsType<TradingMistake>(await store.GetByIdAsync(mistake.Id));
        Assert.Equal(mistake.Id, loaded.Id); Assert.Equal("Moved Stop", loaded.Name); Assert.Equal("Execution error", loaded.Description);
        Assert.True(loaded.IsActive); Assert.Equal(Created, loaded.CreatedAtUtc); Assert.Equal(Created, loaded.UpdatedAtUtc);
        Assert.Null(await store.GetByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task StoreUpdateChangesLifecycleOnlyAndLeavesOtherMistakesUnchanged()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingMistakeStore store = database.ServiceProvider.GetRequiredService<ITradingMistakeStore>();
        var changed = new TradingMistake("FOMO", "Early entry", Created);
        var other = new TradingMistake("Overtrading", null, Created);
        await store.AddAsync(changed); await store.AddAsync(other); changed.Deactivate(Created.AddDays(1)); await store.UpdateAsync(changed);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        TradingMistakeRecord record = await context.TradingMistakes.AsNoTracking().SingleAsync(x => x.Id == changed.Id);
        TradingMistakeRecord untouched = await context.TradingMistakes.AsNoTracking().SingleAsync(x => x.Id == other.Id);
        Assert.False(record.IsActive); Assert.Equal(Created.AddDays(1), record.UpdatedAtUtc); Assert.Equal("FOMO", record.Name);
        Assert.Equal("Early entry", record.Description); Assert.Equal(Created, record.CreatedAtUtc); Assert.True(untouched.IsActive);
    }

    [Theory]
    [InlineData("FOMO", true)] [InlineData("fomo", true)] [InlineData("  FOMO  ", true)] [InlineData("Moved Stop", false)]
    public async Task NameCheckerIsTrimmedCaseInsensitiveAndIncludesInactive(string name, bool expected)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync(); Guid id = Guid.NewGuid();
        await Seed(database, Record(id, "FOMO", null, false, Created));
        ITradingMistakeNameChecker checker = database.ServiceProvider.GetRequiredService<ITradingMistakeNameChecker>();
        Assert.Equal(expected, await checker.ExistsAsync(name));
        if (expected) Assert.False(await checker.ExistsAsync(name, id));
    }

    [Fact]
    public async Task OperationsPropagateCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingMistakeStore store = database.ServiceProvider.GetRequiredService<ITradingMistakeStore>();
        var mistake = new TradingMistake("FOMO", null, Created); await store.AddAsync(mistake); mistake.Deactivate(Created.AddDays(1));
        using var source = new CancellationTokenSource(); await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetByIdAsync(mistake.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpdateAsync(mistake, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.ServiceProvider.GetRequiredService<ITradingMistakeReader>().GetAllAsync(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.ServiceProvider.GetRequiredService<ITradingMistakeNameChecker>().ExistsAsync("FOMO", cancellationToken: source.Token));
    }

    private static TradingMistakeRecord Record(Guid id, string name, string? description, bool active, DateTimeOffset updated) =>
        new() { Id = id, Name = name, Description = description, IsActive = active, CreatedAtUtc = Created, UpdatedAtUtc = updated };
    private static async Task Seed(ReaderTestDatabase database, params TradingMistakeRecord[] records)
    { await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync(); context.TradingMistakes.AddRange(records); await context.SaveChangesAsync(); }
}
