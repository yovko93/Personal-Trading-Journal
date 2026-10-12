using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradePlaybookAssociations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradePlaybookAssociations",
                columns: table => new
                {
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    TradingSetupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Revision = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TradeOpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradePlaybookAssociations", x => new { x.TradeId, x.Sequence });
                    table.CheckConstraint("CK_TradePlaybookAssociations_Snapshot", "Sequence > 0 AND ((Revision IS NULL AND DefinitionJson IS NULL) OR (TradingSetupId IS NOT NULL AND Revision > 0 AND DefinitionJson IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_TradePlaybookAssociations_PlaybookRevisions_TradingSetupId_Revision",
                        columns: x => new { x.TradingSetupId, x.Revision },
                        principalTable: "PlaybookRevisions",
                        principalColumns: new[] { "TradingSetupId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradePlaybookAssociations_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradePlaybookAssociations_TradingSetupId_Revision",
                table: "TradePlaybookAssociations",
                columns: new[] { "TradingSetupId", "Revision" });
            migrationBuilder.Sql("CREATE TRIGGER TradePlaybookAssociations_NoUpdate BEFORE UPDATE ON TradePlaybookAssociations BEGIN SELECT RAISE(ABORT, 'Trade association history is immutable'); END;");
            migrationBuilder.Sql("CREATE TRIGGER TradePlaybookAssociations_NoDelete BEFORE DELETE ON TradePlaybookAssociations WHEN EXISTS (SELECT 1 FROM Trades WHERE Id = OLD.TradeId) BEGIN SELECT RAISE(ABORT, 'Trade association history is retained'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradePlaybookAssociations");
        }
    }
}
