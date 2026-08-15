using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddThirdBankingAndInclusionAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount3Numeric",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Amount3Words",
                table: "Documents",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency3",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InclusionAmount2Numeric",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InclusionAmount2Words",
                table: "Documents",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InclusionAmount3Numeric",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InclusionAmount3Words",
                table: "Documents",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InclusionCurrency2",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InclusionCurrency3",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount3Numeric",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Amount3Words",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Currency3",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InclusionAmount2Numeric",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InclusionAmount2Words",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InclusionAmount3Numeric",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InclusionAmount3Words",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InclusionCurrency2",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "InclusionCurrency3",
                table: "Documents");
        }
    }
}
