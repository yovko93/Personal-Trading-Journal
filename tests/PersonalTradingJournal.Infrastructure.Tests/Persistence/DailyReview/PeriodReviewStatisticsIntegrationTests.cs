using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed partial class DailyReviewEvidenceReaderTests
{
    [Theory]
    [InlineData(3, 8, false)] [InlineData(3, 8, true)]
    [InlineData(11, 1, false)] [InlineData(11, 1, true)]
    public async Task PeriodStatisticsFromSqliteMatchDailyAndCalendarForOneActiveDate(int month, int day, bool missingCosts)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var (other, _) = await References(db, "Other inactive", false);
        var query = new DailyReviewQuery(new(2026, month, day));
        var first = TradeFact(account, instrument, query.FromUtc, 100, commission: 5);
        var last = TradeFact(account, instrument, query.BeforeUtc.AddTicks(-1), -40, fees: missingCosts ? null : 0);
        var zero = TradeFact(other, instrument, query.FromUtc.AddHours(1), 0);
        var euro = TradeFact(other, instrument, query.FromUtc.AddHours(2), 10, currency: "EUR");
        var partial = TradeFact(account, instrument, query.FromUtc.AddHours(3), exitQuantity: 1, commission: null);
        await Add(db, first, last, zero, euro, partial);
        var journal = (await db.ServiceProvider.GetRequiredService<IDailyJournalRepository>()
            .CreateAsync(new(query.Date, null, "Synthetic observations, not performance", true))).Journal!.Entry;

        foreach (Guid? scope in new Guid?[] { null, account, other })
        {
            var observer = new ReadObserver(); var reader = await ObservedPeriod(db, observer);
            var evidence = Ready(await reader.ReadAsync(new(ReviewPeriodKind.Month, query.Date, scope)));
            int reads = observer.Sql.Count;
            var period = PeriodReviewStatisticsCalculator.Calculate(evidence);
            Assert.Equal(reads, observer.Sql.Count); Assert.Equal(0, observer.NonReads);
            Assert.Same(evidence, period.Evidence); Assert.Equal(query.Date, Assert.Single(period.ActiveDays).Date);
            var daily = DailyReviewStatisticsCalculator.Calculate(await Reader(db).GetAsync(new(query.Date, scope)));
            var calendar = await db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>().GetAsync(new(query.Date, scope));
            Assert.Equal(calendar.ClosedTradeCount, period.Population.Closed.Count);
            Assert.Equal(daily.Population.Closed.TradeIds, period.Population.Closed.TradeIds);
            foreach (var currency in period.Currencies)
            {
                var dayMetrics = daily.Currencies.Single(c => c.Currency == currency.Currency);
                var calendarMetrics = calendar.Currencies.Single(c => c.Currency == currency.Currency).Metrics;
                Assert.Equal(dayMetrics.Gross.Metrics, currency.Statistics.Gross.Outcomes.Metrics);
                Assert.Equal(dayMetrics.Net.Metrics, currency.Statistics.Net.Outcomes.Metrics);
                Assert.Equal(calendarMetrics.Gross, currency.Statistics.Gross.Outcomes.Metrics);
                Assert.Equal(calendarMetrics.Net, currency.Statistics.Net.Outcomes.Metrics);
                Assert.Equal(dayMetrics.Gross.Known.TradeIds, currency.Statistics.Gross.Outcomes.Known.TradeIds);
                Assert.Equal(dayMetrics.Net.Unavailable.TradeIds, currency.Statistics.Net.Outcomes.Unavailable.TradeIds);
                Assert.Equal(currency.Statistics.Gross.Outcomes.Metrics.Total, currency.Statistics.Gross.AveragePerActiveDay.Value);
            }
            if (scope is null)
            {
                Assert.Equal(journal.Id, Assert.Single(period.Evidence.Journals).JournalId);
                Assert.Single(period.Evidence.JournalRevisions);
                Assert.Equal(partial.Id, Assert.Single(period.Population.ExcludedOpenActivity.TradeIds));
            }
        }
    }
}
