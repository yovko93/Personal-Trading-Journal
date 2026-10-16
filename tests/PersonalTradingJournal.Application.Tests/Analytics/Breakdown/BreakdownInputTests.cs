using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Domain.Trades;
using static PersonalTradingJournal.Application.Tests.Analytics.Breakdown.BreakdownMetricCalculatorTests;

namespace PersonalTradingJournal.Application.Tests.Analytics.Breakdown;

public sealed class BreakdownInputTests
{
    [Theory]
    [InlineData("trade-id")]
    [InlineData("account-id")]
    [InlineData("instrument-id")]
    [InlineData("setup-id")]
    [InlineData("direction")]
    [InlineData("status")]
    [InlineData("currency")]
    [InlineData("missing-closure")]
    [InlineData("negative-duration")]
    [InlineData("opening-offset")]
    [InlineData("closing-offset")]
    [InlineData("open-final-pnl")]
    [InlineData("negative-costs")]
    [InlineData("non-authoritative-net")]
    public void MalformedProjectionFailsWithoutPublishingAPartialOrFakeDurationGroup(string problem)
    {
        var row = Trade(1, 10);
        var invalid = problem switch
        {
            "trade-id" => row with { Facts = row.Facts with { TradeId = Guid.Empty } },
            "account-id" => row with { TradingAccountId = Guid.Empty },
            "instrument-id" => row with { InstrumentId = Guid.Empty },
            "setup-id" => row with { Facts = row.Facts with { TradingSetupId = Guid.Empty } },
            "direction" => row with { Direction = (TradeDirection)0 },
            "status" => row with { Facts = row.Facts with { Status = (TradeStatus)0 } },
            "currency" => row with { Facts = row.Facts with { Currency = " usd " } },
            "missing-closure" => row with { Facts = row.Facts with { ClosedAtUtc = null } },
            "negative-duration" => row with { Facts = row.Facts with { ClosedAtUtc = row.Facts.OpenedAtUtc.AddTicks(-1) } },
            "opening-offset" => row with { Facts = row.Facts with { OpenedAtUtc = row.Facts.OpenedAtUtc.ToOffset(TimeSpan.FromHours(-4)) } },
            "closing-offset" => row with { Facts = row.Facts with { ClosedAtUtc = row.Facts.ClosedAtUtc!.Value.ToOffset(TimeSpan.FromHours(-4)) } },
            "open-final-pnl" => row with { Facts = row.Facts with { Status = TradeStatus.Open, ClosedAtUtc = null } },
            "negative-costs" => row with { Facts = row.Facts with { TotalCosts = -1m } },
            "non-authoritative-net" => row with { Facts = row.Facts with { NetPnL = 3m, TotalCosts = null } },
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        };
        Assert.Throws<ArgumentException>(() => BreakdownMetricCalculator.Calculate(
            new("USD", grouping: BreakdownGrouping.HoldingDuration), [Trade(2, 20), invalid]));
    }

    [Fact]
    public void NullArgumentsAndFactsHaveExplicitFailures()
    {
        Assert.Throws<ArgumentNullException>(() => BreakdownMetricCalculator.Calculate(null!, []));
        Assert.Throws<ArgumentNullException>(() => BreakdownMetricCalculator.Calculate(new("USD"), null!));
        Assert.Throws<ArgumentNullException>(() => BreakdownMetricCalculator.Calculate(new("USD"), [null!]));
        Assert.Throws<ArgumentNullException>(() => BreakdownMetricCalculator.Calculate(new("USD"), [Trade(1, 10) with { Facts = null! }]));
    }

    [Fact]
    public void UnavailableNetDoesNotInventItsCauseAndKnownCostsAreNotSubtractedAgain()
    {
        var rows = new[] { Trade(1, 10, costs: 3), Trade(2, 0, costs: 0) };
        var result = BreakdownMetricCalculator.Calculate(new("USD"), rows).Summary;
        Assert.Equal(10m, result.Selected.Total); // Gross fallback is the supplied Gross, not Gross - known components.
        Assert.Equal(2, result.FinancialSources.GrossFallbackCount);
        Assert.Equal(0, result.Data.MissingCostTradeCount);
        Assert.Equal(MetricCoverageStatus.Unavailable, result.Data.ActualNet.Status);
        Assert.Equal(3m, rows[0].Facts.TotalCosts);
        Assert.Null(rows[0].Facts.NetPnL);
    }

