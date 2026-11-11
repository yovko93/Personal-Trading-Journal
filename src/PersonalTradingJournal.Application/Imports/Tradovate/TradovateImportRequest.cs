namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovateImportRequest(
    TradovateImportPreparationResult Preparation,
    DateTimeOffset ImportedAtUtc,
    TradovateCostSources? CostSources = null,
    IReadOnlyList<TradovateMatchedPairEvidence>? MatchedPairs = null);
