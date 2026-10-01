using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Calendar;

public sealed class TradingCalendarReaderTests
{
    [Fact]
    public async Task SeptemberGridIncludesAdjacentDatesAndFullSundayWeekTotals()
    {
        // Sep 2026 starts Tuesday and ends Wednesday: Aug 31 through Oct 4 is visible.
        var source = new StubReader(Fact(new(2026, 8, 31, 16, 0, 0, TimeSpan.Zero), 2m),
            Fact(new(2026, 9, 5, 16, 0, 0, TimeSpan.Zero), 3m),
            Fact(new(2026, 9, 6, 16, 0, 0, TimeSpan.Zero), 4m),
            Fact(new(2026, 10, 4, 16, 0, 0, TimeSpan.Zero), 5m));
        var account = Guid.NewGuid();
        TradingCalendarMonth result = await new TradingCalendarReader(source).GetAsync(new(2026, 9, account));

        Assert.Equal(new DateOnly(2026, 8, 31), result.GridStart);
        Assert.Equal(new DateOnly(2026, 10, 4), result.GridEnd);
        Assert.Equal(35, result.GridDates.Count);
        Assert.Equal(DayOfWeek.Monday, result.GridDates[0].DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, result.GridDates[^1].DayOfWeek);
        Assert.Equal(account, source.LastQuery!.TradingAccountId);
        Assert.Equal(result.GridStart, source.LastQuery.ClosedFromNewYork);
        Assert.Equal(result.GridEnd, source.LastQuery.ClosedThroughNewYork);
        Assert.Equal(1, source.Calls);

        TradingCalendarWeek first = Assert.Single(result.Currencies).Weeks[0];
        Assert.Equal(3, first.ClosedTradeCount);
        Assert.Equal(9m, first.EffectiveNetTotal); // Includes Sunday, after the future Saturday summary cell.
        Assert.Equal(7, first.Days.Count);
        Assert.False(first.Days[0].IsInDisplayedMonth);
        Assert.True(first.Days[1].IsInDisplayedMonth);
        Assert.Equal(4m, first.Days[6].EffectiveNetTotal);
        TradingCalendarWeek last = result.Currencies[0].Weeks[^1];
        Assert.Equal(5m, last.EffectiveNetTotal);
        Assert.False(last.Days[^1].IsInDisplayedMonth);
    }

    [Fact]
    public async Task EmptyZeroEstimatedUnavailableAndCurrenciesRemainDistinct()
    {
        var source = new StubReader(Fact(new(2026, 9, 10, 16, 0, 0, TimeSpan.Zero), 0m),
            Fact(new(2026, 9, 11, 16, 0, 0, TimeSpan.Zero), -285m, costs: null),
            Fact(new(2026, 9, 12, 16, 0, 0, TimeSpan.Zero), null, costs: null),
            Fact(new(2026, 9, 11, 16, 0, 0, TimeSpan.Zero), 20m, currency: "EUR"));
        TradingCalendarMonth result = await new TradingCalendarReader(source).GetAsync(new(2026, 9));
        TradingCalendarCurrency usd = Assert.Single(result.Currencies, c => c.Currency == "USD");
        TradingCalendarCurrency eur = Assert.Single(result.Currencies, c => c.Currency == "EUR");
        TradingCalendarWeek usdWeek = usd.Weeks[1];
        TradingCalendarDay empty = usdWeek.Days[2];
        TradingCalendarDay zero = usdWeek.Days[3];
        TradingCalendarDay estimated = usdWeek.Days[4];
        TradingCalendarDay unavailable = usdWeek.Days[5];
        Assert.Null(empty.Metrics);
        Assert.Equal(0, empty.ClosedTradeCount);
        Assert.Equal(0m, zero.EffectiveNetTotal);
        Assert.NotNull(zero.Metrics);
        Assert.False(zero.IsEstimated);
        Assert.Equal(-285m, estimated.EffectiveNetTotal);
        Assert.True(estimated.IsEstimated);
        Assert.Null(estimated.Metrics!.Net.Total);
        Assert.Null(unavailable.EffectiveNetTotal);
        Assert.Equal(1, unavailable.ClosedTradeCount);
        Assert.Null(usdWeek.EffectiveNetTotal); // Incomplete, not the known subset.
        Assert.Equal(-285m, usdWeek.Metrics!.EffectiveNet.KnownSubtotal);
        Assert.Equal(20m, eur.Weeks[1].EffectiveNetTotal);
        Assert.Equal(1, eur.Weeks[1].ClosedTradeCount);
    }

    [Fact]
    public async Task EmptyMonthStillHasGridAndCancellationIsForwarded()
    {
        var source = new StubReader();
        var reader = new TradingCalendarReader(source);
        TradingCalendarMonth empty = await reader.GetAsync(new(2021, 2));
        Assert.Equal(28, empty.GridDates.Count);
        Assert.Empty(empty.Currencies);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(new(2026, 9), cancellation.Token));
        Assert.Equal(1, source.Calls);
        using var forward = new CancellationTokenSource();
        await reader.GetAsync(new(2026, 9), forward.Token);
        Assert.Equal(forward.Token, source.LastToken);
    }

    [Fact]
    public void InvalidInputsAreRejectedBeforeAnyRead()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingCalendarQuery(2026, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingCalendarQuery(0, 1));
        Assert.Throws<ArgumentException>(() => new TradingCalendarQuery(2026, 9, Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingCalendarQuery(9999, 12));
    }

    private static TradeAnalyticsFact Fact(DateTimeOffset closed, decimal? gross, decimal? costs = 0m,
        string currency = "USD") => new(Guid.NewGuid(), TradeStatus.Closed, closed.AddHours(-1), closed,
        currency, null, gross, costs, gross - costs);

    private sealed class StubReader(params TradeAnalyticsFact[] facts) : IDashboardAnalyticsReader
    {
        public int Calls { get; private set; }
        public DashboardAnalyticsQuery? LastQuery { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<DashboardAnalyticsSnapshot> GetAsync(DashboardAnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastQuery = query;
            LastToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            TradeAnalyticsFact[] selected = facts.Where(f => f.ClosedAtUtc >= query.ClosedFromUtc &&
                f.ClosedAtUtc < query.ClosedBeforeUtc).ToArray();
            return Task.FromResult(DashboardMetricCalculator.Calculate(selected, cancellationToken));
        }
    }
}
