using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyTradingPrinciples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyTradingPrinciples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Position = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTradingPrinciples", x => x.Id);
                    table.CheckConstraint("CK_DailyTradingPrinciples_Content", "length(trim(Text)) > 0 AND length(Text) <= 500 AND Position > 0");
                });

            migrationBuilder.CreateTable(
                name: "DailyTradingPrincipleState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTradingPrincipleState", x => x.Id);
                    table.CheckConstraint("CK_DailyTradingPrincipleState_Singleton", "Id = 1 AND Version >= 0");
                });

            migrationBuilder.InsertData(
                table: "DailyTradingPrincipleState",
                columns: new[] { "Id", "Version" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_DailyTradingPrinciples_Position",
                table: "DailyTradingPrinciples",
                column: "Position",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE PrincipleDowngradeGuard (Count INTEGER CHECK (Count = 0));
                INSERT INTO PrincipleDowngradeGuard SELECT COUNT(*) FROM DailyTradingPrinciples;
                DROP TABLE PrincipleDowngradeGuard;
                """);
            migrationBuilder.DropTable(
                name: "DailyTradingPrinciples");

            migrationBuilder.DropTable(
                name: "DailyTradingPrincipleState");
        }
    }
}
