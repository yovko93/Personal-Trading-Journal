namespace PersonalTradingJournal.Application.Imports.Topstep;

public interface ITopstepTradeCandidateReconstructor
{
    /// <summary>
    /// Prepares reported closed-row candidates, not broker executions or verified
    /// account-level positions. No persistence or import eligibility is established.
    /// </summary>
    TopstepTradeReconstructionResult Reconstruct(
        TopstepCsvParseResult source,
        CancellationToken cancellationToken = default);
}
