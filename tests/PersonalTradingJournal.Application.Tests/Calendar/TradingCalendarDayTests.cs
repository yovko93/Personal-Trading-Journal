using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Calendar;

public sealed class TradingCalendarDayTests
{
    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public void DateBoundsReuseNewYorkDstCalendar(int year, int month, int day, int hours)
    {
        var query = new TradingCalendarDayQuery(new(year, month, day));
        Assert.Equal(TimeSpan.FromHours(hours), query.ClosedBeforeUtc - query.ClosedFromUtc);
        Assert.Null(query.TradingAccountId);
        Assert.Throws<ArgumentException>(() => new TradingCalendarDayQuery(query.Date, Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingCalendarDayQuery(DateOnly.MaxValue));
    }

    [Fact]
    public void DetailsUseSameRowsForMetricsCurrenciesAndStableClosureOrder()
    {
        var date = new DateOnly(2026, 9, 5);
        var close = new DateTimeOffset(2026, 9, 5, 16, 0, 0, TimeSpan.Zero);
        TradeListItem a = Row(Guid.Parse("00000000-0000-0000-0000-000000000001"), close, -285m, null);
        TradeListItem b = Row(Guid.Parse("00000000-0000-0000-0000-000000000002"), close, 5m, 5m);
        TradeListItem c = Row(Guid.NewGuid(), close.AddMinutes(-1), 0m, 0m, "EUR");
        TradingCalendarDayDetails details = TradingCalendarDayDetails.Create(date, [c, b, a]);
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, details.Trades.Select(t => t.Id));
        Assert.Equal(3, details.ClosedTradeCount);
        Assert.Equal(new[] { "EUR", "USD" }, details.Currencies.Select(c => c.Currency));
        var usd = details.Currencies[1].Metrics;
        Assert.Equal(-280m, usd.EffectiveNet.Total);
        Assert.True(usd.EffectiveNet.IsEstimated);
        Assert.Null(usd.Net.Total);
        Assert.Equal(0m, details.Currencies[0].Metrics.EffectiveNet.Total);
    }

    [Fact]
    public void EmptyUnavailableWrongDateOpenAndCancellationRemainDistinct()
    {
        DateOnly date = new(2026, 9, 5);
        var close = new DateTimeOffset(2026, 9, 5, 16, 0, 0, TimeSpan.Zero);
        var empty = TradingCalendarDayDetails.Create(date, []);
        Assert.Empty(empty.Currencies);
        Assert.Equal(0, empty.ClosedTradeCount);
        var unknown = TradingCalendarDayDetails.Create(date, [Row(Guid.NewGuid(), close, null, null)]);
        Assert.Null(Assert.Single(unknown.Currencies).Metrics.EffectiveNet.Total);
        Assert.Equal(1, unknown.ClosedTradeCount);
        Assert.Throws<ArgumentException>(() => TradingCalendarDayDetails.Create(date.AddDays(1), unknown.Trades));
        Assert.Throws<ArgumentException>(() => TradingCalendarDayDetails.Create(date, [unknown.Trades[0] with { Status = TradeStatus.Open }]));
        Assert.ThrowsAny<OperationCanceledException>(() => TradingCalendarDayDetails.Create(date, [], new CancellationToken(true)));
    }

    private static TradeListItem Row(Guid id, DateTimeOffset closed, decimal? gross, decimal? net, string currency = "USD") =>
        new(id, Guid.NewGuid(), "Account", Guid.NewGuid(), "MNQ", TradeDirection.Long, TradeStatus.Closed,
            closed.AddMinutes(-1), closed, 0m, 100m, 101m, net.HasValue ? gross - net : null, gross, net, currency, 1m);
}
