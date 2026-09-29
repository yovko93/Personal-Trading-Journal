using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Imports.Topstep;

public sealed class TopstepEconomicsReconcilerTests
{
    private readonly TopstepEconomicsReconciler _reconciler = new();
    private const TopstepCostInterpretation Verified = TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd;

    [Theory]
    [InlineData(TopstepTradeType.Long, 100, 101, 6, "4.8")]
    [InlineData(TopstepTradeType.Long, 100, 99, -6, "-7.2")]
    [InlineData(TopstepTradeType.Short, 100, 99, 6, "4.8")]
    [InlineData(TopstepTradeType.Short, 100, 101, -6, "-7.2")]
    [InlineData(TopstepTradeType.Long, 100, 100, 0, "-1.2")]
    [InlineData(TopstepTradeType.Short, 100, 100, 0, "-1.2")]
    public void ReconcilesProfitLossAndZeroWithSeparateActualCostTotals(
        TopstepTradeType type, int entry, int exit, int gross, string expectedNet)
    {
        TopstepSourceRow source = Row() with
        {
            Type = type, EntryPrice = entry, ExitPrice = exit, Size = 3m,
            SourceReportedPnL = gross, SourceReportedFees = 0.9m, SourceReportedCommissions = 0.3m,
        };
        TopstepEconomicsReconciliationResult result = Reconcile(source);
        TopstepReconciledTradeEconomics row = Assert.Single(result.Rows);
        Assert.True(result.IsEconomicallyReconciled);
        Assert.True(row.IsReconciled);
        Assert.Equal((decimal)gross, row.CalculatedGrossPnL);
        Assert.Equal(decimal.Parse(expectedNet, System.Globalization.CultureInfo.InvariantCulture), row.NetPnL);
        Assert.Equal(0.9m, row.SourceReportedFees);
        Assert.Equal(0.3m, row.SourceReportedCommissions);
        Assert.Equal((decimal)gross, row.SourceReportedPnL);
        Assert.Empty(result.Diagnostics);
        AssertDomainParity(row);
    }

    [Fact]
    public void KeepsExactFractionalPricesQuantitiesPointValueAndSubcentCosts()
    {
        TopstepSourceRow source = Row() with
        {
            EntryPrice = 1.125m, ExitPrice = 1.625m, Size = 0.4m,
            SourceReportedPnL = 0.025m, SourceReportedFees = 0.003m, SourceReportedCommissions = 0.002m,
        };
        TopstepEconomicsReconciliationResult result = _reconciler.Reconcile(Source(source), Pricing(0.125m), Verified);
        TopstepReconciledTradeEconomics row = Assert.Single(result.Rows);
        Assert.Equal(0.025m, row.CalculatedGrossPnL);
        Assert.Equal(0.02m, row.NetPnL);
        Assert.True(row.IsReconciled);
        AssertDomainParity(row);
    }

