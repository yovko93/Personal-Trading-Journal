using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DirectPlaybookDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CurrentPlaybooks",
                columns: table => new
                {
                    TradingSetupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    LegacyRevision = table.Column<int>(type: "INTEGER", nullable: true),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentPlaybooks", x => x.TradingSetupId);
                    table.CheckConstraint("CK_CurrentPlaybooks_Content", "Version > 0 AND ((LegacyRevision IS NOT NULL AND LegacyRevision = Version AND DefinitionJson IS NULL) OR (LegacyRevision IS NULL AND DefinitionJson IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_CurrentPlaybooks_PlaybookRevisions_TradingSetupId_LegacyRevision",
                        columns: x => new { x.TradingSetupId, x.LegacyRevision },
                        principalTable: "PlaybookRevisions",
                        principalColumns: new[] { "TradingSetupId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrentPlaybooks_TradingSetups_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalTable: "TradingSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurrentPlaybookExamples",
                columns: table => new
                {
                    TradingSetupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExampleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceScreenshotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ImageStorageKey = table.Column<string>(type: "TEXT", nullable: true),
                    ImageSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ImageSha256 = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentPlaybookExamples", x => new { x.TradingSetupId, x.ExampleId });
                    table.ForeignKey(
                        name: "FK_CurrentPlaybookExamples_CurrentPlaybooks_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalTable: "CurrentPlaybooks",
                        principalColumn: "TradingSetupId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurrentPlaybookExamples_TradeScreenshots_SourceScreenshotId",
                        column: x => x.SourceScreenshotId,
                        principalTable: "TradeScreenshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrentPlaybookExamples_SourceScreenshotId",
                table: "CurrentPlaybookExamples",
                column: "SourceScreenshotId");

            migrationBuilder.CreateIndex(
                name: "IX_CurrentPlaybooks_TradingSetupId_LegacyRevision",
                table: "CurrentPlaybooks",
                columns: new[] { "TradingSetupId", "LegacyRevision" });
            // GetLatestAsync historically used the highest retained revision. Preserve that exact
            // authoritative definition; do not guess from names, timestamps or captured Trade text.
            migrationBuilder.Sql("""
                INSERT INTO CurrentPlaybooks (TradingSetupId, Version, LegacyRevision, DefinitionJson)
                SELECT TradingSetupId, MAX(Revision), MAX(Revision), NULL FROM PlaybookRevisions GROUP BY TradingSetupId;
                INSERT INTO CurrentPlaybookExamples (TradingSetupId, ExampleId, SourceScreenshotId, ImageStorageKey, ImageSizeBytes, ImageSha256)
                SELECT e.TradingSetupId, e.ExampleId, e.SourceScreenshotId, e.ImageStorageKey, e.ImageSizeBytes, e.ImageSha256
                FROM PlaybookExamples e JOIN CurrentPlaybooks c ON c.TradingSetupId=e.TradingSetupId AND c.LegacyRevision=e.Revision;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A direct edit has no equivalent immutable authoring history. Refuse lossy downgrade.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE DirectPlaybookDowngradeGuard (Count INTEGER CHECK (Count = 0));
                INSERT INTO DirectPlaybookDowngradeGuard SELECT (SELECT COUNT(*) FROM CurrentPlaybooks WHERE DefinitionJson IS NOT NULL) + (SELECT COUNT(*) FROM TradingPlans);
                DROP TABLE DirectPlaybookDowngradeGuard;
                """);
            migrationBuilder.DropTable(
                name: "CurrentPlaybookExamples");

            migrationBuilder.DropTable(
                name: "CurrentPlaybooks");
        }
    }
}
