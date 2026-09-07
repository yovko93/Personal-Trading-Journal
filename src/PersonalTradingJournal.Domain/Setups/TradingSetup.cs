using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Setups;

/// <summary>
/// Represents a specific, repeatable market configuration used for classification.
/// </summary>
public sealed class TradingSetup : AuditableEntity
{
    private const int MaximumNameLength = 128;
    private const int MaximumDescriptionLength = 2000;

    public TradingSetup(
        string name,
        string? description,
        DateTimeOffset createdAtUtc)
        : base(createdAtUtc)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsActive = true;
    }

    private TradingSetup(
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

    public static TradingSetup Rehydrate(
        Guid id,
        string name,
        string? description,
        bool isActive,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradingSetup(
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
            throw new ArgumentException("A trading setup name is required.", nameof(name));
        }

        string normalized = name.Trim();
        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                name,
                $"The trading setup name cannot exceed {MaximumNameLength} characters.");
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
                $"The trading setup description cannot exceed {MaximumDescriptionLength} characters.");
        }

        return normalized;
    }
}
