using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Mistakes;

/// <summary>
/// Represents an observed trading mistake associated with a specific trade.
/// </summary>
public sealed class TradeMistake : AuditableEntity
{
    private const int MaximumNoteLength = 2000;

    public TradeMistake(
        Guid tradeId,
        Guid tradingMistakeId,
        string? note,
        DateTimeOffset createdAtUtc)
        : base(createdAtUtc)
    {
        TradeId = ValidateIdentifier(
            tradeId,
            nameof(tradeId),
            "A trade identifier cannot be empty.");
        TradingMistakeId = ValidateIdentifier(
            tradingMistakeId,
            nameof(tradingMistakeId),
            "A trading mistake identifier cannot be empty.");
        Note = NormalizeNote(note);
    }

    private TradeMistake(
        Guid id,
        Guid tradeId,
        Guid tradingMistakeId,
        string? note,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        TradeId = ValidateIdentifier(
            tradeId,
            nameof(tradeId),
            "A trade identifier cannot be empty.");
        TradingMistakeId = ValidateIdentifier(
            tradingMistakeId,
            nameof(tradingMistakeId),
            "A trading mistake identifier cannot be empty.");
        Note = NormalizeNote(note);
    }

    public Guid TradeId { get; }

    public Guid TradingMistakeId { get; }

    public string? Note { get; }

    public static TradeMistake Rehydrate(
        Guid id,
        Guid tradeId,
        Guid tradingMistakeId,
        string? note,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradeMistake(
            id,
            tradeId,
            tradingMistakeId,
            note,
            createdAtUtc,
            updatedAtUtc);
    }

    private static Guid ValidateIdentifier(
        Guid id,
        string parameterName,
        string message)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(message, parameterName);
        }

        return id;
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        string normalized = note.Trim();
        if (normalized.Length > MaximumNoteLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(note),
                note,
                $"The trade mistake note cannot exceed {MaximumNoteLength} characters.");
        }

        return normalized;
    }
}
