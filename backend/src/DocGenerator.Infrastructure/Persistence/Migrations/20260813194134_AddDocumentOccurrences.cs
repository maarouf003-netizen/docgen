using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurrenceType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FileNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CreatedById",
                table: "DocumentOccurrences",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_DocumentId",
                table: "DocumentOccurrences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_EventDate",
                table: "DocumentOccurrences",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_OccurrenceType",
                table: "DocumentOccurrences",
                column: "OccurrenceType");

            // ترحيل بيانات «منفذ عليه» القائمة إلى سجل الوقوعات:
            // 1) كل ملف يحمل تاريخ شطب → وقعة شطب (تاريخ الشطب + الرقم الأصلي + سنة الشطب).
            // 2) كل ملف يحمل بيان تجديد → وقعة تجديد (الرقم الجديد + النوع + سنة التجديد + ورود الاخطار).
            migrationBuilder.Sql("""
                INSERT INTO "DocumentOccurrences" ("DocumentId", "OccurrenceType", "EventDate", "FileNumber", "FileType", "Year", "ReceiptNumber", "ReceiptDate", "CreatedById", "CreatedAt", "UpdatedAt")
                SELECT "Id", 'struck-off', "StruckOffDate", "FileNumber", NULL, CAST(strftime('%Y', "StruckOffDate") AS INTEGER), NULL, NULL, "CreatedById", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM "Documents"
                WHERE "StruckOffDate" IS NOT NULL;

                INSERT INTO "DocumentOccurrences" ("DocumentId", "OccurrenceType", "EventDate", "FileNumber", "FileType", "Year", "ReceiptNumber", "ReceiptDate", "CreatedById", "CreatedAt", "UpdatedAt")
                SELECT "Id", 'renewal', "RenewalDate", "RenewalFileNumber", "RenewalFileType", CAST(strftime('%Y', "RenewalDate") AS INTEGER), "RenewalFileReceiptNumber", "RenewalFileReceiptDate", "CreatedById", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM "Documents"
                WHERE "RenewalFileNumber" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOccurrences");
        }
    }
}
