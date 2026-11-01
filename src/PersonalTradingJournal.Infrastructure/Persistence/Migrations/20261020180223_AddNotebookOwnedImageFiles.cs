using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotebookOwnedImageFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotebookOwnedImageFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookOwnedImageFiles", x => x.Id);
                    table.CheckConstraint("CK_NotebookOwnedImageFiles_Metadata", "SizeBytes BETWEEN 1 AND 8388608 AND length(Sha256) = 64 AND length(StorageKey) BETWEEN 1 AND 180");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookOwnedImageFiles_NoteId",
                table: "NotebookOwnedImageFiles",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookOwnedImageFiles_StorageKey",
                table: "NotebookOwnedImageFiles",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotebookOwnedImageFiles");
        }
    }
}
