using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class PeriodReviewRecordConfiguration : IEntityTypeConfiguration<PeriodReviewRecord>
{
    public void Configure(EntityTypeBuilder<PeriodReviewRecord> b)
    {
        b.ToTable("PeriodReviews", t =>
        {
            t.HasCheckConstraint("CK_PeriodReviews_Scope", "(ScopeKind = 0 AND AccountId IS NULL) OR (ScopeKind = 1 AND AccountId IS NOT NULL)");
            t.HasCheckConstraint("CK_PeriodReviews_Period", "Kind IN (0,1) AND FirstDate <= LastDate AND FromUtc < BeforeUtc");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.FromUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property(r => r.BeforeUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property(r => r.GeneratedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property(r => r.Provider).IsRequired().HasMaxLength(64); b.Property(r => r.Model).IsRequired().HasMaxLength(128);
        b.Property(r => r.EvidenceContractVersion).IsRequired(); b.Property(r => r.ResponseContractVersion).IsRequired();
        b.Property(r => r.PacketId).IsRequired().HasMaxLength(64);
        b.Property(r => r.EvidenceJson).IsRequired(); b.Property(r => r.ResponseJson).IsRequired(); b.Property(r => r.MetadataJson).IsRequired();
        b.HasIndex(r => new { r.Kind, r.FirstDate, r.LastDate, r.ScopeKind, r.AccountId, r.GeneratedAtUtc, r.Id });
    }
}
