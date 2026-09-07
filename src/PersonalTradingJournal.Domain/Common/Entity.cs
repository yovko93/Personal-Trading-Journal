namespace PersonalTradingJournal.Domain.Common;

/// <summary>
/// Provides the minimal identity shared by domain entities.
/// </summary>
public abstract class Entity
{
    protected Entity()
        : this(Guid.NewGuid())
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An entity identifier cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; }
}
