using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotebook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotebookFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NameKey = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookFolders", x => x.Id);
                    table.CheckConstraint("CK_NotebookFolders_Metadata", "Version > 0 AND length(trim(Name)) BETWEEN 1 AND 128 AND UpdatedAtUtc >= CreatedAtUtc");
                });

            migrationBuilder.CreateTable(
                name: "NotebookTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NameKey = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookTags", x => x.Id);
                    table.CheckConstraint("CK_NotebookTags_Metadata", "Version > 0 AND length(trim(Name)) BETWEEN 1 AND 64 AND UpdatedAtUtc >= CreatedAtUtc");
                });

            migrationBuilder.CreateTable(
                name: "NotebookNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    TitleKey = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    DocumentJson = table.Column<string>(type: "TEXT", nullable: false),
                    ContentVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PlainText = table.Column<string>(type: "TEXT", nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreviousState = table.Column<int>(type: "INTEGER", nullable: true),
                    PreviousFolderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookNotes", x => x.Id);
                    table.CheckConstraint("CK_NotebookNotes_Metadata", "Version > 0 AND length(trim(Title)) BETWEEN 1 AND 200 AND State IN (1,2,3) AND UpdatedAtUtc >= CreatedAtUtc");
                    table.CheckConstraint("CK_NotebookNotes_Restore", "(State = 3 AND PreviousState IN (1,2) AND PreviousState IS NOT NULL AND PreviousFolderId IS FolderId) OR (State <> 3 AND PreviousState IS NULL AND PreviousFolderId IS NULL)");
                    table.ForeignKey(
                        name: "FK_NotebookNotes_NotebookFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "NotebookFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotebookNotes_NotebookFolders_PreviousFolderId",
                        column: x => x.PreviousFolderId,
                        principalTable: "NotebookFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotebookImages",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Ownership = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    TradeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TradeScreenshotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Caption = table.Column<string>(type: "TEXT", nullable: false),
                    AlternativeText = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookImages", x => new { x.NoteId, x.Id });
                    table.CheckConstraint("CK_NotebookImages_Ownership", "(Ownership = 1 AND StorageKey IS NOT NULL AND SizeBytes BETWEEN 1 AND 8388608 AND SizeBytes IS NOT NULL AND length(Sha256) = 64 AND Sha256 IS NOT NULL AND TradeId IS NULL AND TradeScreenshotId IS NULL) OR (Ownership = 2 AND StorageKey IS NULL AND SizeBytes IS NULL AND Sha256 IS NULL AND TradeId IS NOT NULL AND TradeScreenshotId IS NOT NULL)");
                    table.CheckConstraint("CK_NotebookImages_Position", "Position BETWEEN 0 AND 31 AND length(Caption) <= 2000 AND length(AlternativeText) <= 500");
                    table.ForeignKey(
                        name: "FK_NotebookImages_NotebookNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "NotebookNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotebookLinks",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastKnownLabel = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookLinks", x => new { x.NoteId, x.Kind, x.TargetId });
                    table.CheckConstraint("CK_NotebookLinks_Kind", "Kind IN (1,2) AND length(LastKnownLabel) BETWEEN 1 AND 2000");
                    table.ForeignKey(
                        name: "FK_NotebookLinks_NotebookNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "NotebookNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotebookTagAssignments",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookTagAssignments", x => new { x.NoteId, x.TagId });
                    table.ForeignKey(
                        name: "FK_NotebookTagAssignments_NotebookNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "NotebookNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotebookTagAssignments_NotebookTags_TagId",
                        column: x => x.TagId,
                        principalTable: "NotebookTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookFolders_NameKey",
                table: "NotebookFolders",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotebookImages_NoteId_Position",
                table: "NotebookImages",
                columns: new[] { "NoteId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotebookImages_StorageKey",
                table: "NotebookImages",
                column: "StorageKey",
                unique: true,
                filter: "StorageKey IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookNotes_FolderId_State",
                table: "NotebookNotes",
                columns: new[] { "FolderId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookNotes_PreviousFolderId",
                table: "NotebookNotes",
                column: "PreviousFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookNotes_State_TitleKey_Id",
                table: "NotebookNotes",
                columns: new[] { "State", "TitleKey", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookNotes_State_UpdatedAtUtc_Id",
                table: "NotebookNotes",
                columns: new[] { "State", "UpdatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookTagAssignments_TagId",
                table: "NotebookTagAssignments",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookTags_NameKey",
                table: "NotebookTags",
                column: "NameKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotebookImages");

            migrationBuilder.DropTable(
                name: "NotebookLinks");

            migrationBuilder.DropTable(
                name: "NotebookTagAssignments");

            migrationBuilder.DropTable(
                name: "NotebookNotes");

            migrationBuilder.DropTable(
                name: "NotebookTags");

            migrationBuilder.DropTable(
                name: "NotebookFolders");
        }
    }
}
