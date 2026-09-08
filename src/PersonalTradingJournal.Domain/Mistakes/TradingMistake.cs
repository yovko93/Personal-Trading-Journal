using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Mistakes;

/// <summary>
/// Represents a reusable user-defined trading mistake classification.
/// </summary>
public sealed class TradingMistake : AuditableEntity
{
    private const int MaximumNameLength = 128;
    private const int MaximumDescriptionLength = 2000;

    public TradingMistake(
        string name,
        string? description,
        DateTimeOffset createdAtUtc)
        : base(createdAtUtc)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsActive = true;
    }

    private TradingMistake(
        Guid id,
        string name,
        string? description,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsActive = isActive;
    }

    public string Name { get; }

    public string? Description { get; }

    public bool IsActive { get; private set; }

    public static TradingMistake Rehydrate(
        Guid id,
        string name,
        string? description,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradingMistake(
            id,
            name,
            description,
            isActive,
            createdAtUtc,
            updatedAtUtc);
    }

    public void Activate(DateTimeOffset updatedAtUtc)
    {
        if (IsActive)
        {
            return;
        }

        SetUpdatedAtUtc(updatedAtUtc);
        IsActive = true;
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        SetUpdatedAtUtc(updatedAtUtc);
        IsActive = false;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A trading mistake name is required.", nameof(name));
        }

        string normalized = name.Trim();
        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                name,
                $"The trading mistake name cannot exceed {MaximumNameLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        string normalized = description.Trim();
        if (normalized.Length > MaximumDescriptionLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description),
                description,
                $"The trading mistake description cannot exceed {MaximumDescriptionLength} characters.");
        }

        return normalized;
    }
}
