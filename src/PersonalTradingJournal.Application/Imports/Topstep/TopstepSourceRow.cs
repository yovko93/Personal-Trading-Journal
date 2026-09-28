namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>
/// One source row, not a Trade or a broker execution. Reported financial fields
/// are independent evidence; their relationship to gross/net economics is not inferred.
/// </summary>
public sealed record TopstepSourceRow(
    int SourceRecordIndex,
    int SourceLineNumber,
    string Id,
    string ContractName,
    DateTimeOffset SourceEnteredAt,
    DateTimeOffset SourceExitedAt,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal SourceReportedFees,
    decimal SourceReportedPnL,
    decimal Size,
    TopstepTradeType Type,
    DateTimeOffset SourceTradeDay,
    TimeSpan SourceReportedDuration,
    string SourceDurationText,
    decimal SourceReportedCommissions)
{
    public DateTimeOffset EnteredAtUtc => SourceEnteredAt.ToUniversalTime();

    public DateTimeOffset ExitedAtUtc => SourceExitedAt.ToUniversalTime();

    /// <summary>The broker's calendar label, without UTC or named-timezone conversion.</summary>
    public DateOnly BrokerTradingDate => DateOnly.FromDateTime(SourceTradeDay.DateTime);
}
