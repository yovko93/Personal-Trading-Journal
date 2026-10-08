using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class CoachingAnalysisRecordConfiguration : IEntityTypeConfiguration<CoachingAnalysisRecord>
{
    public void Configure(EntityTypeBuilder<CoachingAnalysisRecord> builder)
    {
        builder.ToTable("CoachingAnalyses", t => t.HasCheckConstraint("CK_CoachingAnalyses_Scope",
            "(ScopeKind = 0 AND AccountId IS NULL) OR (ScopeKind = 1 AND AccountId IS NOT NULL)"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.GeneratedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        builder.Property(r => r.Provider).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Model).IsRequired().HasMaxLength(128);
        builder.Property(r => r.EvidenceContractVersion).IsRequired();
        builder.Property(r => r.ResponseContractVersion).IsRequired();
        builder.Property(r => r.PacketId).IsRequired().HasMaxLength(64);
        builder.Property(r => r.EvidenceJson).IsRequired();
        builder.Property(r => r.ResponseJson).IsRequired();
        builder.Property(r => r.MetadataJson).IsRequired();
        builder.HasIndex(r => new { r.ReviewDate, r.ScopeKind, r.AccountId, r.GeneratedAtUtc, r.Id });
    }
}
