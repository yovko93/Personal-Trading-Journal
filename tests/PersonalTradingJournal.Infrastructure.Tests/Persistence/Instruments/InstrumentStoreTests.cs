using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Instruments;

public sealed class InstrumentStoreTests
{
    [Fact]
    public async Task AddAsyncPersistsEveryInstrumentField()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IInstrumentStore store =
            database.ServiceProvider.GetRequiredService<IInstrumentStore>();
        DateTimeOffset createdAtUtc =
            new(2026, 9, 9, 15, 0, 0, TimeSpan.Zero);
        var instrument = new Instrument(
            " nq ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "usd",
            0.25m,
            5m,
            createdAtUtc);

        await store.AddAsync(instrument);

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        InstrumentRecord persisted = await readContext.Instruments
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == instrument.Id);

        Assert.Equal(instrument.Id, persisted.Id);
        Assert.Equal("NQ", persisted.Symbol);
        Assert.Equal("Nasdaq-100 E-mini", persisted.DisplayName);
        Assert.Equal(AssetClass.Futures, persisted.AssetClass);
        Assert.Equal("CME", persisted.Exchange);
        Assert.Equal("USD", persisted.Currency);
        Assert.Equal(0.25m, persisted.TickSize);
        Assert.Equal(5m, persisted.TickValue);
        Assert.True(persisted.IsActive);
        Assert.Equal(createdAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(createdAtUtc, persisted.UpdatedAtUtc);
    }

    [Fact]
    public async Task AddAsyncDoesNotPersistWhenCancellationIsRequested()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IInstrumentStore store =
            database.ServiceProvider.GetRequiredService<IInstrumentStore>();
        var instrument = new Instrument(
            "ES",
            "S&P 500 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            12.50m,
            new DateTimeOffset(2026, 9, 9, 15, 30, 0, TimeSpan.Zero));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.AddAsync(instrument, cancellationSource.Token));

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await readContext.Instruments
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == instrument.Id));
    }
}
