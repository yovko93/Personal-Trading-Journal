using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class ManualTradePersistenceIntegrationTests
{
    private static readonly DateTimeOffset ReferenceCreatedAtUtc =
        new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EntryExecutedAtUtc =
        new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExitExecutedAtUtc =
        new(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 10, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateManualTradeUseCasePersistsAndRehydratesClosedTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore accountStore =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        IInstrumentStore instrumentStore =
            database.ServiceProvider.GetRequiredService<IInstrumentStore>();
        ITradeStore tradeStore =
            database.ServiceProvider.GetRequiredService<ITradeStore>();
        var account = new TradingAccount(
            "Manual Trade Account",
            TradingAccountType.Personal,
            "Test Broker",
            "ACCOUNT-1",
            "USD",
            100000m,
            ReferenceCreatedAtUtc);
        var instrument = new Instrument(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            ReferenceCreatedAtUtc);
        await accountStore.AddAsync(account);
        await instrumentStore.AddAsync(instrument);
        var useCase = new CreateManualTradeUseCase(
            accountStore,
            instrumentStore,
            tradeStore,
            new FixedTimeProvider(CurrentUtc));
        var command = new CreateManualTradeCommand(
            account.Id,
            instrument.Id,
            TradeDirection.Long,
            2.5m,
            new ManualTradeExecutionInput(
                EntryExecutedAtUtc,
                21900.25m,
                1.50m,
                0.25m),
            new ManualTradeExecutionInput(
                ExitExecutedAtUtc,
                21950.75m,
                1.75m,
                0.35m));

        Guid tradeId = await useCase.ExecuteAsync(command);

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        TradeRecord tradeRecord = await readContext.Trades
            .AsNoTracking()
            .SingleAsync(record => record.Id == tradeId);
        List<TradeExecutionRecord> executionRecords = await readContext.TradeExecutions
            .AsNoTracking()
            .Where(record => record.TradeId == tradeId)
            .OrderBy(record => record.Sequence)
            .ToListAsync();

        Assert.Equal(account.Id, tradeRecord.TradingAccountId);
        Assert.Equal(instrument.Id, tradeRecord.InstrumentId);
        Assert.Equal(20m, tradeRecord.PricingPointValue);
        Assert.Equal("USD", tradeRecord.PricingCurrency);
        Assert.Equal(CurrentUtc, tradeRecord.CreatedAtUtc);
        Assert.Equal(CurrentUtc, tradeRecord.UpdatedAtUtc);
        Assert.Equal(2, executionRecords.Count);
        Assert.Equal([1, 2], executionRecords.Select(record => record.Sequence));
        Assert.Equal(ExecutionSide.Buy, executionRecords[0].Side);
        Assert.Equal(ExecutionSide.Sell, executionRecords[1].Side);
        Assert.All(executionRecords, record => Assert.Equal(2.5m, record.Quantity));
        Assert.Equal(21900.25m, executionRecords[0].Price);
        Assert.Equal(21950.75m, executionRecords[1].Price);
        Assert.Equal(1.50m, executionRecords[0].Commission);
        Assert.Equal(1.75m, executionRecords[1].Commission);
        Assert.Equal(0.25m, executionRecords[0].Fees);
        Assert.Equal(0.35m, executionRecords[1].Fees);
        Assert.Equal(EntryExecutedAtUtc, executionRecords[0].ExecutedAtUtc);
        Assert.Equal(ExitExecutedAtUtc, executionRecords[1].ExecutedAtUtc);

        Trade trade = TradePersistenceMapper.ToDomain(tradeRecord, executionRecords);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(20m, trade.Pricing.PointValue);
        Assert.Equal("USD", trade.Pricing.Currency);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
