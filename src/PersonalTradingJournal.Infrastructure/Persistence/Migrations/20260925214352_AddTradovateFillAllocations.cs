using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTradovateFillAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradovateImportedExecutions_TradingAccountIdAtImport_BrokerSymbol_Side_ExternalExecutionId",
                table: "TradovateImportedExecutions");

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedQuantity",
                table: "TradovateImportedExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllocationIndex",
                table: "TradovateImportedExecutions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceFillExecutedAtUtc",
                table: "TradovateImportedExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourceFillPrice",
                table: "TradovateImportedExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourceFillQuantity",
                table: "TradovateImportedExecutions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradovateImportedExecutions_TradingAccountIdAtImport_BrokerSymbol_Side_ExternalExecutionId_AllocationIndex",
                table: "TradovateImportedExecutions",
                columns: new[] { "TradingAccountIdAtImport", "BrokerSymbol", "Side", "ExternalExecutionId", "AllocationIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradovateImportedExecutions_TradingAccountIdAtImport_BrokerSymbol_Side_ExternalExecutionId_AllocationIndex",
                table: "TradovateImportedExecutions");

            migrationBuilder.DropColumn(
                name: "AllocatedQuantity",
                table: "TradovateImportedExecutions");

            migrationBuilder.DropColumn(
                name: "AllocationIndex",
                table: "TradovateImportedExecutions");

            migrationBuilder.DropColumn(
                name: "SourceFillExecutedAtUtc",
                table: "TradovateImportedExecutions");

            migrationBuilder.DropColumn(
                name: "SourceFillPrice",
                table: "TradovateImportedExecutions");

            migrationBuilder.DropColumn(
                name: "SourceFillQuantity",
                table: "TradovateImportedExecutions");

            migrationBuilder.CreateIndex(
                name: "IX_TradovateImportedExecutions_TradingAccountIdAtImport_BrokerSymbol_Side_ExternalExecutionId",
                table: "TradovateImportedExecutions",
                columns: new[] { "TradingAccountIdAtImport", "BrokerSymbol", "Side", "ExternalExecutionId" },
                unique: true);
        }
    }
}
