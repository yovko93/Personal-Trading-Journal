using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed class TopstepReconciledTradeEconomics
{
    internal TopstepReconciledTradeEconomics(
        TopstepTradeCandidate candidate,
        TradePricingSnapshot? pricing,
        TopstepCostInterpretation costInterpretation,
        decimal? calculatedGrossPnL,
        decimal? netPnL,
        IEnumerable<TopstepEconomicsDiagnostic> diagnostics)
    {
        Candidate = candidate;
        Pricing = pricing;
        CostInterpretation = costInterpretation;
        CalculatedGrossPnL = calculatedGrossPnL;
        NetPnL = netPnL;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public TopstepTradeCandidate Candidate { get; }
    public TradePricingSnapshot? Pricing { get; }
    public TopstepCostInterpretation CostInterpretation { get; }
    public decimal SourceReportedPnL => Candidate.SourceRow.SourceReportedPnL;
    public decimal SourceReportedFees => Candidate.SourceRow.SourceReportedFees;
    public decimal SourceReportedCommissions => Candidate.SourceRow.SourceReportedCommissions;

    /// <summary>Calculated comparison value; may be present even when reported PnL disagrees.</summary>
    public decimal? CalculatedGrossPnL { get; }

    /// <summary>Never numeric unless pricing, Gross, currency and both cost components reconcile.</summary>
    public decimal? NetPnL { get; }
    public IReadOnlyList<TopstepEconomicsDiagnostic> Diagnostics { get; }
    public bool IsReconciled => NetPnL.HasValue && Diagnostics.Count == 0;
}
