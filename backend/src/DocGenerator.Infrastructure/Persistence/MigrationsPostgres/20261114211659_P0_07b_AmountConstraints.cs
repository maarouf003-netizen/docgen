using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class P0_07b_AmountConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_Amount2Numeric_NonNegative",
                table: "Documents",
                sql: "\"Amount2Numeric\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_Amount3Numeric_NonNegative",
                table: "Documents",
                sql: "\"Amount3Numeric\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_AmountNumeric_NonNegative",
                table: "Documents",
                sql: "\"AmountNumeric\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_CollectedAmount_NonNegative",
                table: "Documents",
                sql: "\"CollectedAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_CollectedAmount2_NonNegative",
                table: "Documents",
                sql: "\"CollectedAmount2\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_CollectedAmount3_NonNegative",
                table: "Documents",
                sql: "\"CollectedAmount3\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecutedPaidAmount_NonNegative",
                table: "Documents",
                sql: "\"ExecutedPaidAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecutedPaidAmount2_NonNegative",
                table: "Documents",
                sql: "\"ExecutedPaidAmount2\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecutedPaidAmount3_NonNegative",
                table: "Documents",
                sql: "\"ExecutedPaidAmount3\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecutedRequiredAmount_NonNegative",
                table: "Documents",
                sql: "\"ExecutedRequiredAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecutedRequiredAmount2_NonNegative",
                table: "Documents",
                sql: "\"ExecutedRequiredAmount2\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_ExecutedRequiredAmount3_NonNegative",
                table: "Documents",
                sql: "\"ExecutedRequiredAmount3\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_InclusionAmount2Numeric_NonNegative",
                table: "Documents",
                sql: "\"InclusionAmount2Numeric\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_InclusionAmount3Numeric_NonNegative",
                table: "Documents",
                sql: "\"InclusionAmount3Numeric\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_InclusionAmountNumeric_NonNegative",
                table: "Documents",
                sql: "\"InclusionAmountNumeric\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_Amount2Numeric_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_Amount3Numeric_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_AmountNumeric_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_CollectedAmount_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_CollectedAmount2_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_CollectedAmount3_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecutedPaidAmount_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecutedPaidAmount2_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecutedPaidAmount3_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecutedRequiredAmount_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecutedRequiredAmount2_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_ExecutedRequiredAmount3_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_InclusionAmount2Numeric_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_InclusionAmount3Numeric_NonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_InclusionAmountNumeric_NonNegative",
                table: "Documents");
        }
    }
}
