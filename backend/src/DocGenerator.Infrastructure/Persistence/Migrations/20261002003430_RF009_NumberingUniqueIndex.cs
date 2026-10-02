using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RF009_NumberingUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Documents_Court_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "Court", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_Court_FileNumber_FileType_FileYear",
                table: "Documents");
        }
    }
}
