namespace PersonalTradingJournal.Application.Imports.Topstep;

public enum TopstepReferenceDiagnosticSeverity { Warning, Error }

public sealed record TopstepReferenceDiagnostic(
    TopstepReferenceDiagnosticSeverity Severity, string Code, string? SourceContract, string Message);

public static class TopstepReferenceDiagnosticCodes
{
    public const string SourceNotReady = "SOURCE_NOT_READY";
    public const string AccountSelectionRequired = "ACCOUNT_SELECTION_REQUIRED";
    public const string AccountNotFound = "ACCOUNT_NOT_FOUND";
    public const string AccountProviderMismatch = "ACCOUNT_PROVIDER_MISMATCH";
    public const string AccountCurrencyMismatch = "ACCOUNT_CURRENCY_MISMATCH";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string UnrecognizedContract = "UNRECOGNIZED_CONTRACT";
    public const string MultipleInstrumentMatches = "MULTIPLE_INSTRUMENT_MATCHES";
    public const string InstrumentMetadataRequired = "INSTRUMENT_METADATA_REQUIRED";
    public const string InstrumentVerificationRequired = "INSTRUMENT_VERIFICATION_REQUIRED";
    public const string InstrumentSpecificationMismatch = "INSTRUMENT_SPECIFICATION_MISMATCH";
    public const string InstrumentInactive = "INSTRUMENT_INACTIVE";
    public const string InstrumentCreationProposed = "INSTRUMENT_CREATION_PROPOSED";
}
