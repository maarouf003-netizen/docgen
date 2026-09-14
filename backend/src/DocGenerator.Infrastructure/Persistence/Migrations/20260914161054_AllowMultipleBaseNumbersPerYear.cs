using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleBaseNumbersPerYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_AppealBaseNumbers_AppealId_Year",
                table: "AppealBaseNumbers");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year",
                table: "DocumentBaseNumbers",
                columns: new[] { "DocumentId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealBaseNumbers_AppealId_Year",
                table: "AppealBaseNumbers",
                columns: new[] { "AppealId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // إعادة القيد الفريد تفشل (ArgumentException/فهرس) طالما وُجدت صفوف مكررة
            // لنفس (ملف، سنة) أو (استئناف، سنة) — يُطبَّق Down فقط على قاعدة بلا تكرار.
            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_AppealBaseNumbers_AppealId_Year",
                table: "AppealBaseNumbers");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year",
                table: "DocumentBaseNumbers",
                columns: new[] { "DocumentId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealBaseNumbers_AppealId_Year",
                table: "AppealBaseNumbers",
                columns: new[] { "AppealId", "Year" },
                unique: true);
        }
    }
}
