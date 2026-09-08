using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Screenshots;

/// <summary>
/// Represents storage-agnostic metadata for a screenshot attached to a trade.
/// </summary>
public sealed class TradeScreenshot : AuditableEntity
{
    private const int MaximumStorageKeyLength = 512;
    private const int MaximumFileNameLength = 255;
    private const int MaximumTimeframeLength = 32;
    private const int MaximumDescriptionLength = 2000;

    public TradeScreenshot(
        Guid tradeId,
        TradeScreenshotType type,
        string storageKey,
        string fileName,
        DateTimeOffset? capturedAtUtc,
        string? timeframe,
        string? description,
        DateTimeOffset createdAtUtc)
        : base(createdAtUtc)
    {
        TradeId = ValidateTradeId(tradeId);
        Type = ValidateType(type);
        StorageKey = NormalizeRequired(
            storageKey,
            MaximumStorageKeyLength,
            nameof(storageKey));
        FileName = NormalizeRequired(
            fileName,
            MaximumFileNameLength,
            nameof(fileName));
        CapturedAtUtc = ValidateCapturedAtUtc(capturedAtUtc);
        Timeframe = NormalizeOptional(
            timeframe,
            MaximumTimeframeLength,
            nameof(timeframe));
        Description = NormalizeOptional(
            description,
            MaximumDescriptionLength,
            nameof(description));
    }

    private TradeScreenshot(
        Guid id,
        Guid tradeId,
        TradeScreenshotType type,
        string storageKey,
        string fileName,
        DateTimeOffset? capturedAtUtc,
        string? timeframe,
        string? description,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        TradeId = ValidateTradeId(tradeId);
        Type = ValidateType(type);
        StorageKey = NormalizeRequired(
            storageKey,
            MaximumStorageKeyLength,
            nameof(storageKey));
        FileName = NormalizeRequired(
            fileName,
            MaximumFileNameLength,
            nameof(fileName));
        CapturedAtUtc = ValidateCapturedAtUtc(capturedAtUtc);
        Timeframe = NormalizeOptional(
            timeframe,
            MaximumTimeframeLength,
            nameof(timeframe));
        Description = NormalizeOptional(
            description,
            MaximumDescriptionLength,
            nameof(description));
    }

    public Guid TradeId { get; }

    public TradeScreenshotType Type { get; }

    public string StorageKey { get; }

    public string FileName { get; }

    public DateTimeOffset? CapturedAtUtc { get; }

    public string? Timeframe { get; }

    public string? Description { get; }

    public static TradeScreenshot Rehydrate(
        Guid id,
        Guid tradeId,
        TradeScreenshotType type,
        string storageKey,
        string fileName,
        DateTimeOffset? capturedAtUtc,
        string? timeframe,
        string? description,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradeScreenshot(
            id,
            tradeId,
            type,
            storageKey,
            fileName,
            capturedAtUtc,
            timeframe,
            description,
            createdAtUtc,
            updatedAtUtc);
    }

    private static Guid ValidateTradeId(Guid tradeId)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException("A trade identifier cannot be empty.", nameof(tradeId));
        }

        return tradeId;
    }

    private static TradeScreenshotType ValidateType(TradeScreenshotType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "The trade screenshot type is not defined.");
        }

        return type;
    }

    private static DateTimeOffset? ValidateCapturedAtUtc(DateTimeOffset? capturedAtUtc)
    {
        if (capturedAtUtc.HasValue && capturedAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The captured timestamp must use a UTC offset of zero.",
                nameof(capturedAtUtc));
        }

        return capturedAtUtc;
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}
