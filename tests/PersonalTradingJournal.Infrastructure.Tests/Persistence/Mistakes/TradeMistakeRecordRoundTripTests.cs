using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradeMistakeRecordRoundTripTests
{
    private static readonly Guid TradingAccountId =
        Guid.Parse("d620d751-f72f-45ae-a6ce-157d011329c8");

    private static readonly Guid InstrumentId =
        Guid.Parse("76eeb5e9-927d-41df-8cc1-18680ad9118b");

    private static readonly Guid TradeAId =
        Guid.Parse("c4c058fd-22d1-4cff-a183-d3b90d5ddabe");

    private static readonly Guid TradeBId =
        Guid.Parse("4e73ef9a-0da4-42db-b8e1-0c3bd4caa89a");

    private static readonly Guid TradingMistakeAId =
        Guid.Parse("f4248df5-f1ed-4111-b077-3c206be65c4f");

    private static readonly Guid TradingMistakeBId =
        Guid.Parse("4958907a-a04b-4bd8-8e13-8c606051636c");

    private static readonly Guid AssociationAId =
        Guid.Parse("de630a5a-ea3e-4808-b7ea-9db6a062043a");

    private static readonly Guid AssociationBId =
        Guid.Parse("ee505f16-2fd5-43df-905d-ac476c2715bf");

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 28, 14, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        CreatedAtUtc.AddMinutes(20);

    [Fact]
    public void PopulatedTradeMistakeRoundTripsThroughSqlite()
    {
        TradeMistake original = CreateTradeMistake();

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredParents(writeContext);
            writeContext.TradeMistakes.Add(
                TradeMistakePersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradeMistake rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            TradeMistakeRecord record = readContext.TradeMistakes
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = TradeMistakePersistenceMapper.ToDomain(record);
        }

        AssertTradeMistake(original, rehydrated);
        Assert.Equal("Entered before planned confirmation.", rehydrated.Note);
    }

    [Fact]
    public void NullNoteRoundTripsThroughSqlite()
    {
        TradeMistake original = TradeMistake.Rehydrate(
            AssociationAId,
            TradeAId,
            TradingMistakeAId,
            null,
            CreatedAtUtc,
            UpdatedAtUtc);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredParents(writeContext);
            writeContext.TradeMistakes.Add(
                TradeMistakePersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        TradeMistakeRecord record = readContext.TradeMistakes
            .AsNoTracking()
            .Single(candidate => candidate.Id == original.Id);
        TradeMistake rehydrated = TradeMistakePersistenceMapper.ToDomain(record);

        AssertTradeMistake(original, rehydrated);
        Assert.Null(record.Note);
        Assert.Null(rehydrated.Note);
    }

    [Fact]
    public void DuplicateTradeAndTradingMistakePairIsRejectedBySqlite()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredParents(writeContext);
            writeContext.TradeMistakes.Add(CreateAssociationRecord(
                AssociationAId,
                TradeAId,
                TradingMistakeAId));
            writeContext.SaveChanges();
        }

        using var duplicateContext = new JournalDbContext(options);
        duplicateContext.TradeMistakes.Add(CreateAssociationRecord(
            AssociationBId,
            TradeAId,
            TradingMistakeAId));

        Assert.Throws<DbUpdateException>(() => duplicateContext.SaveChanges());
    }

    [Fact]
    public void SameTradingMistakeCanBeAssociatedWithDifferentTrades()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(TradeAId));
            writeContext.Trades.Add(CreateTradeRecord(TradeBId));
            writeContext.TradingMistakes.Add(
                CreateTradingMistakeRecord(TradingMistakeAId));
            writeContext.TradeMistakes.Add(CreateAssociationRecord(
                AssociationAId,
                TradeAId,
                TradingMistakeAId));
            writeContext.TradeMistakes.Add(CreateAssociationRecord(
                AssociationBId,
                TradeBId,
                TradingMistakeAId));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        Assert.Equal(2, readContext.TradeMistakes.Count());
    }

    [Fact]
    public void DifferentTradingMistakesCanBeAssociatedWithSameTrade()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(TradeAId));
            writeContext.TradingMistakes.Add(
                CreateTradingMistakeRecord(TradingMistakeAId));
            writeContext.TradingMistakes.Add(
                CreateTradingMistakeRecord(TradingMistakeBId));
            writeContext.TradeMistakes.Add(CreateAssociationRecord(
                AssociationAId,
                TradeAId,
                TradingMistakeAId));
            writeContext.TradeMistakes.Add(CreateAssociationRecord(
                AssociationBId,
                TradeAId,
                TradingMistakeBId));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        Assert.Equal(2, readContext.TradeMistakes.Count());
    }

    [Fact]
    public void MissingTradeIsRejectedBySqliteForeignKey()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using var context = new JournalDbContext(options);
        context.Database.EnsureCreated();
        context.TradingMistakes.Add(
            CreateTradingMistakeRecord(TradingMistakeAId));
        context.SaveChanges();
        context.TradeMistakes.Add(CreateAssociationRecord(
            AssociationAId,
            Guid.NewGuid(),
            TradingMistakeAId));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void MissingTradingMistakeIsRejectedBySqliteForeignKey()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using var context = new JournalDbContext(options);
        context.Database.EnsureCreated();
        AddReferenceRecords(context);
        context.Trades.Add(CreateTradeRecord(TradeAId));
        context.SaveChanges();
        context.TradeMistakes.Add(CreateAssociationRecord(
            AssociationAId,
            TradeAId,
            Guid.NewGuid()));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void DeletingReferencedTradeIsRejectedBySqlite()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);
        PersistRequiredGraph(options);

        using var deleteContext = new JournalDbContext(options);
        TradeRecord trade = deleteContext.Trades.Single(record => record.Id == TradeAId);
        deleteContext.Trades.Remove(trade);

        Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
    }

    [Fact]
    public void DeletingReferencedTradingMistakeIsRejectedBySqlite()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);
        PersistRequiredGraph(options);

        using var deleteContext = new JournalDbContext(options);
        TradingMistakeRecord tradingMistake = deleteContext.TradingMistakes
            .Single(record => record.Id == TradingMistakeAId);
        deleteContext.TradingMistakes.Remove(tradingMistake);

        Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
    }

    [Fact]
    public void InactiveTradingMistakeRemainsAValidHistoricalReference()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(TradeAId));
            writeContext.TradingMistakes.Add(
                CreateTradingMistakeRecord(TradingMistakeAId, isActive: false));
            writeContext.TradeMistakes.Add(CreateAssociationRecord(
                AssociationAId,
                TradeAId,
                TradingMistakeAId));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        TradeMistakeRecord record = readContext.TradeMistakes
            .AsNoTracking()
            .Single(candidate => candidate.Id == AssociationAId);
        TradeMistake rehydrated = TradeMistakePersistenceMapper.ToDomain(record);

        Assert.Equal(TradingMistakeAId, rehydrated.TradingMistakeId);
        Assert.False(readContext.TradingMistakes
            .AsNoTracking()
            .Single(mistake => mistake.Id == TradingMistakeAId)
            .IsActive);
    }

    [Fact]
    public void DeletingAssociationLeavesBothParents()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);
        PersistRequiredGraph(options);

        using (var deleteContext = new JournalDbContext(options))
        {
            TradeMistakeRecord association = deleteContext.TradeMistakes
                .Single(record => record.Id == AssociationAId);
            deleteContext.TradeMistakes.Remove(association);
            deleteContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        Assert.Empty(readContext.TradeMistakes);
        Assert.True(readContext.Trades.Any(record => record.Id == TradeAId));
        Assert.True(readContext.TradingMistakes.Any(
            record => record.Id == TradingMistakeAId));
    }

    [Fact]
    public void ModelMapsExplicitAssociationAndIntegrityConstraints()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType =
            context.Model.FindEntityType(typeof(TradeMistakeRecord))!;

        Assert.Equal("TradeMistakes", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(TradeMistakeRecord.Id))!.ValueGenerated);

        IProperty noteProperty =
            entityType.FindProperty(nameof(TradeMistakeRecord.Note))!;
        Assert.True(noteProperty.IsNullable);
        Assert.Equal(2000, noteProperty.GetMaxLength());

        IForeignKey tradeForeignKey = Assert.Single(
            entityType.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Single().Name ==
                nameof(TradeMistakeRecord.TradeId));
        Assert.Equal(typeof(TradeRecord), tradeForeignKey.PrincipalEntityType.ClrType);
        Assert.True(tradeForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, tradeForeignKey.DeleteBehavior);

        IForeignKey tradingMistakeForeignKey = Assert.Single(
            entityType.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Single().Name ==
                nameof(TradeMistakeRecord.TradingMistakeId));
        Assert.Equal(
            typeof(TradingMistakeRecord),
            tradingMistakeForeignKey.PrincipalEntityType.ClrType);
        Assert.True(tradingMistakeForeignKey.IsRequired);
        Assert.Equal(
            DeleteBehavior.Restrict,
            tradingMistakeForeignKey.DeleteBehavior);

        IIndex associationIndex = Assert.Single(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(
                [
                    nameof(TradeMistakeRecord.TradeId),
                    nameof(TradeMistakeRecord.TradingMistakeId),
                ]));
        Assert.True(associationIndex.IsUnique);
        Assert.Empty(entityType.GetNavigations());
    }

    private static TradeMistake CreateTradeMistake()
    {
        return TradeMistake.Rehydrate(
            AssociationAId,
            TradeAId,
            TradingMistakeAId,
            "Entered before planned confirmation.",
            CreatedAtUtc,
            UpdatedAtUtc);
    }

    private static TradeMistakeRecord CreateAssociationRecord(
        Guid associationId,
        Guid tradeId,
        Guid tradingMistakeId)
    {
        return new TradeMistakeRecord
        {
            Id = associationId,
            TradeId = tradeId,
            TradingMistakeId = tradingMistakeId,
            Note = "Entered before planned confirmation.",
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
        };
    }

    private static TradeRecord CreateTradeRecord(Guid tradeId)
    {
        return new TradeRecord
        {
            Id = tradeId,
            TradingAccountId = TradingAccountId,
            InstrumentId = InstrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            StrategyId = null,
            TradingSetupId = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradingMistakeRecord CreateTradingMistakeRecord(
        Guid tradingMistakeId,
        bool isActive = true)
    {
        return new TradingMistakeRecord
        {
            Id = tradingMistakeId,
            Name = tradingMistakeId == TradingMistakeAId ? "FOMO" : "Overtrading",
            Description = null,
            IsActive = isActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static void AddRequiredParents(JournalDbContext context)
    {
        AddReferenceRecords(context);
        context.Trades.Add(CreateTradeRecord(TradeAId));
        context.TradingMistakes.Add(
            CreateTradingMistakeRecord(TradingMistakeAId));
    }

    private static void AddReferenceRecords(JournalDbContext context)
    {
        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = TradingAccountId,
            Name = "Test Account",
            AccountType = TradingAccountType.Demo,
            ProviderName = null,
            ExternalAccountId = null,
            Currency = "USD",
            StartingBalance = null,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });

        context.Instruments.Add(new InstrumentRecord
        {
            Id = InstrumentId,
            Symbol = "NQ",
            DisplayName = "Nasdaq-100 E-mini",
            AssetClass = AssetClass.Futures,
            Exchange = "CME",
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
    }

    private static void PersistRequiredGraph(
        DbContextOptions<JournalDbContext> options)
    {
        using var writeContext = new JournalDbContext(options);
        writeContext.Database.EnsureCreated();
        AddRequiredParents(writeContext);
        writeContext.TradeMistakes.Add(CreateAssociationRecord(
            AssociationAId,
            TradeAId,
            TradingMistakeAId));
        writeContext.SaveChanges();
    }

    private static void AssertTradeMistake(
        TradeMistake expected,
        TradeMistake actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TradeId, actual.TradeId);
        Assert.Equal(expected.TradingMistakeId, actual.TradingMistakeId);
        Assert.Equal(expected.Note, actual.Note);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static SqliteConnection OpenConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            ForeignKeys = true,
        };

        var connection = new SqliteConnection(connectionString.ToString());
        connection.Open();
        return connection;
    }

    private static DbContextOptions<JournalDbContext> CreateOptions(
        SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection)
            .Options;
    }
}
