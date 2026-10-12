using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradingRuleRevisionRecordConfiguration : IEntityTypeConfiguration<TradingRuleRevisionRecord>
{
    public void Configure(EntityTypeBuilder<TradingRuleRevisionRecord> b)
    {
        b.ToTable("TradingRuleRevisions", t =>
        {
            t.HasCheckConstraint("CK_TradingRuleRevisions_Definition", "ContractVersion = 1 AND Revision > 0 AND Applicability IN (1,2) AND EvaluationMode IN (1,2) AND IsActive IN (0,1) AND length(trim(Name)) BETWEEN 1 AND 120 AND length(trim(Criterion)) BETWEEN 1 AND 4000 AND length(trim(RequiredEvidence)) BETWEEN 1 AND 2000");
            t.HasCheckConstraint("CK_TradingRuleRevisions_Scope", "(ScopeKind = 1 AND AccountId IS NULL AND AccountNameAtRevision IS NULL) OR (ScopeKind = 2 AND AccountId IS NOT NULL AND AccountNameAtRevision IS NOT NULL)");
            t.HasTrigger("TradingRuleRevisions_NoUpdate"); t.HasTrigger("TradingRuleRevisions_NoDelete");
            t.HasTrigger("TradingRuleRevisions_Append");
        });
        b.HasKey(r => new { r.RuleId, r.Revision });
        b.Property(r => r.RuleId).ValueGeneratedNever(); b.Property(r => r.Revision).ValueGeneratedNever();
        b.Property(r => r.Name).IsRequired().HasMaxLength(120);
        b.Property(r => r.Criterion).IsRequired().HasMaxLength(4000);
        b.Property(r => r.RequiredEvidence).IsRequired().HasMaxLength(2000);
        b.Property(r => r.RecordedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.HasIndex(r => new { r.ScopeKind, r.AccountId, r.IsActive, r.RuleId });
        // Historical identity, deliberately no cascading FK to the mutable Account catalog.
    }
}

public sealed class TradingRuleAssessmentRecordConfiguration : IEntityTypeConfiguration<TradingRuleAssessmentRecord>
{
    public void Configure(EntityTypeBuilder<TradingRuleAssessmentRecord> b)
    {
        b.ToTable("TradingRuleAssessments", t =>
        {
            t.HasCheckConstraint("CK_TradingRuleAssessments_State", "Sequence > 0 AND Outcome IN (1,2,3) AND Source IN (1,2) AND Basis IN (1,2) AND EvidenceSufficiency IN (1,2,3) AND length(trim(Explanation)) BETWEEN 1 AND 4000 AND json_valid(EvidenceJson) AND json_type(EvidenceJson) = 'array' AND json_array_length(EvidenceJson) <= 1000");
            t.HasCheckConstraint("CK_TradingRuleAssessments_Scope", "(ScopeKind = 1 AND AccountId IS NULL AND AccountNameAtAssessment IS NULL) OR (ScopeKind = 2 AND AccountId IS NOT NULL AND AccountNameAtAssessment IS NOT NULL)");
            t.HasCheckConstraint("CK_TradingRuleAssessments_Target", "(TargetKind = 1 AND TradeId IS NOT NULL AND NewYorkDate IS NULL AND ScopeKind = 2 AND TargetFingerprint IS NOT NULL) OR (TargetKind = 2 AND TradeId IS NULL AND NewYorkDate IS NOT NULL AND TargetFingerprint IS NULL)");
            t.HasCheckConstraint("CK_TradingRuleAssessments_Outcome", "(Basis = 1 OR (Basis = 2 AND Outcome = 1 AND Source = 1)) AND (Outcome <> 3 OR (EvidenceSufficiency IN (2,3) AND Basis = 1)) AND ((Outcome <> 2 AND NOT (Outcome = 1 AND Basis = 1)) OR (EvidenceSufficiency = 1 AND json_array_length(EvidenceJson) > 0))");
            t.HasTrigger("TradingRuleAssessments_NoUpdate"); t.HasTrigger("TradingRuleAssessments_NoDelete");
            t.HasTrigger("TradingRuleAssessments_Append");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.TargetKey).IsRequired().HasMaxLength(100);
        b.Property(r => r.Explanation).IsRequired().HasMaxLength(4000);
        b.Property(r => r.EvidenceJson).IsRequired();
        b.Property(r => r.RecordedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        b.HasIndex(r => new { r.RuleId, r.TargetKey, r.Sequence }).IsUnique();
        b.HasOne<TradingRuleRevisionRecord>().WithMany().HasForeignKey(r => new { r.RuleId, r.RuleRevision }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TradingRuleAssessmentRecord>().WithMany().HasForeignKey(r => r.SupersedesAssessmentId).OnDelete(DeleteBehavior.Restrict);
        // Trade/Account/source IDs are historical identities with explicit read-time availability.
    }
}
