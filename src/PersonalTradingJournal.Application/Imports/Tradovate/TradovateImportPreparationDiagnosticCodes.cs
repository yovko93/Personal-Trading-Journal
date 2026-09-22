namespace PersonalTradingJournal.Application.Imports.Tradovate;

public static class TradovateImportPreparationDiagnosticCodes
{
    public const string ReconstructionNotEligible = "RECONSTRUCTION_NOT_ELIGIBLE";
    public const string InstrumentResolutionNotReady = "INSTRUMENT_RESOLUTION_NOT_READY";
    public const string TradingAccountNotFound = "TRADING_ACCOUNT_NOT_FOUND";
    public const string SelectedAccountInactive = "SELECTED_ACCOUNT_INACTIVE";
    public const string InvalidSourceLocalTime = "INVALID_SOURCE_LOCAL_TIME";
    public const string AmbiguousSourceLocalTime = "AMBIGUOUS_SOURCE_LOCAL_TIME";
    public const string UtcChronologyInvalid = "UTC_CHRONOLOGY_INVALID";
    public const string InstrumentMappingMissing = "INSTRUMENT_MAPPING_MISSING";
    public const string AccountInstrumentCurrencyDifference =
        "ACCOUNT_INSTRUMENT_CURRENCY_DIFFERENCE";
}
