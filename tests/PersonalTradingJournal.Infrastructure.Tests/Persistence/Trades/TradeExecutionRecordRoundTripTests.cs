using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeExecutionRecordRoundTripTests
{
    private static readonly Guid TradingAccountId =
        Guid.Parse("a260ea66-52d8-4671-af30-e91d28f712e4");

    private static readonly Guid InstrumentId =
        Guid.Parse("67a02c10-10ae-47fe-b612-243973c30ca3");

    private static readonly Guid TradeAId =
        Guid.Parse("d375ccbc-6f54-4c24-bb17-19cb985a0ef7");

    private static readonly Guid TradeBId =
        Guid.Parse("fc20e385-fe67-4d77-b7fb-035f11fd24a6");

    private static readonly Guid ExecutionAId =
        Guid.Parse("32865163-2e1e-4b20-8be2-f114e2bb981e");

    private static readonly Guid ExecutionBId =
        Guid.Parse("3c504190-0f52-4e3c-b659-0cf53f38ebf8");

    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 8, 15, 13, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 15, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TradeExecutionRoundTripsThroughSqlite()
    {
        TradeExecution original = TradeExecution.Rehydrate(
            ExecutionAId,
            TradeAId,
            1,
            ExecutedAtUtc,
            ExecutionSide.Buy,
            0.5m,
            -37.63m,
            1.25m,
            0.75m,
            "EXEC-42",
            "ORDER-17",
            "CL");

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(TradeAId));
            writeContext.TradeExecutions.Add(
                TradeExecutionPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradeExecution rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            TradeExecutionRecord record = readContext.TradeExecutions
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = TradeExecutionPersistenceMapper.ToDomain(record);
        }

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.TradeId, rehydrated.TradeId);
        Assert.Equal(original.Sequence, rehydrated.Sequence);
        Assert.Equal(original.ExecutedAtUtc, rehydrated.ExecutedAtUtc);
        Assert.Equal(original.Side, rehydrated.Side);
        Assert.Equal(original.Quantity, rehydrated.Quantity);
        Assert.Equal(original.Price, rehydrated.Price);
        Assert.Equal(original.Commission, rehydrated.Commission);
        Assert.Equal(original.Fees, rehydrated.Fees);
        Assert.Equal(original.ExternalExecutionId, rehydrated.ExternalExecutionId);
        Assert.Equal(original.ExternalOrderId, rehydrated.ExternalOrderId);
        Assert.Equal(original.BrokerSymbol, rehydrated.BrokerSymbol);
        Assert.Equal(original.TotalCosts, rehydrated.TotalCosts);
    }

    [Fact]
    public void DuplicateSequenceForSameTradeIsRejectedBySqlite()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using var context = new JournalDbContext(options);
        context.Database.EnsureCreated();
        AddReferenceRecords(context);
        context.Trades.Add(CreateTradeRecord(TradeAId));
        context.TradeExecutions.Add(CreateExecutionRecord(ExecutionAId, TradeAId, 1));
        context.TradeExecutions.Add(CreateExecutionRecord(ExecutionBId, TradeAId, 1));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void SameSequenceForDifferentTradesIsAcceptedBySqlite()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(TradeAId));
            writeContext.Trades.Add(CreateTradeRecord(TradeBId));
            writeContext.TradeExecutions.Add(
                CreateExecutionRecord(ExecutionAId, TradeAId, 1));
            writeContext.TradeExecutions.Add(
                CreateExecutionRecord(ExecutionBId, TradeBId, 1));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        Assert.Equal(2, readContext.TradeExecutions.Count());
    }

    [Fact]
    public void MissingTradeIsRejectedBySqliteForeignKey()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using var context = new JournalDbContext(options);
        context.Database.EnsureCreated();
        context.TradeExecutions.Add(
            CreateExecutionRecord(ExecutionAId, Guid.NewGuid(), 1));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void DeletingTradeCascadesToExecutionRowsInSqlite()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(TradeAId));
            writeContext.TradeExecutions.Add(
                CreateExecutionRecord(ExecutionAId, TradeAId, 1));
            writeContext.SaveChanges();
        }

        using (var deleteContext = new JournalDbContext(options))
        {
            TradeRecord trade = deleteContext.Trades.Single(record => record.Id == TradeAId);
            deleteContext.Trades.Remove(trade);
            deleteContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        Assert.Empty(readContext.TradeExecutions);
    }

    [Fact]
    public void ModelMapsExecutionOwnershipAndSequenceUniqueness()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(TradeExecutionRecord))!;

        Assert.Equal("TradeExecutions", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(TradeExecutionRecord.Id))!.ValueGenerated);
        Assert.Equal(
            typeof(int),
            entityType.FindProperty(nameof(TradeExecutionRecord.Side))!
                .GetProviderClrType());

        IForeignKey foreignKey = Assert.Single(entityType.GetForeignKeys());
        Assert.Equal(nameof(TradeExecutionRecord.TradeId), foreignKey.Properties.Single().Name);
        Assert.Equal(typeof(TradeRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);

        IIndex sequenceIndex = Assert.Single(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(
                [
                    nameof(TradeExecutionRecord.TradeId),
                    nameof(TradeExecutionRecord.Sequence),
                ]));

        Assert.True(sequenceIndex.IsUnique);
        Assert.Empty(entityType.GetNavigations());
    }

    private static TradeExecutionRecord CreateExecutionRecord(
        Guid executionId,
        Guid tradeId,
        int sequence)
    {
        return new TradeExecutionRecord
        {
            Id = executionId,
            TradeId = tradeId,
            Sequence = sequence,
            ExecutedAtUtc = ExecutedAtUtc.AddMinutes(sequence - 1),
            Side = ExecutionSide.Buy,
            Quantity = 1m,
            Price = 20_000m,
            Commission = 0m,
            Fees = 0m,
            ExternalExecutionId = null,
            ExternalOrderId = null,
            BrokerSymbol = "NQ",
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
