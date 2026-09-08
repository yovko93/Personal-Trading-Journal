using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradingMistakePersistenceRoundTripTests
{
    [Fact]
    public void TradingMistakeRoundTripsThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 6, 15, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(1);
        var original = new TradingMistake(
            "FOMO",
            "Entered because price was moving away before planned confirmation.",
            createdAtUtc);
        original.Deactivate(updatedAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.TradingMistakes.Add(
                TradingMistakePersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradingMistake rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            TradingMistakeRecord record = readContext.TradingMistakes
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = TradingMistakePersistenceMapper.ToDomain(record);
        }

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.Name, rehydrated.Name);
        Assert.Equal(original.Description, rehydrated.Description);
        Assert.Equal(original.IsActive, rehydrated.IsActive);
        Assert.Equal(original.CreatedAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(original.UpdatedAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void UserDefinedNameAndNullDescriptionRoundTripThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 6, 16, 14, 0, 0, TimeSpan.Zero);
        var original = new TradingMistake("Outside NY Session", null, createdAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.TradingMistakes.Add(
                TradingMistakePersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradingMistakeRecord persisted;
        using (var readContext = new JournalDbContext(options))
        {
            persisted = readContext.TradingMistakes
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);
        }

        TradingMistake rehydrated = TradingMistakePersistenceMapper.ToDomain(persisted);

        Assert.Equal("Outside NY Session", persisted.Name);
        Assert.Equal("Outside NY Session", rehydrated.Name);
        Assert.Null(persisted.Description);
        Assert.Null(rehydrated.Description);
        Assert.True(rehydrated.IsActive);
        Assert.Equal(createdAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(createdAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void ModelMapsTradingMistakeRecordAsIndependentCatalogData()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(TradingMistakeRecord))!;

        Assert.Equal("TradingMistakes", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(TradingMistakeRecord.Id))!.ValueGenerated);
        Assert.True(entityType.FindProperty(nameof(TradingMistakeRecord.Description))!.IsNullable);
        Assert.Null(entityType.FindProperty("Category"));
        Assert.Null(entityType.FindProperty("Severity"));
        Assert.Null(entityType.FindProperty("Cost"));
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
