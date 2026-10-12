using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingRuleRevisions",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    ContractVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Criterion = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RequiredEvidence = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Applicability = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AccountNameAtRevision = table.Column<string>(type: "TEXT", nullable: true),
                    EvaluationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingRuleRevisions", x => new { x.RuleId, x.Revision });
                    table.CheckConstraint("CK_TradingRuleRevisions_Definition", "ContractVersion = 1 AND Revision > 0 AND Applicability IN (1,2) AND EvaluationMode IN (1,2) AND IsActive IN (0,1) AND length(trim(Name)) BETWEEN 1 AND 120 AND length(trim(Criterion)) BETWEEN 1 AND 4000 AND length(trim(RequiredEvidence)) BETWEEN 1 AND 2000");
                    table.CheckConstraint("CK_TradingRuleRevisions_Scope", "(ScopeKind = 1 AND AccountId IS NULL AND AccountNameAtRevision IS NULL) OR (ScopeKind = 2 AND AccountId IS NOT NULL AND AccountNameAtRevision IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "TradingRuleAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetKind = table.Column<int>(type: "INTEGER", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NewYorkDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ScopeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AccountNameAtAssessment = table.Column<string>(type: "TEXT", nullable: true),
                    TargetFingerprint = table.Column<string>(type: "TEXT", nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Basis = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceSufficiency = table.Column<int>(type: "INTEGER", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    SupersedesAssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingRuleAssessments", x => x.Id);
                    table.CheckConstraint("CK_TradingRuleAssessments_Outcome", "(Basis = 1 OR (Basis = 2 AND Outcome = 1 AND Source = 1)) AND (Outcome <> 3 OR (EvidenceSufficiency IN (2,3) AND Basis = 1)) AND ((Outcome <> 2 AND NOT (Outcome = 1 AND Basis = 1)) OR (EvidenceSufficiency = 1 AND json_array_length(EvidenceJson) > 0))");
                    table.CheckConstraint("CK_TradingRuleAssessments_Scope", "(ScopeKind = 1 AND AccountId IS NULL AND AccountNameAtAssessment IS NULL) OR (ScopeKind = 2 AND AccountId IS NOT NULL AND AccountNameAtAssessment IS NOT NULL)");
                    table.CheckConstraint("CK_TradingRuleAssessments_State", "Sequence > 0 AND Outcome IN (1,2,3) AND Source IN (1,2) AND Basis IN (1,2) AND EvidenceSufficiency IN (1,2,3) AND length(trim(Explanation)) BETWEEN 1 AND 4000 AND json_valid(EvidenceJson) AND json_type(EvidenceJson) = 'array' AND json_array_length(EvidenceJson) <= 1000");
                    table.CheckConstraint("CK_TradingRuleAssessments_Target", "(TargetKind = 1 AND TradeId IS NOT NULL AND NewYorkDate IS NULL AND ScopeKind = 2 AND TargetFingerprint IS NOT NULL) OR (TargetKind = 2 AND TradeId IS NULL AND NewYorkDate IS NOT NULL AND TargetFingerprint IS NULL)");
                    table.ForeignKey(
                        name: "FK_TradingRuleAssessments_TradingRuleAssessments_SupersedesAssessmentId",
                        column: x => x.SupersedesAssessmentId,
                        principalTable: "TradingRuleAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradingRuleAssessments_TradingRuleRevisions_RuleId_RuleRevision",
                        columns: x => new { x.RuleId, x.RuleRevision },
                        principalTable: "TradingRuleRevisions",
                        principalColumns: new[] { "RuleId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingRuleAssessments_RuleId_RuleRevision",
                table: "TradingRuleAssessments",
                columns: new[] { "RuleId", "RuleRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_TradingRuleAssessments_RuleId_TargetKey_Sequence",
                table: "TradingRuleAssessments",
                columns: new[] { "RuleId", "TargetKey", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradingRuleAssessments_SupersedesAssessmentId",
                table: "TradingRuleAssessments",
                column: "SupersedesAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TradingRuleRevisions_ScopeKind_AccountId_IsActive_RuleId",
                table: "TradingRuleRevisions",
                columns: new[] { "ScopeKind", "AccountId", "IsActive", "RuleId" });

            foreach (string table in new[] { "TradingRuleRevisions", "TradingRuleAssessments" })
            {
                migrationBuilder.Sql($"CREATE TRIGGER {table}_NoUpdate BEFORE UPDATE ON {table} BEGIN SELECT RAISE(ABORT, 'Trading Rule history is immutable'); END;");
                migrationBuilder.Sql($"CREATE TRIGGER {table}_NoDelete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT, 'Trading Rule history is retained'); END;");
            }
            migrationBuilder.Sql("""
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingRuleAssessments");

            migrationBuilder.DropTable(
                name: "TradingRuleRevisions");
        }
    }
}
