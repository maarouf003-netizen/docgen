using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class PB001_NormalizationPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalNameNorm",
                table: "PublicEntityGroups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourtNorm",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DelegatedCourtNorm",
                table: "DocumentDelegations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityGroups_CanonicalNameNorm",
                table: "PublicEntityGroups",
                column: "CanonicalNameNorm",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CourtNorm",
                table: "Documents",
                column: "CourtNorm");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CourtNorm_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "CourtNorm", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL AND \"CourtNorm\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicEntityGroups_CanonicalNameNorm",
                table: "PublicEntityGroups");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CourtNorm",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CourtNorm_FileNumber_FileType_FileYear",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CanonicalNameNorm",
                table: "PublicEntityGroups");

            migrationBuilder.DropColumn(
                name: "CourtNorm",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DelegatedCourtNorm",
                table: "DocumentDelegations");
        }
    }
}
