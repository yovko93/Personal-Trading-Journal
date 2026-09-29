using PersonalTradingJournal.Domain.Trades;
using System.Numerics;

namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>Pure, read-only economics checks. Does not resolve Instruments or fetch fee schedules.</summary>
public sealed class TopstepEconomicsReconciler
{
    /// <param name="verifiedPricingByContract">
    /// Caller-verified historical point value and currency for each exact source contract.
    /// A snapshot's existence does not itself prove a correct Instrument mapping.
    /// </param>
    public TopstepEconomicsReconciliationResult Reconcile(
        TopstepTradeReconstructionResult source,
        IReadOnlyDictionary<string, TradePricingSnapshot> verifiedPricingByContract,
        TopstepCostInterpretation costInterpretation = TopstepCostInterpretation.Unverified,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(verifiedPricingByContract);
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<TopstepEconomicsDiagnostic>();
        if (!source.CanUseRowCandidates)
        {
            diagnostics.Add(new(TopstepEconomicsDiagnosticCodes.SourceNotValid, null, null, null,
                "Economics requires valid closed-row candidates. Correct the retained parser/reconstruction diagnostics first."));
            foreach (TopstepCsvDiagnostic diagnostic in source.Source.Diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (diagnostic.FieldName is "Fees" or "Commissions" &&
                    diagnostic.Code is TopstepCsvDiagnosticCodes.RequiredValue or TopstepCsvDiagnosticCodes.MissingHeader)
                {
                    diagnostics.Add(new(TopstepEconomicsDiagnosticCodes.MissingReportedCost,
                        diagnostic.SourceRecordIndex, diagnostic.SourceLineNumber, diagnostic.FieldName,
                        "A separately reported cost is missing. Obtain the complete source value; do not substitute zero."));
                }
            }

            return new(source, [], diagnostics);
        }

        // Do not inherit a caller's case-insensitive symbol comparer or collapse contracts.
        var pricing = new Dictionary<string, TradePricingSnapshot>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, TradePricingSnapshot> entry in verifiedPricingByContract)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pricing.Add(entry.Key, entry.Value);
        }

        var rows = new List<TopstepReconciledTradeEconomics>(source.Candidates.Count);
        foreach (TopstepTradeCandidate candidate in source.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pricing.TryGetValue(candidate.ContractName, out TradePricingSnapshot? snapshot);
            TopstepReconciledTradeEconomics row = ReconcileRow(candidate, snapshot, costInterpretation);
            rows.Add(row);
            diagnostics.AddRange(row.Diagnostics);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new(source, rows, diagnostics);
    }

    private static TopstepReconciledTradeEconomics ReconcileRow(
        TopstepTradeCandidate candidate, TradePricingSnapshot? pricing, TopstepCostInterpretation interpretation)
    {
        TopstepSourceRow row = candidate.SourceRow;
        var diagnostics = new List<TopstepEconomicsDiagnostic>();
        void Error(string code, string? field, string message) =>
            diagnostics.Add(new(code, row.SourceRecordIndex, row.SourceLineNumber, field, message));

        if (interpretation != TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd)
        {
            Error(TopstepEconomicsDiagnosticCodes.CostInterpretationUnverified, null,
                "Verify that this USD export reports PnL before costs and separate, additive Fees and Commissions totals for the completed row quantity.");
        }

        if (pricing is null)
        {
            Error(TopstepEconomicsDiagnosticCodes.PricingNotVerified, null,
                "Verify the Instrument point value and currency for this exact source contract before reconciliation.");
        }
        else if (pricing.Currency != "USD")
        {
            Error(TopstepEconomicsDiagnosticCodes.CurrencyNotSupported, null,
                "The verified source interpretation is USD only. Establish the source currency and matching Instrument pricing; do not apply an exchange rate or relabel amounts.");
        }

        if (row.SourceReportedFees < 0m)
        {
            Error(TopstepEconomicsDiagnosticCodes.NegativeReportedCost, "Fees",
                "Negative reported fees require a verified rebate interpretation; the Domain accepts only non-negative costs.");
        }

        if (row.SourceReportedCommissions < 0m)
        {
            Error(TopstepEconomicsDiagnosticCodes.NegativeReportedCost, "Commissions",
                "Negative reported commissions require a verified rebate interpretation; the Domain accepts only non-negative costs.");
        }

        decimal? gross = null;
        decimal? net = null;
        try
        {
            if (pricing is not null)
            {
                // Match Trade.CalculateGrossPnL's operation order. No display/currency
                // rounding, tick alignment, rate substitution, or tolerance is applied.
                decimal entryNotional = CalculateExact(candidate.EntryPrice, candidate.Quantity, '*');
                decimal exitNotional = CalculateExact(candidate.ExitPrice, candidate.Quantity, '*');
                decimal movement = candidate.Direction == TradeDirection.Long
                    ? CalculateExact(exitNotional, entryNotional, '-')
                    : CalculateExact(entryNotional, exitNotional, '-');
                gross = CalculateExact(movement, pricing.PointValue, '*');
                if (gross.Value != row.SourceReportedPnL)
                {
                    Error(TopstepEconomicsDiagnosticCodes.GrossPnLMismatch, "PnL",
                        "Reported PnL does not exactly match calculated Gross. Verify source prices, quantity, direction, Instrument pricing and export semantics; do not substitute the reported value or waive the difference.");
                }
            }

            if (diagnostics.Count == 0 && gross.HasValue)
            {
                decimal costs = CalculateExact(row.SourceReportedFees, row.SourceReportedCommissions, '+');
                net = CalculateExact(gross.Value, costs, '-');
            }
        }
        catch (OverflowException)
        {
            Error(TopstepEconomicsDiagnosticCodes.ArithmeticOverflow, null,
                "Source economics exceed the supported decimal arithmetic range. Net is unavailable; review the source values and pricing.");
        }
        catch (DecimalPrecisionLossException)
        {
            Error(TopstepEconomicsDiagnosticCodes.ArithmeticPrecisionLoss, null,
                "Decimal arithmetic would round away source economics. Net is unavailable; a verified precision policy is required.");
        }

        return new(candidate, pricing, interpretation, gross, net, diagnostics);
    }

    private static decimal CalculateExact(decimal left, decimal right, char operation)
    {
        decimal result = operation switch
        {
            '*' => checked(left * right),
            '+' => checked(left + right),
            '-' => checked(left - right),
            _ => throw new InvalidOperationException("Unsupported arithmetic operation."),
        };

        // Checked decimal operations can still silently lose fractional precision.
        // Integer coefficients audit exactness only; all published values remain decimal
        // and follow the Domain's operation order, never a different BigInteger PnL formula.
        (BigInteger leftCoefficient, int leftScale) = Parts(left);
        (BigInteger rightCoefficient, int rightScale) = Parts(right);
        int expectedScale = operation == '*' ? leftScale + rightScale : Math.Max(leftScale, rightScale);
        BigInteger expected;
        if (operation == '*')
        {
            expected = leftCoefficient * rightCoefficient;
        }
        else
        {
            BigInteger alignedLeft = leftCoefficient * BigInteger.Pow(10, expectedScale - leftScale);
            BigInteger alignedRight = rightCoefficient * BigInteger.Pow(10, expectedScale - rightScale);
            expected = operation == '+' ? alignedLeft + alignedRight : alignedLeft - alignedRight;
        }

        (BigInteger actual, int actualScale) = Parts(result);
        if (actual * BigInteger.Pow(10, expectedScale) != expected * BigInteger.Pow(10, actualScale))
        {
            throw new DecimalPrecisionLossException();
        }

        return result;
    }

    private static (BigInteger Coefficient, int Scale) Parts(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger coefficient = (uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        return ((bits[3] & int.MinValue) != 0 ? -coefficient : coefficient, (bits[3] >> 16) & 0xFF);
    }

    private sealed class DecimalPrecisionLossException : ArithmeticException;
}
