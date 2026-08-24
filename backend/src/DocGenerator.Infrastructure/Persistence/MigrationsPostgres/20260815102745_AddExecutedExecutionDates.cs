using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddExecutedExecutionDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // تصحيح تاريخي: العمود وُلّد سابقًا بنوع datetime2 الخاص بـ SQL Server/SQLite
            // وهو غير موجود في PostgreSQL، فكان يفشل تطبيق السلسلة كاملة من قاعدة فارغة.
            // التعديل آمن لأن هذه الهجرة لا يمكن أن تكون طُبّقت بنجاح على PostgreSQL أصيل قبل التصحيح.
            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutedExecutionDate",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForcedExecutionDate",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutedExecutionDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ForcedExecutionDate",
                table: "Documents");
        }
    }
}
