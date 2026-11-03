using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class HeadAlertOutbox_ConflictRetries_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConflictRetries",
                table: "HeadAlertOutboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // صفوف سبقت الهجرة وقد استهلكت إعادتها الواحدة (العلامة في `LastError`):
            // تُثبَّت الحالة في العمود الجديد حتى لا تُمنح إعادة ثانية بعد النشر.
            migrationBuilder.Sql(
                "UPDATE \"HeadAlertOutboxMessages\" SET \"ConflictRetries\" = 1 " +
                "WHERE \"LastError\" = 'conflict_retry';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HeadAlertOutbox_ConflictRetries",
                table: "HeadAlertOutboxMessages",
                sql: "\"ConflictRetries\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_HeadAlertOutbox_ConflictRetries",
                table: "HeadAlertOutboxMessages");

            migrationBuilder.DropColumn(
                name: "ConflictRetries",
                table: "HeadAlertOutboxMessages");
        }
    }
}
