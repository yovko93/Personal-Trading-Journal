using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeterministicRuleChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradingRuleAssessments_RuleId_RuleRevision",
                table: "TradingRuleAssessments");

            migrationBuilder.AddColumn<string>(
                name: "AutomaticCriterionJson",
                table: "TradingRuleRevisions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationFingerprint",
                table: "TradingRuleAssessments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationJson",
                table: "TradingRuleAssessments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradingRuleAssessments_RuleId_RuleRevision_TargetKey_EvaluationFingerprint",
                table: "TradingRuleAssessments",
                columns: new[] { "RuleId", "RuleRevision", "TargetKey", "EvaluationFingerprint" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER TradingRuleRevisions_TypedCriterion BEFORE INSERT ON TradingRuleRevisions BEGIN
                    SELECT CASE WHEN NEW.AutomaticCriterionJson IS NOT NULL AND
                        (NEW.EvaluationMode <> 2 OR NOT json_valid(NEW.AutomaticCriterionJson))
                        THEN RAISE(ABORT, 'Invalid typed criterion') END;
                    SELECT CASE WHEN EXISTS (SELECT 1 FROM TradingRuleRevisions p WHERE p.RuleId = NEW.RuleId
                        AND p.Revision = NEW.Revision - 1 AND p.IsActive <> NEW.IsActive
                        AND p.AutomaticCriterionJson IS NOT NEW.AutomaticCriterionJson)
                        THEN RAISE(ABORT, 'Lifecycle transition must preserve typed criterion') END;
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER TradingRuleAssessments_Evaluation BEFORE INSERT ON TradingRuleAssessments BEGIN
                    SELECT CASE WHEN (NEW.EvaluationJson IS NULL) <> (NEW.EvaluationFingerprint IS NULL)
                        OR (NEW.EvaluationJson IS NOT NULL AND
                            (NEW.Source <> 2 OR NOT json_valid(NEW.EvaluationJson) OR length(NEW.EvaluationFingerprint) <> 64))
                        OR (NEW.Source = 2 AND NEW.EvaluationJson IS NULL AND EXISTS
                            (SELECT 1 FROM TradingRuleRevisions r WHERE r.RuleId = NEW.RuleId AND r.Revision = NEW.RuleRevision AND r.AutomaticCriterionJson IS NOT NULL))
                        THEN RAISE(ABORT, 'Typed checks require evaluator evidence') END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TradingRuleAssessments_Evaluation;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TradingRuleRevisions_TypedCriterion;");
            migrationBuilder.DropIndex(
                name: "IX_TradingRuleAssessments_RuleId_RuleRevision_TargetKey_EvaluationFingerprint",
                table: "TradingRuleAssessments");

            // Native SQLite DROP COLUMN preserves the pre-existing append-only triggers.
            // EF's table rebuild temporarily drops their referenced table and loses triggers.
            migrationBuilder.Sql("ALTER TABLE TradingRuleRevisions DROP COLUMN AutomaticCriterionJson;");
            migrationBuilder.Sql("ALTER TABLE TradingRuleAssessments DROP COLUMN EvaluationFingerprint;");
            migrationBuilder.Sql("ALTER TABLE TradingRuleAssessments DROP COLUMN EvaluationJson;");

            migrationBuilder.CreateIndex(
                name: "IX_TradingRuleAssessments_RuleId_RuleRevision",
                table: "TradingRuleAssessments",
                columns: new[] { "RuleId", "RuleRevision" });
        }
    }
}
