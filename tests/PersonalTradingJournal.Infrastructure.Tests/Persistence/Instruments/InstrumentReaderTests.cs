using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Instruments;

public sealed class InstrumentReaderTests
{
    [Fact]
    public async Task GetAllAsyncReturnsEmptyListForEmptyDatabase()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IInstrumentReader reader =
            database.ServiceProvider.GetRequiredService<IInstrumentReader>();

        IReadOnlyList<InstrumentListItem> instruments = await reader.GetAllAsync();

        Assert.Empty(instruments);
    }

    [Fact]
    public async Task GetAllAsyncMapsAllFieldsPointValueAndActiveState()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid activeId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        Guid inactiveId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        DateTimeOffset createdAtUtc =
            new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        await using (JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync())
        {
            context.Instruments.AddRange(
                new InstrumentRecord
                {
                    Id = activeId,
                    Symbol = "NQ",
                    DisplayName = "Nasdaq-100 E-mini",
                    AssetClass = AssetClass.Futures,
                    Exchange = "CME",
                    Currency = "USD",
                    TickSize = 0.25m,
                    TickValue = 5m,
                    IsActive = true,
                    CreatedAtUtc = createdAtUtc,
                    UpdatedAtUtc = createdAtUtc,
                },
                new InstrumentRecord
                {
                    Id = inactiveId,
                    Symbol = "CL",
                    DisplayName = "Crude Oil",
                    AssetClass = AssetClass.Futures,
                    Exchange = null,
                    Currency = "USD",
                    TickSize = 0.01m,
                    TickValue = 10m,
                    IsActive = false,
                    CreatedAtUtc = createdAtUtc,
                    UpdatedAtUtc = createdAtUtc.AddDays(1),
                });
            await context.SaveChangesAsync();
        }

        IInstrumentReader reader =
            database.ServiceProvider.GetRequiredService<IInstrumentReader>();
        IReadOnlyList<InstrumentListItem> instruments = await reader.GetAllAsync();

        Assert.Equal(2, instruments.Count);
        InstrumentListItem active = Assert.Single(instruments, item => item.Id == activeId);
        Assert.Equal("NQ", active.Symbol);
        Assert.Equal("Nasdaq-100 E-mini", active.DisplayName);
        Assert.Equal(AssetClass.Futures, active.AssetClass);
        Assert.Equal("CME", active.Exchange);
        Assert.Equal("USD", active.Currency);
        Assert.Equal(0.25m, active.TickSize);
        Assert.Equal(5m, active.TickValue);
        Assert.Equal(20m, active.PointValue);
        Assert.True(active.IsActive);

        InstrumentListItem inactive = Assert.Single(instruments, item => item.Id == inactiveId);
        Assert.Equal("CL", inactive.Symbol);
        Assert.Null(inactive.Exchange);
        Assert.Equal(1000m, inactive.PointValue);
        Assert.False(inactive.IsActive);
    }

    [Fact]
    public async Task GetAllAsyncOrdersBySymbolThenById()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid firstEsId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        Guid secondEsId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        Guid nqId = Guid.Parse("40000000-0000-0000-0000-000000000003");

        await using (JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync())
        {
            context.Instruments.AddRange(
                CreateRecord(nqId, "NQ"),
                CreateRecord(secondEsId, "ES"),
                CreateRecord(firstEsId, "ES"));
            await context.SaveChangesAsync();
        }

        IInstrumentReader reader =
            database.ServiceProvider.GetRequiredService<IInstrumentReader>();
        IReadOnlyList<InstrumentListItem> instruments = await reader.GetAllAsync();

        Assert.Equal(
            [firstEsId, secondEsId, nqId],
            instruments.Select(instrument => instrument.Id));
    }

    [Fact]
    public async Task GetAllAsyncPropagatesCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IInstrumentReader reader =
            database.ServiceProvider.GetRequiredService<IInstrumentReader>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.GetAllAsync(cancellationSource.Token));
    }

    private static InstrumentRecord CreateRecord(Guid id, string symbol)
    {
        DateTimeOffset timestamp =
            new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        return new InstrumentRecord
        {
            Id = id,
            Symbol = symbol,
            DisplayName = $"{symbol} display name",
            AssetClass = AssetClass.Futures,
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = true,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }
}
