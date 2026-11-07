using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAnnexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentAnnexes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    AnnexType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AnnexNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AnnexDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAnnexes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAnnexes_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAnnexes_DocumentId_Sequence",
                table: "DocumentAnnexes",
                columns: new[] { "DocumentId", "Sequence" });

            // ترحيل الملحق المفرد القديم إلى الجدول الفرعي (الصف الأول) مع إبقاء
            // الأعمدة القديمة قراءة فقط — لا حذف ولا فقدان للبيانات التاريخية.
            migrationBuilder.Sql("""
                INSERT INTO "DocumentAnnexes" ("DocumentId", "Sequence", "AnnexType", "AnnexNumber", "AnnexDate")
                SELECT "Id", 1, "AnnexType", "AnnexNumber", "AnnexDate"
                FROM "Documents"
                WHERE COALESCE(TRIM("AnnexType"), '') <> ''
                   OR COALESCE(TRIM("AnnexNumber"), '') <> ''
                   OR COALESCE(TRIM("AnnexDate"), '') <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentAnnexes");
        }
    }
}
