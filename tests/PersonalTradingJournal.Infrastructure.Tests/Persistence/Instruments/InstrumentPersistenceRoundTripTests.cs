using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Instruments;

public sealed class InstrumentPersistenceRoundTripTests
{
    [Fact]
    public void InstrumentRoundTripsThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 2, 15, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(1);
        var original = new Instrument(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            createdAtUtc);
        original.Deactivate(updatedAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.Instruments.Add(InstrumentPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        Instrument rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            InstrumentRecord record = readContext.Instruments
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = InstrumentPersistenceMapper.ToDomain(record);
        }

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.Symbol, rehydrated.Symbol);
        Assert.Equal(original.DisplayName, rehydrated.DisplayName);
        Assert.Equal(original.AssetClass, rehydrated.AssetClass);
        Assert.Equal(original.Exchange, rehydrated.Exchange);
        Assert.Equal(original.Currency, rehydrated.Currency);
        Assert.Equal(original.TickSize, rehydrated.TickSize);
        Assert.Equal(original.TickValue, rehydrated.TickValue);
        Assert.Equal(20m, rehydrated.PointValue);
        Assert.Equal(original.IsActive, rehydrated.IsActive);
        Assert.Equal(original.CreatedAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(original.UpdatedAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void ModelMapsInstrumentRecordWithoutGeneratedIdOrPointValue()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(InstrumentRecord))!;

        Assert.Equal("Instruments", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(InstrumentRecord.Id))!.ValueGenerated);
        Assert.Equal(
            typeof(int),
            entityType.FindProperty(nameof(InstrumentRecord.AssetClass))!
                .GetProviderClrType());
        Assert.Null(entityType.FindProperty("PointValue"));
    }
}
