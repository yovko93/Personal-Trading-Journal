using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations;

/// <summary>Optional Criterion for global principles; historical text and assessment guards are copied unchanged.</summary>
public partial class OptionalGlobalRuleCriterion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, true);
    protected override void Down(MigrationBuilder migrationBuilder) => Rebuild(migrationBuilder, false);

    private static void Rebuild(MigrationBuilder migrationBuilder, bool allowEmpty)
    {
        // SQLite CHECK changes require a rebuild. Preserve all triggers, including assessment
        // triggers that reference this table. Check FKs before committing the transactional copy.
        migrationBuilder.Sql("PRAGMA foreign_keys=OFF;", suppressTransaction: true);
        migrationBuilder.Sql($"""
            CREATE TABLE __RuleRevisionsReplacement (
                RuleId TEXT NOT NULL, Revision INTEGER NOT NULL, ContractVersion INTEGER NOT NULL,
                Name TEXT NOT NULL, Criterion TEXT NOT NULL, RequiredEvidence TEXT NOT NULL,
                Applicability INTEGER NOT NULL, ScopeKind INTEGER NOT NULL, AccountId TEXT NULL,
                AccountNameAtRevision TEXT NULL, EvaluationMode INTEGER NOT NULL, IsActive INTEGER NOT NULL,
                RecordedAtUtc TEXT NOT NULL, AutomaticCriterionJson TEXT NULL, CheckCapability INTEGER NULL,
                IsGlobal INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT PK_TradingRuleRevisions PRIMARY KEY (RuleId, Revision),
                CONSTRAINT CK_TradingRuleRevisions_Definition CHECK (
                    ContractVersion = 1 AND Revision > 0 AND Applicability IN (1,2) AND EvaluationMode IN (1,2)
                    AND IsActive IN (0,1) AND length(trim(Name)) BETWEEN 1 AND 120
                    AND length(trim(Criterion)) BETWEEN {(allowEmpty ? "CASE WHEN IsGlobal = 1 THEN 0 ELSE 1 END" : "1")} AND 4000
                    AND length(trim(RequiredEvidence)) BETWEEN CASE WHEN IsGlobal = 1 THEN 0 ELSE 1 END AND 2000),
                CONSTRAINT CK_TradingRuleRevisions_Scope CHECK (
                    (ScopeKind = 1 AND AccountId IS NULL AND AccountNameAtRevision IS NULL)
                    OR (ScopeKind = 2 AND AccountId IS NOT NULL AND AccountNameAtRevision IS NOT NULL))
            );
            INSERT INTO __RuleRevisionsReplacement
                SELECT RuleId, Revision, ContractVersion, Name, Criterion, RequiredEvidence,
                       Applicability, ScopeKind, AccountId, AccountNameAtRevision, EvaluationMode,
                       IsActive, RecordedAtUtc, AutomaticCriterionJson, CheckCapability, IsGlobal
                FROM TradingRuleRevisions;
            DROP TRIGGER TradingRuleAssessments_Append;
            DROP TRIGGER TradingRuleAssessments_Evaluation;
            DROP TABLE TradingRuleRevisions;
            ALTER TABLE __RuleRevisionsReplacement RENAME TO TradingRuleRevisions;
            CREATE INDEX IX_TradingRuleRevisions_ScopeKind_AccountId_IsActive_RuleId
                ON TradingRuleRevisions (ScopeKind, AccountId, IsActive, RuleId);
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TradingRuleRevisions_NoUpdate BEFORE UPDATE ON TradingRuleRevisions BEGIN SELECT RAISE(ABORT, 'Trading Rule history is immutable'); END;
            CREATE TRIGGER TradingRuleRevisions_NoDelete BEFORE DELETE ON TradingRuleRevisions BEGIN SELECT RAISE(ABORT, 'Trading Rule history is retained'); END;
            CREATE TRIGGER TradingRuleRevisions_Append BEFORE INSERT ON TradingRuleRevisions BEGIN
            SELECT CASE WHEN NEW.RuleId = '00000000-0000-0000-0000-000000000000'
            OR NEW.Revision <> COALESCE((SELECT MAX(Revision) FROM TradingRuleRevisions WHERE RuleId = NEW.RuleId), 0) + 1
            OR (NEW.Revision = 1 AND NEW.IsActive <> 1)
            OR EXISTS (SELECT 1 FROM TradingRuleRevisions WHERE RuleId = NEW.RuleId AND RecordedAtUtc > NEW.RecordedAtUtc)
            THEN RAISE(ABORT, 'Invalid Rule revision sequence') END;
            SELECT CASE WHEN NEW.IsActive = 1 AND NEW.AccountId IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM TradingAccounts WHERE Id = NEW.AccountId AND Name = NEW.AccountNameAtRevision)
            THEN RAISE(ABORT, 'Rule Account unavailable') END;
            SELECT CASE WHEN EXISTS (SELECT 1 FROM TradingRuleRevisions p WHERE p.RuleId = NEW.RuleId AND p.Revision = NEW.Revision - 1
            AND ((p.IsActive = 0 AND NEW.IsActive = 0) OR (p.IsActive <> NEW.IsActive AND
            (p.Name <> NEW.Name OR p.Criterion <> NEW.Criterion OR p.RequiredEvidence <> NEW.RequiredEvidence
            OR p.Applicability <> NEW.Applicability OR p.ScopeKind <> NEW.ScopeKind OR p.AccountId IS NOT NEW.AccountId
            OR p.EvaluationMode <> NEW.EvaluationMode))))
            THEN RAISE(ABORT, 'Invalid Rule lifecycle transition') END;
            END;
            CREATE TRIGGER TradingRuleRevisions_TypedCriterion BEFORE INSERT ON TradingRuleRevisions BEGIN
            SELECT CASE WHEN NEW.AutomaticCriterionJson IS NOT NULL AND
            (NEW.EvaluationMode <> 2 OR NOT json_valid(NEW.AutomaticCriterionJson))
            THEN RAISE(ABORT, 'Invalid typed criterion') END;
            SELECT CASE WHEN EXISTS (SELECT 1 FROM TradingRuleRevisions p WHERE p.RuleId = NEW.RuleId
            AND p.Revision = NEW.Revision - 1 AND p.IsActive <> NEW.IsActive
            AND p.AutomaticCriterionJson IS NOT NEW.AutomaticCriterionJson)
            THEN RAISE(ABORT, 'Lifecycle transition must preserve typed criterion') END;
            END;
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
            CREATE TRIGGER TradingRuleAssessments_Evaluation BEFORE INSERT ON TradingRuleAssessments BEGIN
            SELECT CASE WHEN (NEW.EvaluationJson IS NULL) <> (NEW.EvaluationFingerprint IS NULL)
            OR (NEW.EvaluationJson IS NOT NULL AND
            (NEW.Source <> 2 OR NOT json_valid(NEW.EvaluationJson) OR length(NEW.EvaluationFingerprint) <> 64))
            OR (NEW.Source = 2 AND NEW.EvaluationJson IS NULL AND EXISTS
            (SELECT 1 FROM TradingRuleRevisions r WHERE r.RuleId = NEW.RuleId AND r.Revision = NEW.RuleRevision AND r.AutomaticCriterionJson IS NOT NULL))
            THEN RAISE(ABORT, 'Typed checks require evaluator evidence') END;
            END;
            CREATE TEMP TABLE RuleMigrationIntegrity (Errors INTEGER CHECK (Errors = 0));
            INSERT INTO RuleMigrationIntegrity SELECT COUNT(*) FROM pragma_foreign_key_check;
            DROP TABLE RuleMigrationIntegrity;
            """);
        // Downgrade refuses empty criteria at the copy CHECK, before touching history.
        migrationBuilder.Sql("PRAGMA foreign_keys=ON;", suppressTransaction: true);
    }
}
