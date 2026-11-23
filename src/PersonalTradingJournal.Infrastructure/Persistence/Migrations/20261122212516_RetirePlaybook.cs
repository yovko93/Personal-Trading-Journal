using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations;

public partial class RetirePlaybook : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        LegacyPlaybookArchive.Capture(migrationBuilder);
        // Only editable Drafts lose the retired relationship. Keep their original link and tokens
        // in the historical archive. Ready revisions and saved review payloads remain byte-for-byte intact.
        migrationBuilder.Sql("""
            UPDATE TradingPlans SET
                DraftJson = json_set(DraftJson, '$.Content.Scenarios',
                    (SELECT json_group_array(json_set(value, '$.Setup', NULL) ORDER BY CAST(key AS INTEGER))
                     FROM json_each(DraftJson, '$.Content.Scenarios'))),
                ReferenceStateJson = (SELECT json_group_object(key, value) FROM json_each(ReferenceStateJson)
                                      WHERE key NOT LIKE '3:%')
            WHERE DraftJson IS NOT NULL AND EXISTS
                (SELECT 1 FROM json_each(DraftJson, '$.Content.Scenarios') WHERE json_type(value, '$.Setup') = 'object');
            """);
        migrationBuilder.DropTable(
            name: "CurrentPlaybookExamples");

        migrationBuilder.DropTable(
            name: "PlaybookExamples");

        migrationBuilder.DropTable(
            name: "TradePlaybookAssociations");

        migrationBuilder.DropTable(
            name: "TradePlaybookContexts");

        migrationBuilder.DropTable(
            name: "CurrentPlaybooks");

        migrationBuilder.DropTable(
            name: "PlaybookRevisions");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("CREATE TEMP TABLE DowngradeGuard (v INTEGER CONSTRAINT RestorePreRetirementBackup CHECK (v = 1)); INSERT INTO DowngradeGuard VALUES (0);");
}