    [Fact]
    public void UsesCsvTotalsNotPerContractReplacementOrPublishedRate()
    {
        TopstepEconomicsReconciliationResult result = Reconcile(Row() with
        {
            SourceReportedFees = 7.11m, SourceReportedCommissions = 0.23m,
        });
        TopstepReconciledTradeEconomics row = Assert.Single(result.Rows);
        Assert.Equal(5m, row.CalculatedGrossPnL);
        Assert.Equal(-2.34m, row.NetPnL);
        Assert.True(row.IsReconciled);
        AssertDomainParity(row);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2.56")]
    [InlineData("5.000000001")]
    [InlineData("-5")]
    public void AnyReportedGrossMismatchBlocksNetWithoutRewritingEitherValue(string reported)
    {
        decimal pnl = decimal.Parse(reported, System.Globalization.CultureInfo.InvariantCulture);
        TopstepSourceRow source = Row() with { SourceReportedPnL = pnl };
        TopstepEconomicsReconciliationResult result = Reconcile(source);
        TopstepReconciledTradeEconomics row = Assert.Single(result.Rows);
        Assert.Equal(5m, row.CalculatedGrossPnL);
        Assert.Equal(pnl, row.SourceReportedPnL);
        Assert.Same(source, row.Candidate.SourceRow);
        AssertBlocked(result, TopstepEconomicsDiagnosticCodes.GrossPnLMismatch);
    }

    [Fact]
    public void KnownZeroGrossAndNetAreDistinctFromUnverifiedNet()
    {
        TopstepSourceRow source = Row() with
        {
            ExitPrice = Row().EntryPrice, SourceReportedPnL = 0m,
            SourceReportedFees = 0m, SourceReportedCommissions = 0m,
        };
        TopstepReconciledTradeEconomics known = Assert.Single(Reconcile(source).Rows);
        TopstepReconciledTradeEconomics unknown = Assert.Single(_reconciler.Reconcile(Source(source), Pricing()).Rows);
        Assert.Equal(0m, known.CalculatedGrossPnL);
        Assert.Equal(0m, known.NetPnL);
        Assert.True(known.IsReconciled);
        Assert.Equal(0m, unknown.CalculatedGrossPnL);
        Assert.Null(unknown.NetPnL);
        Assert.False(unknown.IsReconciled);
    }

    [Fact]
    public void GrossExactlyEqualToCostsProducesVerifiedZeroNet()
    {
        TopstepReconciledTradeEconomics row = Assert.Single(Reconcile(Row() with
        {
            SourceReportedFees = 4m, SourceReportedCommissions = 1m,
        }).Rows);
        Assert.Equal(0m, row.NetPnL);
        Assert.True(row.IsReconciled);
    }

    [Theory]
    [InlineData(TopstepCostInterpretation.Unverified)]
    [InlineData((TopstepCostInterpretation)99)]
    public void RequiresExplicitVerifiedSourceCostInterpretation(TopstepCostInterpretation interpretation)
    {
        AssertBlocked(_reconciler.Reconcile(Source(Row()), Pricing(), interpretation),
            TopstepEconomicsDiagnosticCodes.CostInterpretationUnverified);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("CAD")]
    public void DoesNotAssumeSourceCurrencyOrConvertCosts(string currency)
    {
        var pricing = new Dictionary<string, TradePricingSnapshot> { ["MNQZ6"] = new(2m, currency) };
        AssertBlocked(_reconciler.Reconcile(Source(Row()), pricing, Verified), TopstepEconomicsDiagnosticCodes.CurrencyNotSupported);
    }

    [Fact]
    public void MissingOrWrongContractPricingCannotPublishNet()
    {
        AssertBlocked(_reconciler.Reconcile(Source(Row()), new Dictionary<string, TradePricingSnapshot>(), Verified),
            TopstepEconomicsDiagnosticCodes.PricingNotVerified);
        var wrongCase = new Dictionary<string, TradePricingSnapshot>(StringComparer.OrdinalIgnoreCase) { ["mnqz6"] = new(2m, "USD") };
        AssertBlocked(_reconciler.Reconcile(Source(Row()), wrongCase, Verified), TopstepEconomicsDiagnosticCodes.PricingNotVerified);
        var otherContract = new Dictionary<string, TradePricingSnapshot> { ["MNQH7"] = new(2m, "USD") };
        AssertBlocked(_reconciler.Reconcile(Source(Row()), otherContract, Verified), TopstepEconomicsDiagnosticCodes.PricingNotVerified);
    }

    [Fact]
    public void WrongPointValueDoesNotSilentlyReplaceInstrumentEconomics()
    {
        TopstepEconomicsReconciliationResult result = _reconciler.Reconcile(Source(Row()), Pricing(1m), Verified);
        Assert.Equal(2.5m, Assert.Single(result.Rows).CalculatedGrossPnL);
        AssertBlocked(result, TopstepEconomicsDiagnosticCodes.GrossPnLMismatch);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NegativeCostsNeedAnExplicitRebateModelAndRemainUnchanged(bool fees)
    {
        TopstepSourceRow source = fees ? Row() with { SourceReportedFees = -0.5m } : Row() with { SourceReportedCommissions = -0.5m };
        TopstepEconomicsReconciliationResult result = Reconcile(source);
        AssertBlocked(result, TopstepEconomicsDiagnosticCodes.NegativeReportedCost);
        Assert.Equal(fees ? "Fees" : "Commissions", Assert.Single(result.Diagnostics).FieldName);
        Assert.Same(source, Assert.Single(result.Rows).Candidate.SourceRow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ArithmeticOverflowBlocksInsteadOfReturningAPartialNet(bool prices)
    {
        TopstepSourceRow row = prices
            ? Row() with { EntryPrice = decimal.MaxValue }
            : Row() with { SourceReportedFees = decimal.MaxValue, SourceReportedCommissions = decimal.MaxValue };
        AssertBlocked(Reconcile(row), TopstepEconomicsDiagnosticCodes.ArithmeticOverflow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DecimalRoundingOrUnderflowCannotVerifyFalseZeroOrLoseACost(bool price)
    {
        TopstepSourceRow row = price
            ? Row() with { EntryPrice = 0.1m, ExitPrice = 0.2m, Size = 0.0000000000000000000000000001m, SourceReportedPnL = 0m }
            : Row() with { SourceReportedFees = 100m, SourceReportedCommissions = 0.0000000000000000000000000001m };
        AssertBlocked(Reconcile(row), TopstepEconomicsDiagnosticCodes.ArithmeticPrecisionLoss);
    }

    [Fact]
    public void ResultsRetainCandidatesWarningsPricingAndRowLocationsWithoutGrouping()
    {
        TopstepSourceRow first = Row();
        TopstepSourceRow second = Row() with { Id = "SYNTH-2", SourceRecordIndex = 2, SourceLineNumber = 5, SourceReportedPnL = 9m };
        TopstepTradeReconstructionResult source = Source(first, second);
        Dictionary<string, TradePricingSnapshot> pricing = Pricing();
        TradePricingSnapshot snapshot = pricing["MNQZ6"];
        TopstepEconomicsReconciliationResult result = _reconciler.Reconcile(source, pricing, Verified);
        pricing.Clear();

        Assert.False(result.IsEconomicallyReconciled);
        Assert.Equal(2, result.Rows.Count);
        Assert.Same(source, result.Source);
        Assert.Same(source.Candidates[0], result.Rows[0].Candidate);
        Assert.Same(source.Candidates[1], result.Rows[1].Candidate);
        Assert.Same(snapshot, result.Rows[0].Pricing);
        Assert.True(result.Rows[0].IsReconciled);
        Assert.Null(result.Rows[1].NetPnL);
        Assert.Single(result.Source.Diagnostics);
        Assert.False(result.Source.IsPositionGroupingVerified);
        TopstepEconomicsDiagnostic error = Assert.Single(result.Diagnostics);
        Assert.Equal(2, error.SourceRecordIndex);
        Assert.Equal(5, error.SourceLineNumber);
        Assert.DoesNotContain(second.Id, error.Message);
    }

    [Fact]
    public void CancellationPropagatesBeforeAnyReconciliation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => _reconciler.Reconcile(Source(Row()), Pricing(), Verified, cancellation.Token));
    }

    private TopstepEconomicsReconciliationResult Reconcile(TopstepSourceRow row) =>
        _reconciler.Reconcile(Source(row), Pricing(), Verified);

    private static void AssertBlocked(TopstepEconomicsReconciliationResult result, string code)
    {
        Assert.False(result.IsEconomicallyReconciled);
        Assert.All(result.Rows, r => Assert.Null(r.NetPnL));
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    private static Dictionary<string, TradePricingSnapshot> Pricing(decimal pointValue = 2m) =>
        new(StringComparer.Ordinal) { ["MNQZ6"] = new(pointValue, "USD") };

    private static TopstepTradeReconstructionResult Source(params TopstepSourceRow[] rows) => new(
        new(rows, [], rows.Length, 0, true), rows.Select(r => new TopstepTradeCandidate(r)),
        [new(TopstepReconstructionDiagnosticSeverity.Warning, TopstepReconstructionDiagnosticCodes.PositionBoundariesUnverified,
            rows.Select(r => new TopstepSourceReference(r.SourceRecordIndex, r.SourceLineNumber)), "Position boundaries unverified.")]);

    private static TopstepSourceRow Row() => new(
        1, 2, "SYNTH-1", "MNQZ6",
        new DateTimeOffset(2026, 7, 10, 14, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 10, 14, 0, 1, TimeSpan.Zero),
        20000.125m, 20001.375m, 1.44m, 5m, 2m, TopstepTradeType.Long,
        new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.FromHours(-5)), TimeSpan.FromSeconds(1), "00:00:01", 1m);

    private static void AssertDomainParity(TopstepReconciledTradeEconomics row)
    {
        TopstepTradeCandidate candidate = row.Candidate;
        Guid tradeId = Guid.NewGuid();
        ExecutionSide entrySide = candidate.Direction == TradeDirection.Long ? ExecutionSide.Buy : ExecutionSide.Sell;
        ExecutionSide exitSide = entrySide == ExecutionSide.Buy ? ExecutionSide.Sell : ExecutionSide.Buy;
        // Test-only aggregate: assign the row's complete costs once, on closing.
        // These are internal entity IDs, not fabricated broker execution IDs.
        var entry = new TradeExecution(tradeId, 1, candidate.OpenedAtUtc, entrySide, candidate.Quantity,
            candidate.EntryPrice, 0m, 0m, null, null, candidate.ContractName);
        var exit = new TradeExecution(tradeId, 2, candidate.ClosedAtUtc, exitSide, candidate.Quantity,
            candidate.ExitPrice, row.SourceReportedCommissions, row.SourceReportedFees, null, null, candidate.ContractName);
        Trade trade = Trade.Start(Guid.NewGuid(), Guid.NewGuid(), row.Pricing!, entry, candidate.OpenedAtUtc);
        trade.AddExecution(exit, candidate.ClosedAtUtc);
        Assert.Equal(row.CalculatedGrossPnL, trade.GrossPnL);
        Assert.Equal(row.NetPnL, trade.NetPnL);
        Assert.Equal(row.SourceReportedFees + row.SourceReportedCommissions, trade.TotalCosts);
    }
}
