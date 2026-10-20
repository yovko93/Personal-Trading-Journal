using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotebookRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotebookRecoveryDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvelopeVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedVersion = table.Column<long>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentJson = table.Column<string>(type: "TEXT", nullable: false),
                    CheckpointedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookRecoveryDrafts", x => x.Id);
                    table.CheckConstraint("CK_NotebookRecovery_Metadata", "EnvelopeVersion > 0 AND ExpectedVersion > 0 AND Revision > 0 AND length(Title) <= 200");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookRecoveryDrafts_NoteId",
                table: "NotebookRecoveryDrafts",
                column: "NoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotebookRecoveryDrafts");
        }
    }
}
