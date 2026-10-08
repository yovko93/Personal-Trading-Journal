using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyJournals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyJournals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TradingDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TradingAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", maxLength: 100000, nullable: false),
                    IsDraft = table.Column<bool>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyJournals", x => x.Id);
                    table.CheckConstraint("CK_DailyJournals_Revision", "\"Revision\" >= 1");
                    table.ForeignKey(
                        name: "FK_DailyJournals_TradingAccounts_TradingAccountId",
                        column: x => x.TradingAccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyJournalRevisions",
                columns: table => new
                {
                    JournalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 100000, nullable: false),
                    IsDraft = table.Column<bool>(type: "INTEGER", nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyJournalRevisions", x => new { x.JournalId, x.Revision });
                    table.CheckConstraint("CK_DailyJournalRevisions_Revision", "\"Revision\" >= 1");
                    table.ForeignKey(
                        name: "FK_DailyJournalRevisions_DailyJournals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "DailyJournals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyJournals_TradingAccountId",
                table: "DailyJournals",
                column: "TradingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyJournals_TradingDate",
                table: "DailyJournals",
                column: "TradingDate",
                unique: true,
                filter: "\"TradingAccountId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyJournals_TradingDate_TradingAccountId",
                table: "DailyJournals",
                columns: new[] { "TradingDate", "TradingAccountId" },
                unique: true,
                filter: "\"TradingAccountId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyJournalRevisions");

            migrationBuilder.DropTable(
                name: "DailyJournals");
        }
    }
}
