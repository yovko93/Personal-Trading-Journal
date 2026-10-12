using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybookExampleImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageFileName",
                table: "PlaybookExamples",
                type: "TEXT",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageSha256",
                table: "PlaybookExamples",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImageSizeBytes",
                table: "PlaybookExamples",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageStorageKey",
                table: "PlaybookExamples",
                type: "TEXT",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "PlaybookExamples",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookExamples_ImageStorageKey",
                table: "PlaybookExamples",
                column: "ImageStorageKey");

            // Additive guard: do not rebuild the immutable history table or touch old rows.
            migrationBuilder.Sql("""
                CREATE TRIGGER PlaybookExamples_ImageInsert BEFORE INSERT ON PlaybookExamples
                WHEN NEW.Kind NOT IN (0,1,2) OR NOT (
                    (NEW.ImageStorageKey IS NULL AND NEW.ImageFileName IS NULL AND NEW.ImageSizeBytes IS NULL AND NEW.ImageSha256 IS NULL)
                    OR (NEW.ImageStorageKey IS NOT NULL AND NEW.ImageFileName IS NOT NULL AND NEW.ImageSizeBytes IS NOT NULL
                        AND NEW.ImageSha256 IS NOT NULL AND NEW.Kind IN (1,2) AND NEW.SourceScreenshotId IS NULL
                        AND length(NEW.ImageStorageKey) BETWEEN 1 AND 180 AND length(NEW.ImageFileName) BETWEEN 1 AND 180
                        AND NEW.ImageSizeBytes BETWEEN 1 AND 8388608 AND length(NEW.ImageSha256) = 64
                        AND NEW.ImageSha256 NOT GLOB '*[^0-9A-Fa-f]*'))
                BEGIN SELECT RAISE(ABORT, 'Invalid Playbook image metadata'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS PlaybookExamples_ImageInsert;");
            migrationBuilder.DropIndex(
                name: "IX_PlaybookExamples_ImageStorageKey",
                table: "PlaybookExamples");

            migrationBuilder.DropColumn(
                name: "ImageFileName",
                table: "PlaybookExamples");

            migrationBuilder.DropColumn(
                name: "ImageSha256",
                table: "PlaybookExamples");

            migrationBuilder.DropColumn(
                name: "ImageSizeBytes",
                table: "PlaybookExamples");

            migrationBuilder.DropColumn(
                name: "ImageStorageKey",
                table: "PlaybookExamples");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PlaybookExamples");
        }
    }
}
