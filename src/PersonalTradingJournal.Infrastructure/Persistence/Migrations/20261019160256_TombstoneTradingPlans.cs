using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TombstoneTradingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradingPlans_AccountId_NewYorkDate",
                table: "TradingPlans");

            migrationBuilder.DropIndex(
                name: "IX_TradingPlans_NewYorkDate",
                table: "TradingPlans");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "TradingPlans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlans_AccountId_NewYorkDate",
                table: "TradingPlans",
                columns: new[] { "AccountId", "NewYorkDate" },
                unique: true,
                filter: "AccountId IS NOT NULL AND DeletedAtUtc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPlans_NewYorkDate",
                table: "TradingPlans",
                column: "NewYorkDate",
                unique: true,
                filter: "AccountId IS NULL AND DeletedAtUtc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never partially downgrade an occupied Plan schema or resurrect tombstones.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE PlanDeletionDowngradeGuard (Count INTEGER CHECK (Count = 0));
                INSERT INTO PlanDeletionDowngradeGuard SELECT COUNT(*) FROM TradingPlans;
                DROP TABLE PlanDeletionDowngradeGuard;
                """);
            migrationBuilder.DropIndex(
                name: "IX_TradingPlans_AccountId_NewYorkDate",
                table: "TradingPlans");

            migrationBuilder.DropIndex(
                name: "IX_TradingPlans_NewYorkDate",
                table: "TradingPlans");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "TradingPlans");

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
        }
    }
}
