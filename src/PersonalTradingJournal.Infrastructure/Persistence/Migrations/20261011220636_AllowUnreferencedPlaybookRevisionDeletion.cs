using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowUnreferencedPlaybookRevisionDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[] { "PlaybookExamples", "PlaybookRevisions" })
            {
                migrationBuilder.Sql($"DROP TRIGGER {table}_NoDelete;");
                migrationBuilder.Sql($"""
                    CREATE TRIGGER {table}_NoDelete BEFORE DELETE ON {table}
                    WHEN OLD.Revision >= (SELECT MAX(Revision) FROM PlaybookRevisions WHERE TradingSetupId = OLD.TradingSetupId)
                      OR EXISTS (SELECT 1 FROM TradePlaybookContexts WHERE TradingSetupId = OLD.TradingSetupId AND Revision = OLD.Revision)
                      OR EXISTS (SELECT 1 FROM TradePlaybookAssociations WHERE TradingSetupId = OLD.TradingSetupId AND Revision = OLD.Revision)
                    BEGIN SELECT RAISE(ABORT, 'Current or referenced Playbook revision is retained'); END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[] { "PlaybookExamples", "PlaybookRevisions" })
            {
                migrationBuilder.Sql($"DROP TRIGGER {table}_NoDelete;");
                migrationBuilder.Sql($"CREATE TRIGGER {table}_NoDelete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT, 'Playbook history is retained'); END;");
            }
        }
    }
}
