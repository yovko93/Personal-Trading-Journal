using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTradeWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire only Trade capture rows. Frozen review JSON, Rule assessments,
            // Playbook revisions/examples and all image files remain untouched.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TradePlaybookAssociations_NoDelete;");
            migrationBuilder.Sql("DELETE FROM TradePlaybookAssociations; DELETE FROM TradePlaybookContexts;");
            migrationBuilder.Sql("CREATE TRIGGER TradePlaybookAssociations_Retired BEFORE INSERT ON TradePlaybookAssociations BEGIN SELECT RAISE(ABORT, 'Trade Playbook associations are retired'); END;");
            migrationBuilder.Sql("CREATE TRIGGER TradePlaybookContexts_Retired BEFORE INSERT ON TradePlaybookContexts BEGIN SELECT RAISE(ABORT, 'Trade Playbook captures are retired'); END;");
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Trades",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Downgrade restores the schema capability, never deleted historical rows.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TradePlaybookAssociations_Retired; DROP TRIGGER IF EXISTS TradePlaybookContexts_Retired;");
            migrationBuilder.Sql("CREATE TRIGGER IF NOT EXISTS TradePlaybookAssociations_NoDelete BEFORE DELETE ON TradePlaybookAssociations WHEN EXISTS (SELECT 1 FROM Trades WHERE Id = OLD.TradeId) BEGIN SELECT RAISE(ABORT, 'Trade association history is retained'); END;");
            migrationBuilder.Sql("ALTER TABLE Trades DROP COLUMN Description;");
        }
    }
}

