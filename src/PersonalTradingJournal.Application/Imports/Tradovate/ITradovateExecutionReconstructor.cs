namespace PersonalTradingJournal.Application.Imports.Tradovate;

public interface ITradovateExecutionReconstructor
{
    TradovateExecutionReconstructionResult Reconstruct(
        TradovateCsvParseResult parseResult,
        CancellationToken cancellationToken = default);
}
