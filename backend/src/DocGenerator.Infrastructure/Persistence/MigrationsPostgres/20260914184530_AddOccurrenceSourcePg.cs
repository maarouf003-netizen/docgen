using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddOccurrenceSourcePg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "DocumentOccurrences",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "manual");

            // القرار 1: توسيم كل الوقوعات القائمة (ما قبل هذه الهجرة) على أنها نظامية —
            // أحداث حقيقية سجلها النظام، ولها قيمتها الأرشيفية ومِنعةُ التعديل ذاتها.
            migrationBuilder.Sql("UPDATE \"DocumentOccurrences\" SET \"Source\" = 'system'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "DocumentOccurrences");
        }
    }
}
