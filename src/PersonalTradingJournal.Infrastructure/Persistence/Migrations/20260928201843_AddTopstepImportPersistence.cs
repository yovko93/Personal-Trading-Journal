using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTopstepImportPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TopstepImportedRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradingAccountIdAtImport = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    EconomicFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceRowJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreviewFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceContentSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Representation = table.Column<string>(type: "TEXT", nullable: false),
                    DerivedEntryExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DerivedExitExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopstepImportedRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopstepImportedRows_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TopstepImportedRows_TradeId",
                table: "TopstepImportedRows",
                column: "TradeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopstepImportedRows_TradingAccountIdAtImport_SourceId",
                table: "TopstepImportedRows",
                columns: new[] { "TradingAccountIdAtImport", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TopstepImportedRows");
        }
    }
}
