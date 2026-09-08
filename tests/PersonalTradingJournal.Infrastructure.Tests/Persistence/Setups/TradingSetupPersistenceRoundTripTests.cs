using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed class TradingSetupPersistenceRoundTripTests
{
    [Fact]
    public void TradingSetupRoundTripsThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 5, 15, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(1);
        var original = new TradingSetup(
            "Liquidity Sweep + MSS + FVG",
            "Liquidity sweep followed by market structure shift and FVG entry.",
            createdAtUtc);
        original.Deactivate(updatedAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradingSetup rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            TradingSetupRecord record = readContext.TradingSetups
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = TradingSetupPersistenceMapper.ToDomain(record);
        }

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.Name, rehydrated.Name);
        Assert.Equal(original.Description, rehydrated.Description);
        Assert.Equal(original.IsActive, rehydrated.IsActive);
        Assert.Equal(original.CreatedAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(original.UpdatedAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void NullDescriptionRoundTripsThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 5, 16, 14, 0, 0, TimeSpan.Zero);
        var original = new TradingSetup("Opening Range Breakout", null, createdAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradingSetupRecord persisted;
        using (var readContext = new JournalDbContext(options))
        {
            persisted = readContext.TradingSetups
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);
        }

        TradingSetup rehydrated = TradingSetupPersistenceMapper.ToDomain(persisted);

        Assert.Null(persisted.Description);
        Assert.Null(rehydrated.Description);
        Assert.True(rehydrated.IsActive);
        Assert.Equal(createdAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(createdAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void ModelMapsTradingSetupRecordWithoutStrategyRelationship()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(TradingSetupRecord))!;

        Assert.Equal("TradingSetups", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(TradingSetupRecord.Id))!.ValueGenerated);
        Assert.True(entityType.FindProperty(nameof(TradingSetupRecord.Description))!.IsNullable);
        Assert.Null(entityType.FindProperty("StrategyId"));
        Assert.Empty(entityType.GetForeignKeys());
        Assert.Empty(entityType.GetNavigations());
    }

    private static DbContextOptions<JournalDbContext> CreateOptions(
        SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection)
            .Options;
    }
}
