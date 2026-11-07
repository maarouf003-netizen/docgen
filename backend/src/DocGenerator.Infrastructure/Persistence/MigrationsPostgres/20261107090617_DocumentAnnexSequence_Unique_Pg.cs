using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class DocumentAnnexSequence_Unique_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentAnnexes_DocumentId_Sequence",
                table: "DocumentAnnexes");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAnnexes_DocumentId_Sequence",
                table: "DocumentAnnexes",
                columns: new[] { "DocumentId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentAnnexes_DocumentId_Sequence",
                table: "DocumentAnnexes");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAnnexes_DocumentId_Sequence",
                table: "DocumentAnnexes",
                columns: new[] { "DocumentId", "Sequence" });
        }
    }
}
