using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybookHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybookRevisions",
                columns: table => new
                {
                    TradingSetupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    ContractVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SetupName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SetupDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MarketContext = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    EntryConditions = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Confirmations = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Invalidation = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ExecutionGuidance = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookRevisions", x => new { x.TradingSetupId, x.Revision });
                    table.CheckConstraint("CK_PlaybookRevisions_Version", "ContractVersion = 1 AND Revision >= 1");
                    table.ForeignKey(
                        name: "FK_PlaybookRevisions_TradingSetups_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalTable: "TradingSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlaybookExamples",
                columns: table => new
                {
                    TradingSetupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    ExampleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Annotation = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    SourceTradeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceScreenshotId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookExamples", x => new { x.TradingSetupId, x.Revision, x.ExampleId });
                    table.CheckConstraint("CK_PlaybookExamples_Position", "Position >= 0 AND Position < 12");
                    table.CheckConstraint("CK_PlaybookExamples_Source", "SourceScreenshotId IS NULL OR SourceTradeId IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_PlaybookExamples_PlaybookRevisions_TradingSetupId_Revision",
                        columns: x => new { x.TradingSetupId, x.Revision },
                        principalTable: "PlaybookRevisions",
                        principalColumns: new[] { "TradingSetupId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlaybookExamples_TradeScreenshots_SourceScreenshotId",
                        column: x => x.SourceScreenshotId,
                        principalTable: "TradeScreenshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradePlaybookContexts",
                columns: table => new
                {
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    TradingSetupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    TradeOpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradePlaybookContexts", x => x.TradeId);
                    table.CheckConstraint("CK_TradePlaybookContexts_Version", "ContractVersion = 1");
                    table.ForeignKey(
                        name: "FK_TradePlaybookContexts_PlaybookRevisions_TradingSetupId_Revision",
                        columns: x => new { x.TradingSetupId, x.Revision },
                        principalTable: "PlaybookRevisions",
                        principalColumns: new[] { "TradingSetupId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradePlaybookContexts_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookExamples_SourceScreenshotId",
                table: "PlaybookExamples",
                column: "SourceScreenshotId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookExamples_TradingSetupId_Revision_Position",
                table: "PlaybookExamples",
                columns: new[] { "TradingSetupId", "Revision", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradePlaybookContexts_TradingSetupId_Revision",
                table: "TradePlaybookContexts",
                columns: new[] { "TradingSetupId", "Revision" });

            // Immutable revisions cannot be erased and recreated with reused numbers.
            foreach (string table in new[] { "PlaybookRevisions", "PlaybookExamples" })
            {
                migrationBuilder.Sql($"CREATE TRIGGER {table}_NoUpdate BEFORE UPDATE ON {table} BEGIN SELECT RAISE(ABORT, 'Playbook history is immutable'); END;");
                migrationBuilder.Sql($"CREATE TRIGGER {table}_NoDelete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT, 'Playbook history is retained'); END;");
            }
            migrationBuilder.Sql("CREATE TRIGGER TradePlaybookContexts_NoUpdate BEFORE UPDATE ON TradePlaybookContexts BEGIN SELECT RAISE(ABORT, 'Trade Playbook context is immutable'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybookExamples");

            migrationBuilder.DropTable(
                name: "TradePlaybookContexts");

            migrationBuilder.DropTable(
                name: "PlaybookRevisions");
        }
    }
}
