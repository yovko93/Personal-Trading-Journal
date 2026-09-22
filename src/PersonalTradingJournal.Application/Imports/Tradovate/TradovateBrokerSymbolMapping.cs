namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateBrokerSymbolMapping
{
    public TradovateBrokerSymbolMapping(
        string brokerSymbol,
        string? canonicalSymbol,
        TradovateInstrumentResolutionStatus status,
        Guid? existingInstrumentId,
        bool? isExistingInstrumentActive,
        IEnumerable<string> diagnosticCodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerSymbol);
        ArgumentNullException.ThrowIfNull(diagnosticCodes);

        BrokerSymbol = brokerSymbol;
        CanonicalSymbol = canonicalSymbol;
        Status = status;
        ExistingInstrumentId = existingInstrumentId;
        IsExistingInstrumentActive = isExistingInstrumentActive;
        DiagnosticCodes = Array.AsReadOnly(diagnosticCodes
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }

    public string BrokerSymbol { get; }

    public string? CanonicalSymbol { get; }

    public TradovateInstrumentResolutionStatus Status { get; }

    public Guid? ExistingInstrumentId { get; }

    public bool? IsExistingInstrumentActive { get; }

    public IReadOnlyList<string> DiagnosticCodes { get; }
}
