using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeDetailReaderTests
{
    private static readonly DateTimeOffset ReferenceCreatedAtUtc =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenTradeDoesNotExist()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeDetailReader reader = GetReader(database);

        TradeDetail? detail = await reader.GetByIdAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetByIdAsyncRejectsEmptyTradeId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeDetailReader reader = GetReader(database);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => reader.GetByIdAsync(Guid.Empty));

        Assert.Equal("tradeId", exception.ParamName);
    }

    [Fact]
    public async Task GetByIdAsyncProjectsOpenTradeAndExecutionExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(
            database,
            "Open Detail Account",
            "NQ",
            "Nasdaq-100 E-mini");
        Guid tradeId = Guid.NewGuid();
        Guid executionId = Guid.NewGuid();
        DateTimeOffset openedAtUtc = Utc(10, 9);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            tradeId,
            Utc(10, 12),
            20m,
            "USD",
            new ExecutionFact(
                executionId,
                openedAtUtc,
                ExecutionSide.Buy,
                2m,
                100m,
                1.5m,
                0.5m,
                "NQH7",
                "EXEC-1",
                "ORDER-1"));
        await PersistTradeAsync(database, trade);

        TradeDetail detail = Assert.IsType<TradeDetail>(
            await GetReader(database).GetByIdAsync(tradeId));

        Assert.Equal(tradeId, detail.Id);
        Assert.Equal(account.Id, detail.TradingAccountId);
        Assert.Equal("Open Detail Account", detail.TradingAccountName);
        Assert.Equal(instrument.Id, detail.InstrumentId);
        Assert.Equal("NQ", detail.InstrumentSymbol);
        Assert.Equal("Nasdaq-100 E-mini", detail.InstrumentDisplayName);
        Assert.Equal(TradeDirection.Long, detail.Direction);
        Assert.Equal(TradeStatus.Open, detail.Status);
        Assert.Equal(openedAtUtc, detail.OpenedAtUtc);
        Assert.Null(detail.ClosedAtUtc);
        Assert.Equal(2m, detail.OpenQuantity);
        Assert.Equal(100m, detail.AverageEntryPrice);
        Assert.Null(detail.AverageExitPrice);
        Assert.Equal(2m, detail.TotalCosts);
        Assert.Null(detail.GrossPnL);
        Assert.Null(detail.NetPnL);
        Assert.Equal(20m, detail.PricingPointValue);
        Assert.Equal("USD", detail.Currency);

        TradeExecutionDetailItem execution = Assert.Single(detail.Executions);
        Assert.Equal(executionId, execution.Id);
        Assert.Equal(1, execution.Sequence);
        Assert.Equal(openedAtUtc, execution.ExecutedAtUtc);
        Assert.Equal(ExecutionSide.Buy, execution.Side);
        Assert.Equal(2m, execution.Quantity);
        Assert.Equal(100m, execution.Price);
        Assert.Equal(1.5m, execution.Commission);
        Assert.Equal(0.5m, execution.Fees);
        Assert.Equal(2m, execution.TotalCosts);
        Assert.Equal("NQH7", execution.BrokerSymbol);
        Assert.Equal("EXEC-1", execution.ExternalExecutionId);
        Assert.Equal("ORDER-1", execution.ExternalOrderId);
    }

    [Fact]
    public async Task GetByIdAsyncProjectsClosedTradeEconomicsExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) =
            await PersistReferencesAsync(database);
        DateTimeOffset openedAtUtc = Utc(10, 9);
        DateTimeOffset closedAtUtc = openedAtUtc.AddHours(1);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 12),
            20m,
            "USD",
            Execution(openedAtUtc, ExecutionSide.Buy, 2m, 100m, 1m, 0.5m),
            Execution(closedAtUtc, ExecutionSide.Sell, 2m, 110m, 1.5m, 0.25m));
        await PersistTradeAsync(database, trade);

        TradeDetail detail = Assert.IsType<TradeDetail>(
            await GetReader(database).GetByIdAsync(trade.Id));

        Assert.Equal(TradeStatus.Closed, detail.Status);
        Assert.Equal(0m, detail.OpenQuantity);
        Assert.Equal(openedAtUtc, detail.OpenedAtUtc);
        Assert.Equal(closedAtUtc, detail.ClosedAtUtc);
        Assert.Equal(100m, detail.AverageEntryPrice);
        Assert.Equal(110m, detail.AverageExitPrice);
        Assert.Equal(3.25m, detail.TotalCosts);
        Assert.Equal(400m, detail.GrossPnL);
        Assert.Equal(396.75m, detail.NetPnL);
        Assert.Equal(20m, detail.PricingPointValue);
        Assert.Equal("USD", detail.Currency);
        Assert.Equal([1, 2], detail.Executions.Select(execution => execution.Sequence));
    }

    [Fact]
    public async Task GetByIdAsyncProjectsCompleteScaleInPartialExitLifecycleInSequence()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) =
            await PersistReferencesAsync(database);
        Guid firstId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        Guid secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Guid thirdId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        DateTimeOffset openedAtUtc = Utc(10, 9);
        DateTimeOffset sharedTimestampUtc = openedAtUtc.AddHours(1);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 12),
            20m,
            "USD",
            new ExecutionFact(
                firstId,
                openedAtUtc,
                ExecutionSide.Buy,
                2m,
                100m,
                1m,
                0.5m,
                null,
                null,
                null),
            new ExecutionFact(
                secondId,
                sharedTimestampUtc,
                ExecutionSide.Buy,
                1m,
                110m,
                0.75m,
                0.25m,
                "NQH7",
                "EXEC-2",
                "ORDER-2"),
            new ExecutionFact(
                thirdId,
                sharedTimestampUtc,
                ExecutionSide.Sell,
                1m,
                120m,
                0.5m,
                0.25m,
                "NQH7",
                null,
                "ORDER-3"));
        await PersistTradeAsync(database, trade);

        TradeDetail detail = Assert.IsType<TradeDetail>(
            await GetReader(database).GetByIdAsync(trade.Id));

        Assert.Equal(TradeDirection.Long, detail.Direction);
        Assert.Equal(TradeStatus.Open, detail.Status);
        Assert.Equal(2m, detail.OpenQuantity);
        Assert.Equal(310m / 3m, detail.AverageEntryPrice);
        Assert.Equal(120m, detail.AverageExitPrice);
        Assert.Null(detail.ClosedAtUtc);
        Assert.Null(detail.GrossPnL);
        Assert.Null(detail.NetPnL);
        Assert.Equal(3.25m, detail.TotalCosts);
        Assert.Equal([1, 2, 3], detail.Executions.Select(item => item.Sequence));
        Assert.Equal(
            [firstId, secondId, thirdId],
            detail.Executions.Select(item => item.Id));
        Assert.Equal(sharedTimestampUtc, detail.Executions[1].ExecutedAtUtc);
        Assert.Equal(sharedTimestampUtc, detail.Executions[2].ExecutedAtUtc);
        Assert.Null(detail.Executions[0].BrokerSymbol);
        Assert.Null(detail.Executions[0].ExternalExecutionId);
        Assert.Null(detail.Executions[0].ExternalOrderId);
        Assert.Equal("NQH7", detail.Executions[1].BrokerSymbol);
        Assert.Equal("EXEC-2", detail.Executions[1].ExternalExecutionId);
        Assert.Equal("ORDER-2", detail.Executions[1].ExternalOrderId);
        Assert.Null(detail.Executions[2].ExternalExecutionId);
        Assert.Equal("ORDER-3", detail.Executions[2].ExternalOrderId);
        Assert.Equal(
            [1.5m, 1m, 0.75m],
            detail.Executions.Select(item => item.TotalCosts));
    }

    [Fact]
    public async Task GetByIdAsyncIncludesTradeWithInactiveReferences()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(
            database,
            "Retired Account",
            "ES",
            "S&P 500 E-mini");
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 12),
            50m,
            "USD",
            Execution(Utc(10, 9), ExecutionSide.Buy, 1m, 5000m, 0m, 0m));
        await PersistTradeAsync(database, trade);
        DateTimeOffset deactivatedAtUtc = Utc(11, 12);
        account.Deactivate(deactivatedAtUtc);
        instrument.Deactivate(deactivatedAtUtc);
        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>()
            .UpdateAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>()
            .UpdateAsync(instrument);

        TradeDetail detail = Assert.IsType<TradeDetail>(
            await GetReader(database).GetByIdAsync(trade.Id));

        Assert.Equal("Retired Account", detail.TradingAccountName);
        Assert.Equal("ES", detail.InstrumentSymbol);
        Assert.Equal("S&P 500 E-mini", detail.InstrumentDisplayName);
    }

    [Fact]
    public async Task GetByIdAsyncUsesCurrentLabelsAndHistoricalTradeEconomics()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(
            database,
            "Original Account",
            "NQ",
            "Nasdaq-100 E-mini");
        DateTimeOffset openedAtUtc = Utc(10, 9);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 12),
            20m,
            "USD",
            Execution(openedAtUtc, ExecutionSide.Buy, 2m, 100m, 1m, 0m),
            Execution(openedAtUtc.AddHours(1), ExecutionSide.Sell, 2m, 110m, 1m, 0m));
        await PersistTradeAsync(database, trade);

        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            TradingAccountRecord currentAccount = await context.TradingAccounts
                .SingleAsync(record => record.Id == account.Id);
            InstrumentRecord currentInstrument = await context.Instruments
                .SingleAsync(record => record.Id == instrument.Id);
            currentAccount.Name = "Renamed Account";
            currentInstrument.Symbol = "MNQ";
            currentInstrument.DisplayName = "Micro Nasdaq-100";
            currentInstrument.Currency = "EUR";
            currentInstrument.TickValue = 2.5m;
            await context.SaveChangesAsync();
        }

        TradeDetail detail = Assert.IsType<TradeDetail>(
            await GetReader(database).GetByIdAsync(trade.Id));

        Assert.Equal("Renamed Account", detail.TradingAccountName);
        Assert.Equal("MNQ", detail.InstrumentSymbol);
        Assert.Equal("Micro Nasdaq-100", detail.InstrumentDisplayName);
        Assert.Equal(20m, detail.PricingPointValue);
        Assert.Equal("USD", detail.Currency);
        Assert.Equal(400m, detail.GrossPnL);
        Assert.Equal(398m, detail.NetPnL);
    }

    [Fact]
    public async Task GetByIdAsyncUsesFreshContextForSubsequentRead()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(
            database,
            "Initial Account",
            "NQ",
            "Initial Instrument");
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 12),
            20m,
            "USD",
            Execution(Utc(10, 9), ExecutionSide.Buy, 1m, 100m, 0m, 0m));
        await PersistTradeAsync(database, trade);
        ITradeDetailReader reader = GetReader(database);
        TradeDetail first = Assert.IsType<TradeDetail>(
            await reader.GetByIdAsync(trade.Id));

        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            TradingAccountRecord currentAccount = await context.TradingAccounts
                .SingleAsync(record => record.Id == account.Id);
            InstrumentRecord currentInstrument = await context.Instruments
                .SingleAsync(record => record.Id == instrument.Id);
            currentAccount.Name = "Current Account";
            currentInstrument.DisplayName = "Current Instrument";
            await context.SaveChangesAsync();
        }

        TradeDetail second = Assert.IsType<TradeDetail>(
            await reader.GetByIdAsync(trade.Id));

        Assert.Equal("Initial Account", first.TradingAccountName);
        Assert.Equal("Initial Instrument", first.InstrumentDisplayName);
        Assert.Equal("Current Account", second.TradingAccountName);
        Assert.Equal("Current Instrument", second.InstrumentDisplayName);
    }

    [Fact]
    public async Task GetByIdAsyncPropagatesPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeDetailReader reader = GetReader(database);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.GetByIdAsync(Guid.NewGuid(), cancellationSource.Token));
    }

    private static ITradeDetailReader GetReader(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeDetailReader>();

    private static async Task<(TradingAccount Account, Instrument Instrument)>
        PersistReferencesAsync(
            ReaderTestDatabase database,
            string accountName = "Detail Account",
            string instrumentSymbol = "NQ",
            string instrumentDisplayName = "Nasdaq-100 E-mini")
    {
        var account = new TradingAccount(
            accountName,
            TradingAccountType.Personal,
            "Reference Provider",
            "ACCOUNT-1",
            "USD",
            100000m,
            ReferenceCreatedAtUtc);
        var instrument = new Instrument(
            instrumentSymbol,
            instrumentDisplayName,
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            ReferenceCreatedAtUtc);
        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>()
            .AddAsync(instrument);

        return (account, instrument);
    }

    private static Task PersistTradeAsync(ReaderTestDatabase database, Trade trade) =>
        database.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(trade);

    private static Trade CreateTrade(
        Guid accountId,
        Guid instrumentId,
        Guid tradeId,
        DateTimeOffset createdAtUtc,
        decimal pointValue,
        string currency,
        params ExecutionFact[] executionFacts)
    {
        TradeExecution[] executions = executionFacts
            .Select((fact, index) => TradeExecution.Rehydrate(
                fact.Id,
                tradeId,
                index + 1,
                fact.ExecutedAtUtc,
                fact.Side,
                fact.Quantity,
                fact.Price,
                fact.Commission,
                fact.Fees,
                fact.ExternalExecutionId,
                fact.ExternalOrderId,
                fact.BrokerSymbol))
            .ToArray();

        return Trade.Rehydrate(
            tradeId,
            accountId,
            instrumentId,
            new TradePricingSnapshot(pointValue, currency),
            strategyId: null,
            tradingSetupId: null,
            executions,
            createdAtUtc,
            createdAtUtc);
    }

    private static ExecutionFact Execution(
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees) =>
        new(
            Guid.NewGuid(),
            executedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            null,
            null,
            null);

    private static DateTimeOffset Utc(int day, int hour) =>
        new(2026, 9, day, hour, 0, 0, TimeSpan.Zero);

    private sealed record ExecutionFact(
        Guid Id,
        DateTimeOffset ExecutedAtUtc,
        ExecutionSide Side,
        decimal Quantity,
        decimal Price,
        decimal Commission,
        decimal Fees,
        string? BrokerSymbol,
        string? ExternalExecutionId,
        string? ExternalOrderId);
}
