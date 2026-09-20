namespace PersonalTradingJournal.Application.Imports.Tradovate;

public static class TradovateReconstructionDiagnosticCodes
{
    public const string ParserInputIncomplete = "PARSER_INPUT_INCOMPLETE";
    public const string ConflictingFillSymbol = "CONFLICTING_FILL_SYMBOL";
    public const string ConflictingFillPrice = "CONFLICTING_FILL_PRICE";
    public const string ConflictingFillTimestamp = "CONFLICTING_FILL_TIMESTAMP";
    public const string ConflictingFillTickSize = "CONFLICTING_FILL_TICK_SIZE";
    public const string DuplicateMatchedRow = "DUPLICATE_MATCHED_ROW";
    public const string QuantityOverflow = "QUANTITY_OVERFLOW";
    public const string QuantityReconciliationFailed = "QUANTITY_RECONCILIATION_FAILED";
    public const string TimestampOrderAmbiguous = "TIMESTAMP_ORDER_AMBIGUOUS";
    public const string PositionReversal = "POSITION_REVERSAL";
    public const string IncompleteLifecycle = "INCOMPLETE_LIFECYCLE";
    public const string SourceCompletenessUnverified = "SOURCE_COMPLETENESS_UNVERIFIED";
}
