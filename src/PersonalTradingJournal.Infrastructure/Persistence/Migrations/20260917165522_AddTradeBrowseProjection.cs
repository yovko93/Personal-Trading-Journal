using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeBrowseProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeBrowse",
                columns: table => new
                {
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectionVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenQuantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    OpenQuantitySortKey = table.Column<string>(type: "TEXT", maxLength: 58, nullable: false, collation: "BINARY"),
                    AverageEntryPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    AverageEntryPriceSortKey = table.Column<string>(type: "TEXT", maxLength: 58, nullable: false, collation: "BINARY"),
                    AverageExitPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    TotalCosts = table.Column<decimal>(type: "TEXT", nullable: false),
                    GrossPnL = table.Column<decimal>(type: "TEXT", nullable: true),
                    NetPnL = table.Column<decimal>(type: "TEXT", nullable: true),
                    NetPnLSortKey = table.Column<string>(type: "TEXT", maxLength: 58, nullable: true, collation: "BINARY")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeBrowse", x => x.TradeId);
                    table.ForeignKey(
                        name: "FK_TradeBrowse_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeBrowse_AverageEntryPriceSortKey",
                table: "TradeBrowse",
                column: "AverageEntryPriceSortKey");

            migrationBuilder.CreateIndex(
                name: "IX_TradeBrowse_NetPnLSortKey",
                table: "TradeBrowse",
                column: "NetPnLSortKey");

            migrationBuilder.CreateIndex(
                name: "IX_TradeBrowse_OpenedAtUtc",
                table: "TradeBrowse",
                column: "OpenedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TradeBrowse_OpenQuantitySortKey",
                table: "TradeBrowse",
                column: "OpenQuantitySortKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeBrowse");
        }
    }
}
