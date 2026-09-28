namespace PersonalTradingJournal.Application.Imports.Topstep;

public enum TopstepCostInterpretation
{
    Unverified,

    /// <summary>
    /// The reviewed TopstepX USD closed-row export: Fees excludes Commissions;
    /// both are totals for the row's completed quantity, not per-side/unit rates.
    /// No fee schedule or rate is implied by this interpretation.
    /// </summary>
    SeparateReportedRoundTurnTotalsUsd,
}
