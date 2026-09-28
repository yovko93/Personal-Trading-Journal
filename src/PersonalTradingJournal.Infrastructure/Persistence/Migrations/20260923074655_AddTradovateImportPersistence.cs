using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradovateImportPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Fees",
                table: "TradeExecutions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "Commission",
                table: "TradeExecutions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCosts",
                table: "TradeBrowse",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.CreateTable(
                name: "TradovateImportedExecutions",
                columns: table => new
                {
                    TradeExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradingAccountIdAtImport = table.Column<Guid>(type: "TEXT", nullable: false),
                    BrokerSymbol = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalExecutionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradovateImportedExecutions", x => x.TradeExecutionId);
                    table.ForeignKey(
                        name: "FK_TradovateImportedExecutions_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradovateImportedExecutions_TradeId",
                table: "TradovateImportedExecutions",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_TradovateImportedExecutions_TradingAccountIdAtImport_BrokerSymbol_Side_ExternalExecutionId",
                table: "TradovateImportedExecutions",
                columns: new[] { "TradingAccountIdAtImport", "BrokerSymbol", "Side", "ExternalExecutionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradovateImportedExecutions");

            migrationBuilder.AlterColumn<decimal>(
                name: "Fees",
                table: "TradeExecutions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Commission",
                table: "TradeExecutions",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalCosts",
                table: "TradeBrowse",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
