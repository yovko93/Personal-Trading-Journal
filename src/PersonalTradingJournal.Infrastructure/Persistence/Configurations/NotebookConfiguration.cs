using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

internal static class NotebookConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        var notes = model.Entity<NotebookNoteRecord>();
        notes.ToTable("NotebookNotes", t =>
        {
            t.HasCheckConstraint("CK_NotebookNotes_Metadata", "Version > 0 AND length(trim(Title)) BETWEEN 1 AND 200 AND State IN (1,2,3) AND UpdatedAtUtc >= CreatedAtUtc");
            t.HasCheckConstraint("CK_NotebookNotes_Restore", "(State = 3 AND PreviousState IN (1,2) AND PreviousState IS NOT NULL AND PreviousFolderId IS FolderId) OR (State <> 3 AND PreviousState IS NULL AND PreviousFolderId IS NULL)");
        });
        notes.HasKey(x => x.Id); notes.Property(x => x.Id).ValueGeneratedNever();
        notes.Property(x => x.Version).IsConcurrencyToken();
        notes.Property(x => x.Title).IsRequired(); notes.Property(x => x.TitleKey).IsRequired().UseCollation("BINARY");
        notes.Property(x => x.DocumentJson).IsRequired(); notes.Property(x => x.PlainText).IsRequired(); notes.Property(x => x.SearchText).IsRequired();
        notes.Property(x => x.CreatedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        notes.Property(x => x.UpdatedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        notes.HasIndex(x => new { x.State, x.UpdatedAtUtc, x.Id });
        notes.HasIndex(x => new { x.State, x.TitleKey, x.Id });
        notes.HasIndex(x => new { x.FolderId, x.State });
        notes.HasOne<NotebookFolderRecord>().WithMany().HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.Restrict);
        notes.HasOne<NotebookFolderRecord>().WithMany().HasForeignKey(x => x.PreviousFolderId).OnDelete(DeleteBehavior.Restrict);

        Catalog(model.Entity<NotebookFolderRecord>(), "NotebookFolders", 128);
        Catalog(model.Entity<NotebookTagRecord>(), "NotebookTags", 64);
        var tags = model.Entity<NotebookTagAssignmentRecord>();
        tags.ToTable("NotebookTagAssignments"); tags.HasKey(x => new { x.NoteId, x.TagId });
        tags.HasOne<NotebookNoteRecord>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Restrict);
        tags.HasOne<NotebookTagRecord>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
        var links = model.Entity<NotebookLinkRecord>();
        links.ToTable("NotebookLinks", t => t.HasCheckConstraint("CK_NotebookLinks_Kind", "Kind IN (1,2) AND length(LastKnownLabel) BETWEEN 1 AND 2000"));
        links.HasKey(x => new { x.NoteId, x.Kind, x.TargetId });
        links.HasOne<NotebookNoteRecord>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Restrict);
        // Target IDs intentionally have no cascading FK: an unavailable source must not erase a note/reference.
        var images = model.Entity<NotebookImageRecord>();
        images.ToTable("NotebookImages", t =>
        {
            t.HasCheckConstraint("CK_NotebookImages_Position", "Position BETWEEN 0 AND 31 AND length(Caption) <= 2000 AND length(AlternativeText) <= 500");
            t.HasCheckConstraint("CK_NotebookImages_Ownership",
                "(Ownership = 1 AND StorageKey IS NOT NULL AND SizeBytes BETWEEN 1 AND 8388608 AND SizeBytes IS NOT NULL AND length(Sha256) = 64 AND Sha256 IS NOT NULL AND TradeId IS NULL AND TradeScreenshotId IS NULL) OR (Ownership = 2 AND StorageKey IS NULL AND SizeBytes IS NULL AND Sha256 IS NULL AND TradeId IS NOT NULL AND TradeScreenshotId IS NOT NULL)");
        });
        images.HasKey(x => new { x.NoteId, x.Id });
        images.Property(x => x.Id).ValueGeneratedNever();
        images.HasIndex(x => new { x.NoteId, x.Position }).IsUnique();
        images.Property(x => x.StorageKey).UseCollation("NOCASE");
        images.HasIndex(x => x.StorageKey).IsUnique().HasFilter("StorageKey IS NOT NULL");
        images.HasOne<NotebookNoteRecord>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void Catalog<T>(EntityTypeBuilder<T> b, string table, int maximum) where T : class
    {
        b.ToTable(table, t => t.HasCheckConstraint($"CK_{table}_Metadata", $"Version > 0 AND length(trim(Name)) BETWEEN 1 AND {maximum} AND UpdatedAtUtc >= CreatedAtUtc"));
        b.HasKey("Id"); b.Property<Guid>("Id").ValueGeneratedNever();
        b.Property<string>("Name").IsRequired();
        b.Property<string>("NameKey").IsRequired().UseCollation("BINARY");
        b.HasIndex("NameKey").IsUnique();
        b.Property<long>("Version").IsConcurrencyToken();
        b.Property<DateTimeOffset>("CreatedAtUtc").HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property<DateTimeOffset>("UpdatedAtUtc").HasConversion<SqliteUtcDateTimeOffsetConverter>();
    }
}
