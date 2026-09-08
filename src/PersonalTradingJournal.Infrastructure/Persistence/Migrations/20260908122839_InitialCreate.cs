using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AssetClass = table.Column<int>(type: "INTEGER", nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    TickSize = table.Column<decimal>(type: "TEXT", nullable: false),
                    TickValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AccountType = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExternalAccountId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    StartingBalance = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingMistakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingMistakes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingSetups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingSetups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PricingPointValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    PricingCurrency = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    StrategyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TradingSetupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_TradingAccounts_TradingAccountId",
                        column: x => x.TradingAccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_TradingSetups_TradingSetupId",
                        column: x => x.TradingSetupId,
                        principalTable: "TradingSetups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Commission = table.Column<decimal>(type: "TEXT", nullable: false),
                    Fees = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExternalExecutionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExternalOrderId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    BrokerSymbol = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeExecutions_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeMistakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradingMistakeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeMistakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeMistakes_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeMistakes_TradingMistakes_TradingMistakeId",
                        column: x => x.TradingMistakeId,
                        principalTable: "TradingMistakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeScreenshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Timeframe = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeScreenshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeScreenshots_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_TradeId_Sequence",
                table: "TradeExecutions",
                columns: new[] { "TradeId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeMistakes_TradeId_TradingMistakeId",
                table: "TradeMistakes",
                columns: new[] { "TradeId", "TradingMistakeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeMistakes_TradingMistakeId",
                table: "TradeMistakes",
                column: "TradingMistakeId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_InstrumentId",
                table: "Trades",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_StrategyId",
                table: "Trades",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_TradingAccountId",
                table: "Trades",
                column: "TradingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_TradingSetupId",
                table: "Trades",
                column: "TradingSetupId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeScreenshots_TradeId",
                table: "TradeScreenshots",
                column: "TradeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeExecutions");

            migrationBuilder.DropTable(
                name: "TradeMistakes");

            migrationBuilder.DropTable(
                name: "TradeScreenshots");

            migrationBuilder.DropTable(
                name: "TradingMistakes");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "Strategies");

            migrationBuilder.DropTable(
                name: "TradingAccounts");

            migrationBuilder.DropTable(
                name: "TradingSetups");
        }
    }
}
