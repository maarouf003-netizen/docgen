using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class PB002_StatusChecksPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecStatus",
                table: "Documents",
                sql: "\"ExecStatus\" IN ('', 'منفذ جبريا', 'منفذ بالتسوية', 'تريث', 'منفذ إنابة', 'مسترد', 'محال الى البداية', 'مشطوب')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecSubStatus",
                table: "Documents",
                sql: "\"ExecSubStatus\" IN ('منفذ جزئيا', 'منفذ كاملا')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecutedStatus",
                table: "Documents",
                sql: "\"ExecutedStatus\" IN ('', 'منفذ', 'مشطوب')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_GeneralEntitySide",
                table: "Documents",
                sql: "\"GeneralEntitySide\" IN ('applicant', 'executed', 'deposit')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentDelegations_Status",
                table: "DocumentDelegations",
                sql: "\"Status\" IN ('بانتظار رئيس القسم', 'محالة', 'مسجلة أصولًا', 'منفذ إنابة')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentAppeals_Status",
                table: "DocumentAppeals",
                sql: "\"Status\" IN ('pending', 'decided', 'struck-off')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecStatus",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecSubStatus",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecutedStatus",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_GeneralEntitySide",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentDelegations_Status",
                table: "DocumentDelegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentAppeals_Status",
                table: "DocumentAppeals");
        }
    }
}
