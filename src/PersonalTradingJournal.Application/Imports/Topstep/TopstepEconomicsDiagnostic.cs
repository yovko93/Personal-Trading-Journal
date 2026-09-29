namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>Every economics diagnostic blocks verified Net for its affected row/input.</summary>
public sealed record TopstepEconomicsDiagnostic(
    string Code,
    int? SourceRecordIndex,
    int? SourceLineNumber,
    string? FieldName,
    string Message);

public static class TopstepEconomicsDiagnosticCodes
{
    public const string SourceNotValid = "SOURCE_NOT_VALID";
    public const string MissingReportedCost = "MISSING_REPORTED_COST";
    public const string PricingNotVerified = "PRICING_NOT_VERIFIED";
    public const string CostInterpretationUnverified = "COST_INTERPRETATION_UNVERIFIED";
    public const string CurrencyNotSupported = "CURRENCY_NOT_SUPPORTED";
    public const string NegativeReportedCost = "NEGATIVE_REPORTED_COST";
    public const string GrossPnLMismatch = "GROSS_PNL_MISMATCH";
    public const string ArithmeticOverflow = "ARITHMETIC_OVERFLOW";
    public const string ArithmeticPrecisionLoss = "ARITHMETIC_PRECISION_LOSS";
}
