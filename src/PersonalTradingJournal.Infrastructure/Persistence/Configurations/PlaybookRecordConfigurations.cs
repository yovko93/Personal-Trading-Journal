using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradePlaybookAssociationRecordConfiguration : IEntityTypeConfiguration<TradePlaybookAssociationRecord>
{
    public void Configure(EntityTypeBuilder<TradePlaybookAssociationRecord> b)
    {
        b.ToTable("TradePlaybookAssociations", t =>
        {
            t.HasCheckConstraint("CK_TradePlaybookAssociations_Snapshot", "Sequence > 0 AND ((Revision IS NULL AND DefinitionJson IS NULL) OR (TradingSetupId IS NOT NULL AND Revision > 0 AND DefinitionJson IS NOT NULL))");
            t.HasTrigger("TradePlaybookAssociations_NoUpdate");
            t.HasTrigger("TradePlaybookAssociations_NoDelete");
        });
        b.HasKey(r => new { r.TradeId, r.Sequence });
        b.Property(r => r.TradeId).ValueGeneratedNever();
        b.Property(r => r.Sequence).ValueGeneratedNever();
        b.Property(r => r.RecordedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property(r => r.TradeOpenedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.HasOne<TradeRecord>().WithMany().HasForeignKey(r => r.TradeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PlaybookRevisionRecord>().WithMany().HasForeignKey(r => new { r.TradingSetupId, r.Revision }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlaybookRevisionRecordConfiguration : IEntityTypeConfiguration<PlaybookRevisionRecord>
{
    public void Configure(EntityTypeBuilder<PlaybookRevisionRecord> b)
    {
        b.ToTable("PlaybookRevisions", t =>
        {
            t.HasCheckConstraint("CK_PlaybookRevisions_Version", "ContractVersion = 1 AND Revision >= 1");
            t.HasTrigger("PlaybookRevisions_NoUpdate");
            t.HasTrigger("PlaybookRevisions_NoDelete");
        });
        b.HasKey(r => new { r.TradingSetupId, r.Revision });
        b.Property(r => r.TradingSetupId).ValueGeneratedNever();
        b.Property(r => r.Revision).ValueGeneratedNever();
        b.Property(r => r.SetupName).HasMaxLength(128).IsRequired();
        b.Property(r => r.SetupDescription).HasMaxLength(2000);
        b.Property(r => r.MarketContext).HasMaxLength(4000).IsRequired();
        b.Property(r => r.EntryConditions).HasMaxLength(4000).IsRequired();
        b.Property(r => r.Confirmations).HasMaxLength(4000).IsRequired();
        b.Property(r => r.Invalidation).HasMaxLength(4000).IsRequired();
        b.Property(r => r.ExecutionGuidance).HasMaxLength(4000).IsRequired();
        b.Property(r => r.RecordedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.HasOne<TradingSetupRecord>().WithMany().HasForeignKey(r => r.TradingSetupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlaybookExampleRecordConfiguration : IEntityTypeConfiguration<PlaybookExampleRecord>
{
    public void Configure(EntityTypeBuilder<PlaybookExampleRecord> b)
    {
        b.ToTable("PlaybookExamples", t =>
        {
            t.HasCheckConstraint("CK_PlaybookExamples_Position", "Position >= 0 AND Position < 12");
            t.HasCheckConstraint("CK_PlaybookExamples_Source", "SourceScreenshotId IS NULL OR SourceTradeId IS NOT NULL");
            t.HasTrigger("PlaybookExamples_NoUpdate");
            t.HasTrigger("PlaybookExamples_NoDelete");
        });
        b.HasKey(r => new { r.TradingSetupId, r.Revision, r.ExampleId });
        b.Property(r => r.ExampleId).ValueGeneratedNever();
        b.Property(r => r.Annotation).HasMaxLength(4000).IsRequired();
        b.Property(r => r.Kind).HasDefaultValue(0);
        b.Property(r => r.ImageStorageKey).HasMaxLength(180);
        b.Property(r => r.ImageFileName).HasMaxLength(180);
        b.Property(r => r.ImageSha256).HasMaxLength(64);
        b.HasIndex(r => r.ImageStorageKey);
        b.HasIndex(r => new { r.TradingSetupId, r.Revision, r.Position }).IsUnique();
        b.HasOne<PlaybookRevisionRecord>().WithMany().HasForeignKey(r => new { r.TradingSetupId, r.Revision }).OnDelete(DeleteBehavior.Restrict);
        // An immutable historical reference pins the existing Trade-owned screenshot metadata/file.
        b.HasOne<TradeScreenshotRecord>().WithMany().HasForeignKey(r => r.SourceScreenshotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TradePlaybookContextRecordConfiguration : IEntityTypeConfiguration<TradePlaybookContextRecord>
{
    public void Configure(EntityTypeBuilder<TradePlaybookContextRecord> b)
    {
        b.ToTable("TradePlaybookContexts", t =>
        {
            t.HasCheckConstraint("CK_TradePlaybookContexts_Version", "ContractVersion = 1");
            t.HasTrigger("TradePlaybookContexts_NoUpdate");
        });
        b.HasKey(r => r.TradeId);
        b.Property(r => r.TradeId).ValueGeneratedNever();
        b.Property(r => r.TradeOpenedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property(r => r.CapturedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property(r => r.DefinitionJson).IsRequired();
        b.HasOne<TradeRecord>().WithMany().HasForeignKey(r => r.TradeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PlaybookRevisionRecord>().WithMany().HasForeignKey(r => new { r.TradingSetupId, r.Revision }).OnDelete(DeleteBehavior.Restrict);
    }
}
