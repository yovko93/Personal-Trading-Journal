using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradingPlanConfiguration : IEntityTypeConfiguration<TradingPlanRecord>, IEntityTypeConfiguration<TradingPlanReadyRecord>
{
    public void Configure(EntityTypeBuilder<TradingPlanRecord> b)
    {
        b.ToTable("TradingPlans", t => t.HasCheckConstraint("CK_TradingPlans_State",
            "Version > 0 AND LatestReadyRevision >= 0 AND ((DraftJson IS NOT NULL AND ReferenceStateJson IS NOT NULL) OR (DraftJson IS NULL AND ReferenceStateJson IS NULL AND LatestReadyRevision > 0))"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.AccountId, x.NewYorkDate }).IsUnique();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.Property(x => x.UpdatedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        // Account/catalog IDs are historical identities, not cascade-delete ownership.
    }
    public void Configure(EntityTypeBuilder<TradingPlanReadyRecord> b)
    {
        b.ToTable("TradingPlanReadyRevisions", t =>
        {
            t.HasCheckConstraint("CK_TradingPlanReady_Revision", "Revision > 0");
            t.HasTrigger("TradingPlanReady_NoUpdate"); t.HasTrigger("TradingPlanReady_NoDelete");
            t.HasTrigger("TradingPlanReady_Sequence");
        });
        b.HasKey(x => new { x.PlanId, x.Revision });
        b.Property(x => x.SnapshotJson).IsRequired();
        b.Property(x => x.ReadyAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.HasOne<TradingPlanRecord>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
