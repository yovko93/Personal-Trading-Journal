namespace PersonalTradingJournal.Application.Common.Time;

public readonly record struct LocalTimeConversionResult(
    LocalTimeConversionStatus Status,
    DateTimeOffset? UtcTimestamp)
{
    public bool IsSuccess =>
        Status == LocalTimeConversionStatus.Success && UtcTimestamp.HasValue;
}
