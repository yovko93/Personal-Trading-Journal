using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountPayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPayouts", x => x.Id);
                    table.CheckConstraint("CK_AccountPayouts_Positive", "CAST(Amount AS REAL) > 0");
                    table.ForeignKey(
                        name: "FK_AccountPayouts_TradingAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "TradingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPayouts_AccountId",
                table: "AccountPayouts",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPayouts");
        }
    }
}
