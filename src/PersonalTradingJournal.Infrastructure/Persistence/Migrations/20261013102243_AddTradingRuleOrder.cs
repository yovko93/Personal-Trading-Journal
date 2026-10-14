using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingRuleOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingRuleCatalogState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingRuleCatalogState", x => x.Id);
                    table.CheckConstraint("CK_TradingRuleCatalogState_Version", "Id = 1 AND typeof(Version) = 'integer' AND Version >= 0");
                });

            migrationBuilder.CreateTable(
                name: "TradingRulePositions",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InitialRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingRulePositions", x => x.RuleId);
                    table.CheckConstraint("CK_TradingRulePositions_Position", "InitialRevision = 1 AND typeof(Position) = 'integer' AND Position >= 0");
                    table.ForeignKey(
                        name: "FK_TradingRulePositions_TradingRuleRevisions_RuleId_InitialRevision",
                        columns: x => new { x.RuleId, x.InitialRevision },
                        principalTable: "TradingRuleRevisions",
                        principalColumns: new[] { "RuleId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingRulePositions_Position_RuleId",
                table: "TradingRulePositions",
                columns: new[] { "Position", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_TradingRulePositions_RuleId_InitialRevision",
                table: "TradingRulePositions",
                columns: new[] { "RuleId", "InitialRevision" });

            migrationBuilder.Sql("""
                INSERT INTO TradingRuleCatalogState (Id, Version) VALUES (1, 0);
                INSERT INTO TradingRulePositions (RuleId, InitialRevision, Position)
                SELECT RuleId, 1, ROW_NUMBER() OVER (ORDER BY Name, RuleId) - 1
                FROM TradingRuleRevisions AS r
                WHERE IsGlobal = 1 AND IsActive = 1 AND Revision =
                    (SELECT MAX(Revision) FROM TradingRuleRevisions WHERE RuleId = r.RuleId);

                CREATE TRIGGER TR_TradingRuleCatalog_Append AFTER INSERT ON TradingRuleRevisions
                WHEN NEW.IsGlobal = 1
                BEGIN
                    INSERT INTO TradingRulePositions (RuleId, InitialRevision, Position)
                    SELECT NEW.RuleId, 1, COALESCE(MAX(Position), -1) + 1 FROM TradingRulePositions
                    HAVING NEW.Revision = 1 AND NEW.IsActive = 1;
                    UPDATE TradingRulePositions SET Position = Position - 1
                    WHERE NEW.IsActive = 0 AND Position >
                        (SELECT Position FROM TradingRulePositions WHERE RuleId = NEW.RuleId);
                    DELETE FROM TradingRulePositions WHERE NEW.IsActive = 0 AND RuleId = NEW.RuleId;
                    UPDATE TradingRuleCatalogState SET Version = Version + 1 WHERE Id = 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER TR_TradingRuleCatalog_Append;");
            migrationBuilder.DropTable(
                name: "TradingRuleCatalogState");

            migrationBuilder.DropTable(
                name: "TradingRulePositions");
        }
    }
}
