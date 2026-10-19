using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedTradingPlansAndScreenshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradingPlans_AccountId_NewYorkDate",
                table: "TradingPlans");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "TradingPlans",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.CreateTable(
                name: "TradingPlanAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timeframe = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPlanAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingPlanAssets_TradingPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "TradingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlans_AccountId_NewYorkDate",
                table: "TradingPlans",
                columns: new[] { "AccountId", "NewYorkDate" },
                unique: true,
                filter: "AccountId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlans_NewYorkDate",
                table: "TradingPlans",
                column: "NewYorkDate",
                unique: true,
                filter: "AccountId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlanAssets_PlanId",
                table: "TradingPlanAssets",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlanAssets_StorageKey",
                table: "TradingPlanAssets",
                column: "StorageKey",
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER TradingPlanAssets_NoUpdate BEFORE UPDATE ON TradingPlanAssets
                BEGIN SELECT RAISE(ABORT, 'Plan assets are immutable'); END;
                CREATE TRIGGER TradingPlanAssets_NoDelete BEFORE DELETE ON TradingPlanAssets
                BEGIN SELECT RAISE(ABORT, 'Plan assets are retained'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty-schema migration round trips remain supported. Never downgrade stored v2
            // preparation or invent an Account for shared scope; check before any live change.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE PlanDowngradeGuard (Count INTEGER CHECK (Count = 0));
                INSERT INTO PlanDowngradeGuard SELECT
                    (SELECT COUNT(*) FROM TradingPlans) + (SELECT COUNT(*) FROM TradingPlanAssets);
                DROP TABLE PlanDowngradeGuard;
                """);
            migrationBuilder.DropTable(name: "TradingPlanAssets");
            migrationBuilder.DropIndex(name: "IX_TradingPlans_AccountId_NewYorkDate", table: "TradingPlans");
            migrationBuilder.DropIndex(name: "IX_TradingPlans_NewYorkDate", table: "TradingPlans");
            migrationBuilder.AlterColumn<Guid>(name: "AccountId", table: "TradingPlans", type: "TEXT",
                nullable: false, oldClrType: typeof(Guid), oldType: "TEXT", oldNullable: true);
            migrationBuilder.CreateIndex(name: "IX_TradingPlans_AccountId_NewYorkDate", table: "TradingPlans",
                columns: new[] { "AccountId", "NewYorkDate" }, unique: true);
        }
    }
}
