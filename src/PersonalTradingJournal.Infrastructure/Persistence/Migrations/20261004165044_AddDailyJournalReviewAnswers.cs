using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyJournalReviewAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NeedsImprovement",
                table: "DailyJournals",
                type: "TEXT",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NextTradingDay",
                table: "DailyJournals",
                type: "TEXT",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WentWell",
                table: "DailyJournals",
                type: "TEXT",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NeedsImprovement",
                table: "DailyJournalRevisions",
                type: "TEXT",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NextTradingDay",
                table: "DailyJournalRevisions",
                type: "TEXT",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WentWell",
                table: "DailyJournalRevisions",
                type: "TEXT",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeedsImprovement",
                table: "DailyJournals");

            migrationBuilder.DropColumn(
                name: "NextTradingDay",
                table: "DailyJournals");

            migrationBuilder.DropColumn(
                name: "WentWell",
                table: "DailyJournals");

            migrationBuilder.DropColumn(
                name: "NeedsImprovement",
                table: "DailyJournalRevisions");

            migrationBuilder.DropColumn(
                name: "NextTradingDay",
                table: "DailyJournalRevisions");

            migrationBuilder.DropColumn(
                name: "WentWell",
                table: "DailyJournalRevisions");
        }
    }
}
