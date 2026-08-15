using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantEntitiesAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileArrivalDate",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileArrivalNumber",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicantPublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantPublicEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicantPublicEntities_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LawyerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssignedByName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAssignments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantPublicEntities_DocumentId",
                table: "ApplicantPublicEntities",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAssignments_DocumentId",
                table: "DocumentAssignments",
                column: "DocumentId");

            // ترحيل «طالب التنفيذ» النصي القديم إلى قائمة الجهات طالبة التنفيذ:
            // كل ملف يحمل Applicant نصيًا غير فارغ يُنشأ له صف واحد (الاسم = النص، بلا فرع).
            migrationBuilder.Sql("""
                INSERT INTO "ApplicantPublicEntities" ("DocumentId", "Name", "Branch")
                SELECT "Id", "Applicant", NULL
                FROM "Documents"
                WHERE "Applicant" IS NOT NULL AND trim("Applicant") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantPublicEntities");

            migrationBuilder.DropTable(
                name: "DocumentAssignments");

            migrationBuilder.DropColumn(
                name: "FileArrivalDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileArrivalNumber",
                table: "Documents");
        }
    }
}
