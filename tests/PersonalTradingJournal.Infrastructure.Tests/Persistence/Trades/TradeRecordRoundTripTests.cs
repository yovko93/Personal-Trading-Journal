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

public sealed class TradeRecordRoundTripTests
{
    private static readonly Guid TradeId =
        Guid.Parse("30184aac-c9be-467f-897d-6f0100065953");

    private static readonly Guid TradingAccountId =
        Guid.Parse("ed750d83-67f6-43d9-b009-0d3af99481f1");

    private static readonly Guid InstrumentId =
        Guid.Parse("410e625f-d26e-47c3-b830-141b0150d218");

    private static readonly Guid StrategyId =
        Guid.Parse("de88659e-a495-43ac-91a3-a7e4f104d22c");

    private static readonly Guid TradingSetupId =
        Guid.Parse("b7f2ef28-e923-4a70-8c39-09e365061e9d");

    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 7, 15, 13, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 15, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnclassifiedTradeRecordRoundTripsWithRequiredReferences()
    {
        Trade original = StartTrade();
        TradeRecord expected = TradePersistenceMapper.ToRecord(original);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredReferences(writeContext);
            writeContext.Trades.Add(expected);
            writeContext.SaveChanges();
        }

        TradeRecord persisted;
        using (var readContext = new JournalDbContext(options))
        {
            persisted = readContext.Trades
                .AsNoTracking()
                .Single(record => record.Id == original.Id);
        }

        AssertTradeRecord(expected, persisted);
        Assert.Null(persisted.StrategyId);
        Assert.Null(persisted.TradingSetupId);
    }

    [Fact]
    public void ClassifiedTradeRecordRoundTripsWithOptionalReferences()
    {
        Trade original = StartTrade();
        original.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(1));
        TradeRecord expected = TradePersistenceMapper.ToRecord(original);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredReferences(writeContext);
            writeContext.Strategies.Add(CreateStrategyRecord());
            writeContext.TradingSetups.Add(CreateTradingSetupRecord());
            writeContext.Trades.Add(expected);
            writeContext.SaveChanges();
        }

        TradeRecord persisted;
        using (var readContext = new JournalDbContext(options))
        {
            persisted = readContext.Trades
                .AsNoTracking()
                .Single(record => record.Id == original.Id);
        }

        AssertTradeRecord(expected, persisted);
        Assert.Equal(StrategyId, persisted.StrategyId);
        Assert.Equal(TradingSetupId, persisted.TradingSetupId);
    }

    [Fact]
    public void ModelMapsTradeRecordWithRestrictiveReferenceForeignKeys()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType = context.Model.FindEntityType(typeof(TradeRecord))!;

        Assert.Equal("Trades", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(TradeRecord.Id))!.ValueGenerated);
        Assert.Equal(
            8,
            entityType.FindProperty(nameof(TradeRecord.PricingCurrency))!.GetMaxLength());
        AssertForeignKey(
            entityType,
            nameof(TradeRecord.TradingAccountId),
            typeof(TradingAccountRecord),
            isRequired: true);
        AssertForeignKey(
            entityType,
            nameof(TradeRecord.InstrumentId),
            typeof(InstrumentRecord),
            isRequired: true);
        AssertForeignKey(
            entityType,
            nameof(TradeRecord.StrategyId),
            typeof(StrategyRecord),
            isRequired: false);
        AssertForeignKey(
            entityType,
            nameof(TradeRecord.TradingSetupId),
            typeof(TradingSetupRecord),
            isRequired: false);
        Assert.Empty(entityType.GetNavigations());
    }

    private static void AssertForeignKey(
        IEntityType entityType,
        string propertyName,
        Type principalType,
        bool isRequired)
    {
        IForeignKey foreignKey = Assert.Single(
            entityType.GetForeignKeys(),
            candidate => candidate.Properties.Single().Name == propertyName);

        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(isRequired, foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private static void AssertTradeRecord(TradeRecord expected, TradeRecord actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TradingAccountId, actual.TradingAccountId);
        Assert.Equal(expected.InstrumentId, actual.InstrumentId);
        Assert.Equal(expected.PricingPointValue, actual.PricingPointValue);
        Assert.Equal(expected.PricingCurrency, actual.PricingCurrency);
        Assert.Equal(expected.StrategyId, actual.StrategyId);
        Assert.Equal(expected.TradingSetupId, actual.TradingSetupId);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static Trade StartTrade()
    {
        var openingExecution = new TradeExecution(
            TradeId,
            1,
            ExecutedAtUtc,
            ExecutionSide.Buy,
            2m,
            20_000m,
            1m,
            0.25m,
            null,
            null,
            "NQ");

        return Trade.Start(
            TradingAccountId,
            InstrumentId,
            new TradePricingSnapshot(20m, "USD"),
            openingExecution,
            CreatedAtUtc);
    }

    private static void AddRequiredReferences(JournalDbContext context)
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

    private static StrategyRecord CreateStrategyRecord()
    {
        return new StrategyRecord
        {
            Id = StrategyId,
            Name = "ICT 2022 Model",
            Description = null,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradingSetupRecord CreateTradingSetupRecord()
    {
        return new TradingSetupRecord
        {
            Id = TradingSetupId,
            Name = "Liquidity Sweep + MSS + FVG",
            Description = null,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static DbContextOptions<JournalDbContext> CreateOptions(
        SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection)
            .Options;
    }
}
