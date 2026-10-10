using System.Text.Json;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.DailyReview;

public sealed class ReviewEvidencePresentationTests
{
    [Fact]
    public void ReadableCitationsRetainSavedIdentityAndExactScopeWithoutChangingTheSnapshot()
    {
        var evidence = ReviewFixture.Evidence(new(ReviewFixture.Day));
        var saved = ReviewFixture.Saved(evidence, citeAll: true);
        var snapshot = ReviewSnapshot.Read(saved);
        var packet = PersonalTradingJournal.Application.DailyReview.Coaching.CoachingEvidencePacketBuilder.Build(evidence).Packet!;
        var citations = snapshot.Statements.SelectMany(s => s.Citations).ToArray();
        Assert.Equal(packet.Content.Sources.Count, citations.Length);
        Assert.Equal(packet.Json, saved.EvidenceJson);
        Assert.All(citations, c =>
        {
            Assert.StartsWith("Saved", c.Label);
            Assert.DoesNotContain("calculated:", c.Label);
            Assert.DoesNotContain("AccountStatistics", c.Label);
            Assert.DoesNotContain("null", c.Label);
            foreach (var trade in evidence.Trades) Assert.DoesNotContain(trade.TradeId.ToString(), c.Label);
        });
        foreach (var trade in evidence.Trades)
        {
            var targets = citations.Where(c => c.Trade?.Id == trade.TradeId).ToArray();
            Assert.Equal(1 + trade.Executions.Count, targets.Length);
            Assert.All(targets, c => Assert.Equal(trade.Account.Id, c.Trade!.Source.Account.Id));
            Assert.Contains(targets, c => c.Label.Contains("Execution"));
        }
        Assert.Equal(evidence.Journals.Select(j => (j.JournalId, j.TradingAccountId, j.Revision)).OrderBy(j => j.JournalId),
            citations.Where(c => c.HasJournal).Select(c => (c.Journal!.Source.JournalId, c.Journal.Source.TradingAccountId, c.Journal.Source.Revision)).OrderBy(j => j.JournalId));
        Assert.Contains(citations, c => c.Label.Contains("USD summary"));
        Assert.Contains(citations, c => c.Label.Contains("EUR summary"));
    }

    [Fact]
    public void OneSummaryPerAccountCurrencyRetainsCompleteStatisticsAndEverySourceInternally()
    {
        var evidence = ReviewFixture.Evidence(new(ReviewFixture.Day));
        var statistics = DailyReviewStatisticsCalculator.Calculate(evidence);
        var display = new ReviewEvidencePresentation(evidence, statistics);
        Assert.Equal(2, display.Metrics.Count);
        Assert.All(display.Metrics, row =>
        {
            var currency = statistics.Currencies.Single(c => c.Currency == row.Currency);
            var account = currency.Accounts.Single(a => a.Account.Id == row.AccountId);
            Assert.Same(account.Gross, row.Gross);
            Assert.Same(account.Net, row.Net);
            Assert.Contains(row.Currency, row.Label);
            Assert.DoesNotContain(row.AccountId.ToString(), row.Label);
            Assert.Contains(row.AccountId.ToString(), row.Sources);
        });
        Assert.Contains("Multiple currencies", display.CoverageSummary);
        var exact = JsonDocument.Parse(display.SourceDetails).RootElement;
        Assert.Equal(evidence.Trades.Count, exact.GetProperty("evidence").GetProperty("trades").GetArrayLength());
        Assert.Equal(2, exact.GetProperty("statistics").GetProperty("currencies").GetArrayLength());
        Assert.All(evidence.Trades, t => Assert.Contains(t.TradeId.ToString(), display.SourceDetails));
        Assert.All(evidence.Journals, j => Assert.Contains(j.JournalId.ToString(), display.SourceDetails));
    }

    [Fact]
    public void CompleteSingleCurrencyHasNoZeroOnlyOrCrossCurrencyWarningsAndTradesUseKnownSets()
    {
        var original = ReviewFixture.Evidence(new(ReviewFixture.Day));
        var trade = original.Trades.Single(t => t.PricingCurrency == "EUR");
        var evidence = original with { Trades = [trade] };
        var display = new ReviewEvidencePresentation(evidence, DailyReviewStatisticsCalculator.Calculate(evidence));
        Assert.Empty(display.CoverageSummary);
        var metric = Assert.Single(display.Metrics);
        Assert.Empty(metric.Coverage);
        Assert.Contains("1 losses", metric.Outcomes);
        var row = Assert.Single(display.Trades);
        Assert.Contains("Net -12.00 EUR", row.Values);
        Assert.Contains("New York", row.Context);
        Assert.DoesNotContain("ClosedOnDate", row.Context);
        Assert.DoesNotContain(trade.TradeId.ToString(), row.Heading + row.Context + row.Values);
        Assert.Contains(trade.Executions[0].Id.ToString(), row.Details);
    }

    [Theory]
    [InlineData(DailyReviewTradeQuality.UnknownCommission, "commissions missing")]
    [InlineData(DailyReviewTradeQuality.UnknownFees, "fees missing")]
    [InlineData(DailyReviewTradeQuality.UnknownGrossPnL, "other Net data incomplete")]
    public void UnavailableNetIsNotAPartialTotalAndWarningsDescribeTheActualMissingData(DailyReviewTradeQuality quality, string warning)
    {
        var original = ReviewFixture.Evidence(new(ReviewFixture.Day));
        var known = original.Trades.Single(t => t.PricingCurrency == "EUR");
        var unknown = known with { TradeId = Guid.NewGuid(), Quality = quality };
        var evidence = original with { Trades = [known, unknown] };
        var display = new ReviewEvidencePresentation(evidence, DailyReviewStatisticsCalculator.Calculate(evidence));
        var metric = Assert.Single(display.Metrics);
        Assert.Contains("Net: Unavailable (EUR)", metric.Values);
        Assert.Contains(warning, metric.Coverage);
        Assert.DoesNotContain("missing for 0", metric.Coverage);
        Assert.Contains("No partial total", metric.Coverage);
        Assert.Contains("Net Unavailable (EUR)", display.Trades.Single(t => t.Id == unknown.TradeId).Values);
        Assert.Contains("Net -12.00 EUR", display.Trades.Single(t => t.Id == known.TradeId).Values);
    }
}
