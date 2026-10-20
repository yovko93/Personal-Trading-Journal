using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NewYorkDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    LatestReadyRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    DraftJson = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPlans", x => x.Id);
                    table.CheckConstraint("CK_TradingPlans_State", "Version > 0 AND LatestReadyRevision >= 0 AND ((DraftJson IS NOT NULL AND ReferenceStateJson IS NOT NULL) OR (DraftJson IS NULL AND ReferenceStateJson IS NULL AND LatestReadyRevision > 0))");
                });

            migrationBuilder.CreateTable(
                name: "TradingPlanReadyRevisions",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReadyAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPlanReadyRevisions", x => new { x.PlanId, x.Revision });
                    table.CheckConstraint("CK_TradingPlanReady_Revision", "Revision > 0");
                    table.ForeignKey(
                        name: "FK_TradingPlanReadyRevisions_TradingPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "TradingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlans_AccountId_NewYorkDate",
                table: "TradingPlans",
                columns: new[] { "AccountId", "NewYorkDate" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER TradingPlanReady_NoUpdate BEFORE UPDATE ON TradingPlanReadyRevisions
                BEGIN SELECT RAISE(ABORT, 'Ready revisions are immutable'); END;
                CREATE TRIGGER TradingPlanReady_NoDelete BEFORE DELETE ON TradingPlanReadyRevisions
                BEGIN SELECT RAISE(ABORT, 'Ready revisions are retained'); END;
                CREATE TRIGGER TradingPlanReady_Sequence BEFORE INSERT ON TradingPlanReadyRevisions
                WHEN NEW.Revision <> COALESCE((SELECT MAX(Revision) FROM TradingPlanReadyRevisions WHERE PlanId = NEW.PlanId), 0) + 1
                BEGIN SELECT RAISE(ABORT, 'Ready revisions must be consecutive'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingPlanReadyRevisions");

            migrationBuilder.DropTable(
                name: "TradingPlans");
        }
    }
}
