using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Complete proposed reference data, not a persisted Instrument or a final InstrumentId.
/// </summary>
public sealed class TradovateInstrumentCreationProposal
{
    public TradovateInstrumentCreationProposal(
        string canonicalSymbol,
        string displayName,
        AssetClass assetClass,
        string? exchange,
        string currency,
        decimal tickSize,
        decimal tickValue,
        IEnumerable<string> sourceBrokerSymbols,
        string metadataSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(sourceBrokerSymbols);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataSource);

        CanonicalSymbol = canonicalSymbol;
        DisplayName = displayName;
        AssetClass = assetClass;
        Exchange = exchange;
        Currency = currency;
        TickSize = tickSize;
        TickValue = tickValue;
        SourceBrokerSymbols = Array.AsReadOnly(sourceBrokerSymbols
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
        MetadataSource = metadataSource;
    }

    public string CanonicalSymbol { get; }

    public string DisplayName { get; }

    public AssetClass AssetClass { get; }

    public string? Exchange { get; }

    public string Currency { get; }

    public decimal TickSize { get; }

    public decimal TickValue { get; }

    public IReadOnlyList<string> SourceBrokerSymbols { get; }

    public string MetadataSource { get; }
}