    [Fact]
    public void UnavailableGroupDoesNotTurnOtherGroupsKnownSubtotalIntoACompleteSummary()
    {
        var result = BreakdownMetricCalculator.Calculate(new("USD", grouping: BreakdownGrouping.Account),
            [Trade(1, 10, 8), Trade(2, null, account: AccountB), Trade(3, -4, account: AccountB)]);
        Assert.Equal(MetricCoverageStatus.Partial, result.Summary.Selected.Coverage.Status);
        Assert.Null(result.Summary.Selected.Total);
        Assert.Equal(4m, result.Summary.Selected.KnownSubtotal);
        Assert.Equal(result.Summary.Selected.KnownSubtotal, result.Groups.Sum(g => g.Metrics.Selected.KnownSubtotal));
        Assert.Equal(new MetricCoverage(3, 1), result.Summary.Data.ActualNet);
        Assert.Equal(new MetricCoverage(3, 2), result.Summary.Data.Gross);
        Assert.Equal(1, result.Summary.FinancialSources.NetUsedCount);
        Assert.Equal(1, result.Summary.FinancialSources.GrossFallbackCount);
        Assert.Equal(1, result.Summary.FinancialSources.UnavailableCount);
        Assert.Equal(BreakdownFinancialSource.NetGross, result.Summary.FinancialSources.Source);
        Assert.Equal(8m, result.Groups.Single(g => g.Key.ReferenceId == AccountA).Metrics.Selected.Total);
        Assert.Null(result.Groups.Single(g => g.Key.ReferenceId == AccountB).Metrics.Selected.Total);
    }

    [Fact]
    public void IneligibleOpenActivityAndOtherCurrencyCannotFabricateAZeroResult()
    {
        var open = Trade(1, null) with { Facts = Trade(1, null).Facts with { Status = TradeStatus.Open, ClosedAtUtc = null } };
        var otherCurrency = Trade(2, 10) with { Facts = Trade(2, 10).Facts with { Currency = "EUR" } };
        var result = BreakdownMetricCalculator.Calculate(new("USD"), [open, otherCurrency]);
        Assert.Empty(result.Groups);
        Assert.Null(result.Summary.Selected.Total);
        Assert.Equal(MetricCoverageStatus.Empty, result.Summary.Selected.Coverage.Status);
        Assert.Equal(0, result.Summary.EligibleTradeCount);
    }

    [Fact]
    public void AllAccountsAggregateOnlyTheSelectedHistoricalCurrencyAndRetainExactMembership()
    {
        var rows = new[] { Trade(1, 10, 8), Trade(2, 20, account: AccountB),
            Trade(3, 100, 90) with { Facts = Trade(3, 100, 90).Facts with { Currency = "EUR" } },
            Trade(4, -20, account: AccountB) with { Facts = Trade(4, -20, account: AccountB).Facts with { Currency = "EUR" } } };
        var usd = BreakdownMetricCalculator.Calculate(new("USD", grouping: BreakdownGrouping.Account), rows);
        var eur = BreakdownMetricCalculator.Calculate(new("EUR", grouping: BreakdownGrouping.Account), rows);
        Assert.Equal(28m, usd.Summary.Selected.Total);
        Assert.Equal(70m, eur.Summary.Selected.Total);
        Assert.Equal(new[] { Id(1), Id(2) }, usd.Summary.TradeIds);
        Assert.Equal(new[] { Id(3), Id(4) }, eur.Summary.TradeIds);
        Assert.Equal(2, usd.Groups.Count);
        Assert.Equal(2, eur.Groups.Count);
        Assert.Equal(new[] { AccountA, AccountB }, usd.Groups.Select(g => g.Key.ReferenceId!.Value));
        Assert.Equal(8m, usd.Groups.Single(g => g.Key.ReferenceId == AccountA).Metrics.Selected.Total);
        Assert.Equal(20m, usd.Groups.Single(g => g.Key.ReferenceId == AccountB).Metrics.Selected.Total);
    }
}
