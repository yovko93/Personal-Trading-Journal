using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingAnalysisSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoachingAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ScopeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AccountDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EvidenceContractVersion = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseContractVersion = table.Column<string>(type: "TEXT", nullable: false),
                    PacketId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EvidenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseJson = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachingAnalyses", x => x.Id);
                    table.CheckConstraint("CK_CoachingAnalyses_Scope", "(ScopeKind = 0 AND AccountId IS NULL) OR (ScopeKind = 1 AND AccountId IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachingAnalyses_ReviewDate_ScopeKind_AccountId_GeneratedAtUtc_Id",
                table: "CoachingAnalyses",
                columns: new[] { "ReviewDate", "ScopeKind", "AccountId", "GeneratedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachingAnalyses");
        }
    }
}
