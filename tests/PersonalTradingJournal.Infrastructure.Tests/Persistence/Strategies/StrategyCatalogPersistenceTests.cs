using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Strategies;
using PersonalTradingJournal.Domain.Strategies;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Strategies;

public sealed class StrategyCatalogPersistenceTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReaderReturnsEmptyListForEmptyDatabase()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IStrategyReader reader = database.ServiceProvider.GetRequiredService<IStrategyReader>();

        Assert.Empty(await reader.GetAllAsync());
    }

    [Fact]
    public async Task ReaderProjectsAllFieldsAndOrdersActiveThenNameIgnoringCaseThenId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid alphaFirst = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid alphaSecond = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid inactive = Guid.Parse("10000000-0000-0000-0000-000000000003");
        await SeedAsync(database,
            Record(alphaSecond, "alpha", "Second", true, CreatedAtUtc),
            Record(inactive, "Aardvark", null, false, CreatedAtUtc.AddHours(2)),
            Record(alphaFirst, "Alpha", "First", true, CreatedAtUtc.AddHours(1)));

        IStrategyReader reader = database.ServiceProvider.GetRequiredService<IStrategyReader>();
        IReadOnlyList<StrategyListItem> result = await reader.GetAllAsync();

        Assert.Equal([alphaFirst, alphaSecond, inactive], result.Select(item => item.Id));
        StrategyListItem first = result[0];
        Assert.Equal("Alpha", first.Name);
        Assert.Equal("First", first.Description);
        Assert.True(first.IsActive);
        Assert.Equal(CreatedAtUtc, first.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(1), first.UpdatedAtUtc);
        Assert.False(result[2].IsActive);
    }

    [Fact]
    public async Task StoreAddsAndRehydratesEveryField()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IStrategyStore store = database.ServiceProvider.GetRequiredService<IStrategyStore>();
        var strategy = new Strategy("  Mean Reversion ", " Fade extremes ", CreatedAtUtc);

        await store.AddAsync(strategy);
        Strategy loaded = Assert.IsType<Strategy>(await store.GetByIdAsync(strategy.Id));

        Assert.Equal(strategy.Id, loaded.Id);
        Assert.Equal("Mean Reversion", loaded.Name);
        Assert.Equal("Fade extremes", loaded.Description);
        Assert.True(loaded.IsActive);
        Assert.Equal(CreatedAtUtc, loaded.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, loaded.UpdatedAtUtc);
    }

    [Fact]
    public async Task StoreGetReturnsNullWhenMissingAndRejectsEmptyId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IStrategyStore store = database.ServiceProvider.GetRequiredService<IStrategyStore>();

        Assert.Null(await store.GetByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task StoreUpdatePersistsLifecycleOnlyAndLeavesOtherRowsUnchanged()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IStrategyStore store = database.ServiceProvider.GetRequiredService<IStrategyStore>();
        var changed = new Strategy("Breakout", "Expansion", CreatedAtUtc);
        var unrelated = new Strategy("Trend Following", null, CreatedAtUtc);
        await store.AddAsync(changed);
        await store.AddAsync(unrelated);
        DateTimeOffset changedAt = CreatedAtUtc.AddDays(1);
        changed.Deactivate(changedAt);

        await store.UpdateAsync(changed);

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        StrategyRecord persisted = await context.Strategies.AsNoTracking()
            .SingleAsync(record => record.Id == changed.Id);
        StrategyRecord untouched = await context.Strategies.AsNoTracking()
            .SingleAsync(record => record.Id == unrelated.Id);
        Assert.False(persisted.IsActive);
        Assert.Equal(changedAt, persisted.UpdatedAtUtc);
        Assert.Equal("Breakout", persisted.Name);
        Assert.Equal("Expansion", persisted.Description);
        Assert.Equal(CreatedAtUtc, persisted.CreatedAtUtc);
        Assert.True(untouched.IsActive);
        Assert.Equal(CreatedAtUtc, untouched.UpdatedAtUtc);
    }

    [Fact]
    public async Task StoreUpdateThrowsWhenMissing()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IStrategyStore store = database.ServiceProvider.GetRequiredService<IStrategyStore>();
        var strategy = new Strategy("Breakout", null, CreatedAtUtc);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.UpdateAsync(strategy));
    }

    [Fact]
    public async Task StoreAndReaderPropagatePreCancelledTokensWithoutWriting()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IStrategyStore store = database.ServiceProvider.GetRequiredService<IStrategyStore>();
        IStrategyReader reader = database.ServiceProvider.GetRequiredService<IStrategyReader>();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.AddAsync(new Strategy("ICT", null, CreatedAtUtc), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetByIdAsync(Guid.NewGuid(), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.GetAllAsync(source.Token));
    }

    [Fact]
    public async Task UpdateAndNameCheckPropagatePreCancelledTokens()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IStrategyStore store = database.ServiceProvider.GetRequiredService<IStrategyStore>();
        IStrategyNameChecker checker =
            database.ServiceProvider.GetRequiredService<IStrategyNameChecker>();
        var strategy = new Strategy("Breakout", null, CreatedAtUtc);
        await store.AddAsync(strategy);
        strategy.Deactivate(CreatedAtUtc.AddDays(1));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.UpdateAsync(strategy, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => checker.ExistsAsync("Breakout", cancellationToken: source.Token));

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.True((await context.Strategies.AsNoTracking()
            .SingleAsync(record => record.Id == strategy.Id)).IsActive);
    }

    [Theory]
    [InlineData("ICT", true)]
    [InlineData("ict", true)]
    [InlineData("  ICT  ", true)]
    [InlineData("Breakout", false)]
    public async Task NameCheckerUsesTrimmedCaseInsensitiveComparison(
        string candidate,
        bool expected)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        await SeedAsync(database, Record(Guid.NewGuid(), "ICT", null, false, CreatedAtUtc));
        IStrategyNameChecker checker =
            database.ServiceProvider.GetRequiredService<IStrategyNameChecker>();

        Assert.Equal(expected, await checker.ExistsAsync(candidate));
    }

    [Fact]
    public async Task NameCheckerCanExcludeMatchingStrategy()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid id = Guid.NewGuid();
        await SeedAsync(database, Record(id, "ICT", null, true, CreatedAtUtc));
        IStrategyNameChecker checker =
            database.ServiceProvider.GetRequiredService<IStrategyNameChecker>();

        Assert.False(await checker.ExistsAsync("ict", id));
        Assert.True(await checker.ExistsAsync("ict", Guid.NewGuid()));
    }

    private static StrategyRecord Record(
        Guid id,
        string name,
        string? description,
        bool active,
        DateTimeOffset updatedAtUtc) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            IsActive = active,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };

    private static async Task SeedAsync(
        ReaderTestDatabase database,
        params StrategyRecord[] records)
    {
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        context.Strategies.AddRange(records);
        await context.SaveChangesAsync();
    }
}
