using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations;

public partial class GlobalTradingRuleCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing immutable rows stay legacy. No scoped rule is silently broadened.
        migrationBuilder.AddColumn<int>("CheckCapability", "TradingRuleRevisions", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<bool>("IsGlobal", "TradingRuleRevisions", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.Sql("""
            CREATE TRIGGER TradingRuleRevisions_Global BEFORE INSERT ON TradingRuleRevisions BEGIN
                SELECT CASE WHEN NEW.IsGlobal <> 1 OR NEW.AccountId IS NOT NULL OR NEW.ScopeKind <> 1
                    OR NEW.Applicability <> 1 OR NEW.AccountNameAtRevision IS NOT NULL
                    OR EXISTS (SELECT 1 FROM TradingRuleRevisions p WHERE p.RuleId = NEW.RuleId AND (p.IsGlobal = 0 OR p.IsActive = 0))
                    OR (NEW.AutomaticCriterionJson IS NULL AND NEW.CheckCapability IS NOT NULL)
                    OR (NEW.AutomaticCriterionJson IS NOT NULL AND
                        (NEW.CheckCapability IS NULL OR NEW.CheckCapability NOT IN (1,2)
                         OR json_extract(NEW.AutomaticCriterionJson, '$.Capability') IS NOT NEW.CheckCapability))
                    THEN RAISE(ABORT, 'Only global Rules may be authored; legacy and deleted Rules are read-only') END;
            END;
            """);
        migrationBuilder.Sql("DROP TRIGGER TradingRuleAssessments_Append;");
        migrationBuilder.Sql("""
                CREATE TRIGGER TradingRuleAssessments_Append BEFORE INSERT ON TradingRuleAssessments BEGIN
                    SELECT CASE WHEN NEW.Id = '00000000-0000-0000-0000-000000000000'
                        OR NEW.TargetKey <> CASE WHEN NEW.TargetKind = 1
                            THEN 'trade:' || lower(NEW.TradeId) || ':' || lower(NEW.AccountId)
                            ELSE 'day:' || NEW.NewYorkDate || ':' || COALESCE(lower(NEW.AccountId), 'all') END
                        OR NEW.Sequence <> COALESCE((SELECT MAX(Sequence) FROM TradingRuleAssessments WHERE RuleId = NEW.RuleId AND TargetKey = NEW.TargetKey), 0) + 1
                        OR NEW.SupersedesAssessmentId IS NOT (SELECT Id FROM TradingRuleAssessments WHERE RuleId = NEW.RuleId AND TargetKey = NEW.TargetKey ORDER BY Sequence DESC LIMIT 1)
                        OR EXISTS (SELECT 1 FROM TradingRuleAssessments WHERE RuleId = NEW.RuleId AND TargetKey = NEW.TargetKey AND RecordedAtUtc > NEW.RecordedAtUtc)
                        THEN RAISE(ABORT, 'Invalid assessment sequence or target') END;
                    SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM TradingRuleRevisions r WHERE r.RuleId = NEW.RuleId AND r.Revision = NEW.RuleRevision
                        AND r.Revision = (SELECT MAX(Revision) FROM TradingRuleRevisions WHERE RuleId = NEW.RuleId)
                        AND r.IsGlobal = 1 AND r.IsActive = 1 AND r.RecordedAtUtc <= NEW.RecordedAtUtc
                        AND (NEW.Source = 1 OR (r.EvaluationMode = 2 AND r.CheckCapability = NEW.TargetKind)))
                        THEN RAISE(ABORT, 'Rule revision or scope changed') END;
                    SELECT CASE WHEN (NEW.AccountId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TradingAccounts WHERE Id = NEW.AccountId AND Name = NEW.AccountNameAtAssessment))
                        OR (NEW.TargetKind = 1 AND NOT EXISTS (SELECT 1 FROM Trades WHERE Id = NEW.TradeId AND TradingAccountId = NEW.AccountId))
                        THEN RAISE(ABORT, 'Assessment target unavailable') END;
                    SELECT CASE WHEN (NEW.Outcome = 2 OR (NEW.Outcome = 1 AND NEW.Basis = 1))
                        AND NOT EXISTS (SELECT 1 FROM json_each(NEW.EvidenceJson) WHERE json_extract(value, '$.kind') IN (1,2,3))
                        THEN RAISE(ABORT, 'A Playbook link alone is not assessment evidence') END;
                END;
                """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Global definitions cannot be narrowed to the old semantics by a downgrade.
        migrationBuilder.Sql("""
            CREATE TEMP TABLE GlobalRuleDowngradeGuard (Value INTEGER CHECK(Value = 0));
            INSERT INTO GlobalRuleDowngradeGuard SELECT COUNT(*) FROM TradingRuleRevisions WHERE IsGlobal = 1;
            DROP TABLE GlobalRuleDowngradeGuard;
            DROP TRIGGER TradingRuleRevisions_Global;
            DROP TRIGGER TradingRuleAssessments_Append;
            ALTER TABLE TradingRuleRevisions DROP COLUMN CheckCapability;
            ALTER TABLE TradingRuleRevisions DROP COLUMN IsGlobal;
            """);
        migrationBuilder.Sql("""
                CREATE TRIGGER TradingRuleAssessments_Append BEFORE INSERT ON TradingRuleAssessments BEGIN
                    SELECT CASE WHEN NEW.Id = '00000000-0000-0000-0000-000000000000'
                        OR NEW.TargetKey <> CASE WHEN NEW.TargetKind = 1
                            THEN 'trade:' || lower(NEW.TradeId) || ':' || lower(NEW.AccountId)
                            ELSE 'day:' || NEW.NewYorkDate || ':' || COALESCE(lower(NEW.AccountId), 'all') END
                        OR NEW.Sequence <> COALESCE((SELECT MAX(Sequence) FROM TradingRuleAssessments WHERE RuleId = NEW.RuleId AND TargetKey = NEW.TargetKey), 0) + 1
                        OR NEW.SupersedesAssessmentId IS NOT (SELECT Id FROM TradingRuleAssessments WHERE RuleId = NEW.RuleId AND TargetKey = NEW.TargetKey ORDER BY Sequence DESC LIMIT 1)
                        OR EXISTS (SELECT 1 FROM TradingRuleAssessments WHERE RuleId = NEW.RuleId AND TargetKey = NEW.TargetKey AND RecordedAtUtc > NEW.RecordedAtUtc)
                        THEN RAISE(ABORT, 'Invalid assessment sequence or target') END;
                    SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM TradingRuleRevisions r WHERE r.RuleId = NEW.RuleId AND r.Revision = NEW.RuleRevision
                        AND r.Revision = (SELECT MAX(Revision) FROM TradingRuleRevisions WHERE RuleId = NEW.RuleId)
                        AND r.IsActive = 1 AND r.RecordedAtUtc <= NEW.RecordedAtUtc AND r.Applicability = NEW.TargetKind
                        AND (NEW.Source = 1 OR r.EvaluationMode = 2)
                        AND (r.ScopeKind = 1 OR (r.ScopeKind = NEW.ScopeKind AND r.AccountId = NEW.AccountId)))
                        THEN RAISE(ABORT, 'Rule revision or scope changed') END;
                    SELECT CASE WHEN (NEW.AccountId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TradingAccounts WHERE Id = NEW.AccountId AND Name = NEW.AccountNameAtAssessment))
                        OR (NEW.TargetKind = 1 AND NOT EXISTS (SELECT 1 FROM Trades WHERE Id = NEW.TradeId AND TradingAccountId = NEW.AccountId))
                        THEN RAISE(ABORT, 'Assessment target unavailable') END;
                    SELECT CASE WHEN (NEW.Outcome = 2 OR (NEW.Outcome = 1 AND NEW.Basis = 1))
                        AND NOT EXISTS (SELECT 1 FROM json_each(NEW.EvidenceJson) WHERE json_extract(value, '$.kind') IN (1,2,3))
                        THEN RAISE(ABORT, 'A Playbook link alone is not assessment evidence') END;
                END;
                """);
    }
}
