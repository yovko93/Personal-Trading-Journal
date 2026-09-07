namespace PersonalTradingJournal.Domain.Common;

/// <summary>
/// Provides canonical UTC audit timestamps for a domain entity.
/// </summary>
/// <remarks>
/// Audit timestamps must carry a zero offset and are never converted implicitly.
/// </remarks>
public abstract class AuditableEntity : Entity
{
    protected AuditableEntity(DateTimeOffset createdAtUtc)
        : this(Guid.NewGuid(), createdAtUtc, createdAtUtc)
    {
    }

    protected AuditableEntity(
        Guid id,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(id)
    {
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (createdAtUtc > updatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                updatedAtUtc,
                "The updated timestamp cannot be earlier than the created timestamp.");
        }

        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    protected void SetUpdatedAtUtc(DateTimeOffset updatedAtUtc)
    {
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (updatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                updatedAtUtc,
                "The updated timestamp cannot be earlier than the created timestamp.");
        }

        if (updatedAtUtc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                updatedAtUtc,
                "The updated timestamp cannot move backwards.");
        }

        UpdatedAtUtc = updatedAtUtc;
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Audit timestamps must use a UTC offset of zero.",
                parameterName);
        }
    }
}
