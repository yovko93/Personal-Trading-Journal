using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradingRulePositionConfiguration : IEntityTypeConfiguration<TradingRulePositionRecord>
{
    public void Configure(EntityTypeBuilder<TradingRulePositionRecord> b)
    {
        b.ToTable("TradingRulePositions", t => t.HasCheckConstraint("CK_TradingRulePositions_Position",
            "InitialRevision = 1 AND typeof(Position) = 'integer' AND Position >= 0"));
        b.HasKey(r => r.RuleId); b.Property(r => r.RuleId).ValueGeneratedNever();
        b.HasIndex(r => new { r.Position, r.RuleId });
        b.HasOne<TradingRuleRevisionRecord>().WithMany().HasForeignKey(r => new { r.RuleId, r.InitialRevision })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TradingRuleCatalogStateConfiguration : IEntityTypeConfiguration<TradingRuleCatalogStateRecord>
{
    public void Configure(EntityTypeBuilder<TradingRuleCatalogStateRecord> b)
    {
        b.ToTable("TradingRuleCatalogState", t => t.HasCheckConstraint("CK_TradingRuleCatalogState_Version",
            "Id = 1 AND typeof(Version) = 'integer' AND Version >= 0"));
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
    }
}
