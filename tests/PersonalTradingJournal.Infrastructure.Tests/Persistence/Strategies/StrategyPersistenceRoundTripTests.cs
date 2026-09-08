using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Strategies;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Strategies;

public sealed class StrategyPersistenceRoundTripTests
{
    [Fact]
    public void StrategyRoundTripsThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 4, 15, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(1);
        var original = new Strategy(
            "ICT 2022 Model",
            "Liquidity-based discretionary framework.",
            createdAtUtc);
        original.Deactivate(updatedAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.Strategies.Add(StrategyPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        Strategy rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            StrategyRecord record = readContext.Strategies
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = StrategyPersistenceMapper.ToDomain(record);
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
            new(2026, 4, 16, 14, 0, 0, TimeSpan.Zero);
        var original = new Strategy("Opening Range", null, createdAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.Strategies.Add(StrategyPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        StrategyRecord persisted;
        using (var readContext = new JournalDbContext(options))
        {
            persisted = readContext.Strategies
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);
        }

        Strategy rehydrated = StrategyPersistenceMapper.ToDomain(persisted);

        Assert.Null(persisted.Description);
        Assert.Null(rehydrated.Description);
        Assert.True(rehydrated.IsActive);
        Assert.Equal(createdAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(createdAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void ModelMapsStrategyRecordWithoutGeneratedIdOrRelationships()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(StrategyRecord))!;

        Assert.Equal("Strategies", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(StrategyRecord.Id))!.ValueGenerated);
        Assert.True(entityType.FindProperty(nameof(StrategyRecord.Description))!.IsNullable);
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
