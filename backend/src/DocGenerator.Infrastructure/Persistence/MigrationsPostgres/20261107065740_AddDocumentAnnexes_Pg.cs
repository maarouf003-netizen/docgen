using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddDocumentAnnexes_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentAnnexes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    AnnexType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnexNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnexDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                WHERE COALESCE(BTRIM("AnnexType"), '') <> ''
                   OR COALESCE(BTRIM("AnnexNumber"), '') <> ''
                   OR COALESCE(BTRIM("AnnexDate"), '') <> '';
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
