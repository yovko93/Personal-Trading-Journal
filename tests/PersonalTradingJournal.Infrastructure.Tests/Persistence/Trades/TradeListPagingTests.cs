using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeListPagingTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetPageAsyncPaginatesAllTradesWithoutDuplicates()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        var expectedIds = new HashSet<Guid>();
        for (int index = 0; index < 45; index++)
        {
            Guid id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}");
            expectedIds.Add(id);
            await PersistTradeAsync(database, OpenTrade(
                account.Id,
                instrument.Id,
                id,
                CreatedAtUtc.AddMinutes(index),
                1m,
                100m + index));
        }

        ITradeListReader reader = GetReader(database);
        TradeListPage first = await reader.GetPageAsync(Query(1));
        TradeListPage second = await reader.GetPageAsync(Query(2));
        TradeListPage third = await reader.GetPageAsync(Query(3));

        Assert.Equal(20, first.Items.Count);
        Assert.Equal(20, second.Items.Count);
        Assert.Equal(5, third.Items.Count);
        Assert.All([first, second, third], page => Assert.Equal(45, page.TotalCount));
        Guid[] actualIds = first.Items.Concat(second.Items).Concat(third.Items)
            .Select(item => item.Id)
            .ToArray();
        Assert.Equal(45, actualIds.Distinct().Count());
        Assert.True(expectedIds.SetEquals(actualIds));
    }

    [Fact]
    public async Task OpenedSortSupportsBothDirectionsAndTradeIdTieBreaker()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        Guid lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Guid newestId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        DateTimeOffset tiedTime = CreatedAtUtc.AddDays(2);
        await PersistTradeAsync(database, OpenTrade(
            account.Id, instrument.Id, higherId, tiedTime, 1m, 100m));
        await PersistTradeAsync(database, OpenTrade(
            account.Id, instrument.Id, newestId, tiedTime.AddDays(1), 1m, 100m));
        await PersistTradeAsync(database, OpenTrade(
            account.Id, instrument.Id, lowerId, tiedTime, 1m, 100m));

        ITradeListReader reader = GetReader(database);
        TradeListPage ascending = await reader.GetPageAsync(Query(
            1,
            TradeListSortColumn.OpenedAtUtc,
            TradeListSortDirection.Ascending));
        TradeListPage descending = await reader.GetPageAsync(Query(
            1,
            TradeListSortColumn.OpenedAtUtc,
            TradeListSortDirection.Descending));

        Assert.Equal([lowerId, higherId, newestId], ascending.Items.Select(x => x.Id));
        Assert.Equal([newestId, lowerId, higherId], descending.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task LiveInstrumentAndAccountLabelsSortInBothDirections()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount alpha, Instrument es) = await PersistReferencesAsync(
            database, "Alpha", "ES");
        (TradingAccount zulu, Instrument nq) = await PersistReferencesAsync(
            database, "Zulu", "NQ", "2");
        Guid firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Guid thirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        await PersistTradeAsync(database, OpenTrade(
            zulu.Id, es.Id, thirdId, CreatedAtUtc, 1m, 100m));
        await PersistTradeAsync(database, OpenTrade(
            alpha.Id, nq.Id, secondId, CreatedAtUtc, 1m, 100m));
        await PersistTradeAsync(database, OpenTrade(
            alpha.Id, es.Id, firstId, CreatedAtUtc, 1m, 100m));

        ITradeListReader reader = GetReader(database);
        TradeListPage instrumentAsc = await reader.GetPageAsync(Query(
            1, TradeListSortColumn.Instrument, TradeListSortDirection.Ascending));
        TradeListPage instrumentDesc = await reader.GetPageAsync(Query(
            1, TradeListSortColumn.Instrument, TradeListSortDirection.Descending));
        TradeListPage accountAsc = await reader.GetPageAsync(Query(
            1, TradeListSortColumn.Account, TradeListSortDirection.Ascending));
        TradeListPage accountDesc = await reader.GetPageAsync(Query(
            1, TradeListSortColumn.Account, TradeListSortDirection.Descending));

        Assert.Equal([firstId, thirdId, secondId], instrumentAsc.Items.Select(x => x.Id));
        Assert.Equal([secondId, firstId, thirdId], instrumentDesc.Items.Select(x => x.Id));
        Assert.Equal([firstId, secondId, thirdId], accountAsc.Items.Select(x => x.Id));
        Assert.Equal([thirdId, firstId, secondId], accountDesc.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task ExactDecimalKeysOrderAverageEntryPriceAndOpenQuantityNumerically()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        decimal[] prices = [10m, 1.20m, 29131.25m, 2m, 1.25m];
        decimal[] quantities = [10m, 1m, 2m, 5m, 3m];
        for (int index = 0; index < prices.Length; index++)
        {
            await PersistTradeAsync(database, OpenTrade(
                account.Id,
                instrument.Id,
                Guid.NewGuid(),
                CreatedAtUtc.AddMinutes(index),
                quantities[index],
                prices[index]));
        }

        ITradeListReader reader = GetReader(database);
        TradeListPage priceAsc = await reader.GetPageAsync(Query(
            1,
            TradeListSortColumn.AverageEntryPrice,
            TradeListSortDirection.Ascending));
        TradeListPage priceDesc = await reader.GetPageAsync(Query(
            1,
            TradeListSortColumn.AverageEntryPrice,
            TradeListSortDirection.Descending));
        TradeListPage quantityAsc = await reader.GetPageAsync(Query(
            1,
            TradeListSortColumn.OpenQuantity,
            TradeListSortDirection.Ascending));
        TradeListPage quantityDesc = await reader.GetPageAsync(Query(
            1,
            TradeListSortColumn.OpenQuantity,
            TradeListSortDirection.Descending));

        Assert.Equal(prices.Order(), priceAsc.Items.Select(x => x.AverageEntryPrice));
        Assert.Equal(prices.OrderDescending(), priceDesc.Items.Select(x => x.AverageEntryPrice));
        Assert.Equal(quantities.Order(), quantityAsc.Items.Select(x => x.OpenQuantity));
        Assert.Equal(quantities.OrderDescending(), quantityDesc.Items.Select(x => x.OpenQuantity));
    }

    [Fact]
    public async Task NetPnlSortsExactlyWithNullAlwaysLast()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        decimal[] outcomes = [-100m, -2m, 0m, 2m, 100m];
        for (int index = 0; index < outcomes.Length; index++)
        {
            await PersistTradeAsync(database, ClosedTrade(
                account.Id,
                instrument.Id,
                Guid.NewGuid(),
                CreatedAtUtc.AddMinutes(index),
                outcomes[index]));
        }

        await PersistTradeAsync(database, OpenTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            CreatedAtUtc.AddHours(1),
            1m,
            100m));

        ITradeListReader reader = GetReader(database);
        TradeListPage ascending = await reader.GetPageAsync(Query(
            1, TradeListSortColumn.NetPnL, TradeListSortDirection.Ascending));
        TradeListPage descending = await reader.GetPageAsync(Query(
            1, TradeListSortColumn.NetPnL, TradeListSortDirection.Descending));

        Assert.Equal(
            outcomes.Cast<decimal?>().Append(null),
            ascending.Items.Select(x => x.NetPnL));
        Assert.Equal(
            outcomes.OrderDescending().Cast<decimal?>().Append(null),
            descending.Items.Select(x => x.NetPnL));
    }

    [Fact]
    public async Task GlobalSortIsAppliedBeforeCrossPageSkipAndTake()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        decimal[] shuffledPrices = Enumerable.Range(1, 35)
            .Select(value => (decimal)value)
            .OrderBy(value => (value * 17m) % 37m)
            .ToArray();
        foreach (decimal price in shuffledPrices)
        {
            await PersistTradeAsync(database, OpenTrade(
                account.Id,
                instrument.Id,
                Guid.NewGuid(),
                CreatedAtUtc.AddMinutes((double)price),
                1m,
                price));
        }

        ITradeListReader reader = GetReader(database);
        TradeListPage first = await reader.GetPageAsync(Query(
            1,
            TradeListSortColumn.AverageEntryPrice,
            TradeListSortDirection.Ascending));
        TradeListPage second = await reader.GetPageAsync(Query(
            2,
            TradeListSortColumn.AverageEntryPrice,
            TradeListSortDirection.Ascending));

        Assert.Equal(
            Enumerable.Range(1, 35).Select(value => (decimal)value),
            first.Items.Concat(second.Items).Select(item => item.AverageEntryPrice));
    }

    private static ITradeListReader GetReader(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeListReader>();

    private static TradeListQuery Query(
        int pageNumber,
        TradeListSortColumn sortColumn = TradeListSortColumn.OpenedAtUtc,
        TradeListSortDirection sortDirection = TradeListSortDirection.Descending) =>
        new(pageNumber, 20, sortColumn, sortDirection);

    private static async Task<(TradingAccount Account, Instrument Instrument)>
        PersistReferencesAsync(
            ReaderTestDatabase database,
            string accountName = "Paging Account",
            string instrumentSymbol = "ES",
            string suffix = "1")
    {
        var account = new TradingAccount(
            accountName,
            TradingAccountType.Personal,
            "Provider",
            $"ACCOUNT-{suffix}",
            "USD",
            100000m,
            CreatedAtUtc);
        var instrument = new Instrument(
            instrumentSymbol,
            $"{instrumentSymbol} Display",
            AssetClass.Futures,
            "CME",
            "USD",
            0.01m,
            0.01m,
            CreatedAtUtc);
        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>()
            .AddAsync(instrument);
        return (account, instrument);
    }

    private static Task PersistTradeAsync(ReaderTestDatabase database, Trade trade) =>
        database.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(trade);

    private static Trade OpenTrade(
        Guid accountId,
        Guid instrumentId,
        Guid tradeId,
        DateTimeOffset openedAtUtc,
        decimal quantity,
        decimal price) => Trade.Rehydrate(
            tradeId,
            accountId,
            instrumentId,
            new TradePricingSnapshot(1m, "USD"),
            tradingSetupId: null,
            [Execution(tradeId, 1, openedAtUtc, ExecutionSide.Buy, quantity, price)],
            openedAtUtc,
            openedAtUtc);

    private static Trade ClosedTrade(
        Guid accountId,
        Guid instrumentId,
        Guid tradeId,
        DateTimeOffset openedAtUtc,
        decimal outcome) => Trade.Rehydrate(
            tradeId,
            accountId,
            instrumentId,
            new TradePricingSnapshot(1m, "USD"),
            tradingSetupId: null,
            [
                Execution(tradeId, 1, openedAtUtc, ExecutionSide.Buy, 1m, 200m),
                Execution(
                    tradeId,
                    2,
                    openedAtUtc.AddMinutes(1),
                    ExecutionSide.Sell,
                    1m,
                    200m + outcome),
            ],
            openedAtUtc,
            openedAtUtc.AddMinutes(1));

    private static TradeExecution Execution(
        Guid tradeId,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price) => TradeExecution.Rehydrate(
            Guid.NewGuid(),
            tradeId,
            sequence,
            executedAtUtc,
            side,
            quantity,
            price,
            commission: 0m,
            fees: 0m,
            externalExecutionId: null,
            externalOrderId: null,
            brokerSymbol: "TEST");
}
