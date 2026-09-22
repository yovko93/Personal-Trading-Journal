namespace PersonalTradingJournal.Application.Imports.Tradovate;

public static class TradovateInstrumentResolutionDiagnosticCodes
{
    public const string ReconstructionNotEligible = "RECONSTRUCTION_NOT_ELIGIBLE";
    public const string ReconstructionSymbolCoverageMismatch = "RECONSTRUCTION_SYMBOL_COVERAGE_MISMATCH";
    public const string UnrecognizedContractSymbol = "UNRECOGNIZED_CONTRACT_SYMBOL";
    public const string SourceTickSizeConflict = "SOURCE_TICK_SIZE_CONFLICT";
    public const string SourceProfileTickSizeMismatch = "SOURCE_PROFILE_TICK_SIZE_MISMATCH";
    public const string MultipleExistingInstruments = "MULTIPLE_EXISTING_INSTRUMENTS";
    public const string ExistingInstrumentInactive = "EXISTING_INSTRUMENT_INACTIVE";
    public const string InstrumentAssetClassMismatch = "INSTRUMENT_ASSET_CLASS_MISMATCH";
    public const string InstrumentTickSizeMismatch = "INSTRUMENT_TICK_SIZE_MISMATCH";
    public const string InstrumentCurrencyMismatch = "INSTRUMENT_CURRENCY_MISMATCH";
    public const string InstrumentTickValueMismatch = "INSTRUMENT_TICK_VALUE_MISMATCH";
    public const string InstrumentMetadataRequired = "INSTRUMENT_METADATA_REQUIRED";
}
