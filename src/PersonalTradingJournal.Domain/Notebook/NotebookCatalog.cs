using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Notebook;

// Uniqueness is checked transactionally by the application store, never by a domain database lookup.
public sealed class NotebookFolder : AuditableEntity
{
    public NotebookFolder(string name, DateTimeOffset now) : base(now) => Name = NotebookLimits.Name(name, NotebookLimits.FolderName);
    private NotebookFolder(Guid id, string name, long version, DateTimeOffset created, DateTimeOffset updated)
        : base(id, created, updated)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        Name = NotebookLimits.Name(name, NotebookLimits.FolderName); Version = version;
    }
    public string Name { get; private set; }
    public long Version { get; private set; } = 1;
    public static StringComparer NameComparer => StringComparer.OrdinalIgnoreCase;
    public static NotebookFolder Rehydrate(Guid id, string name, long version, DateTimeOffset created, DateTimeOffset updated) =>
        new(id, name, version, created, updated);
    public void Rename(string name, DateTimeOffset now)
    {
        var normalized = NotebookLimits.Name(name, NotebookLimits.FolderName);
        if (Name == normalized) return;
        var next = checked(Version + 1); SetUpdatedAtUtc(now); Name = normalized; Version = next;
    }
}

public sealed class NotebookTag : AuditableEntity
{
    public NotebookTag(string name, DateTimeOffset now) : base(now) => Name = NotebookLimits.Name(name, NotebookLimits.TagName);
    private NotebookTag(Guid id, string name, long version, DateTimeOffset created, DateTimeOffset updated)
        : base(id, created, updated)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        Name = NotebookLimits.Name(name, NotebookLimits.TagName); Version = version;
    }
    public string Name { get; private set; }
    public long Version { get; private set; } = 1;
    public static StringComparer NameComparer => StringComparer.OrdinalIgnoreCase;
    public static NotebookTag Rehydrate(Guid id, string name, long version, DateTimeOffset created, DateTimeOffset updated) =>
        new(id, name, version, created, updated);
    public void Rename(string name, DateTimeOffset now)
    {
        var normalized = NotebookLimits.Name(name, NotebookLimits.TagName);
        if (Name == normalized) return;
        var next = checked(Version + 1); SetUpdatedAtUtc(now); Name = normalized; Version = next;
    }
}
