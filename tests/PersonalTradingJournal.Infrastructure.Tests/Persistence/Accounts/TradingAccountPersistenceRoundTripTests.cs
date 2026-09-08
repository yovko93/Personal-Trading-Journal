using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Accounts;

public sealed class TradingAccountPersistenceRoundTripTests
{
    [Fact]
    public void TradingAccountRoundTripsThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 3, 15, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(1);
        var original = new TradingAccount(
            "Topstep 50K",
            TradingAccountType.PropEvaluation,
            "Topstep",
            "TS-ACC-001",
            "USD",
            50000m,
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
            writeContext.TradingAccounts.Add(
                TradingAccountPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradingAccount rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            TradingAccountRecord record = readContext.TradingAccounts
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = TradingAccountPersistenceMapper.ToDomain(record);
        }

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.Name, rehydrated.Name);
        Assert.Equal(original.AccountType, rehydrated.AccountType);
        Assert.Equal(original.ProviderName, rehydrated.ProviderName);
        Assert.Equal(original.ExternalAccountId, rehydrated.ExternalAccountId);
        Assert.Equal(original.Currency, rehydrated.Currency);
        Assert.Equal(original.StartingBalance, rehydrated.StartingBalance);
        Assert.Equal(original.IsActive, rehydrated.IsActive);
        Assert.Equal(original.CreatedAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(original.UpdatedAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void NullOptionalValuesRoundTripThroughSqlite()
    {
        DateTimeOffset createdAtUtc =
            new(2026, 3, 16, 14, 0, 0, TimeSpan.Zero);
        var original = new TradingAccount(
            "Personal",
            TradingAccountType.Personal,
            null,
            null,
            "USD",
            null,
            createdAtUtc);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.TradingAccounts.Add(
                TradingAccountPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradingAccountRecord persisted;
        using (var readContext = new JournalDbContext(options))
        {
            persisted = readContext.TradingAccounts
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);
        }

        TradingAccount rehydrated = TradingAccountPersistenceMapper.ToDomain(persisted);

        Assert.Null(persisted.ProviderName);
        Assert.Null(persisted.ExternalAccountId);
        Assert.Null(persisted.StartingBalance);
        Assert.Null(rehydrated.ProviderName);
        Assert.Null(rehydrated.ExternalAccountId);
        Assert.Null(rehydrated.StartingBalance);
        Assert.True(rehydrated.IsActive);
        Assert.Equal(createdAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(createdAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void ModelMapsTradingAccountRecordWithoutGeneratedIdOrCurrentBalance()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(TradingAccountRecord))!;

        Assert.Equal("TradingAccounts", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(TradingAccountRecord.Id))!.ValueGenerated);
        Assert.Equal(
            typeof(int),
            entityType.FindProperty(nameof(TradingAccountRecord.AccountType))!
                .GetProviderClrType());
        Assert.True(entityType.FindProperty(nameof(TradingAccountRecord.ProviderName))!.IsNullable);
        Assert.True(
            entityType.FindProperty(nameof(TradingAccountRecord.ExternalAccountId))!.IsNullable);
        Assert.True(
            entityType.FindProperty(nameof(TradingAccountRecord.StartingBalance))!.IsNullable);
        Assert.Null(entityType.FindProperty("CurrentBalance"));
    }
}
