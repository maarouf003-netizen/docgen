using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutedPaidCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExecutedPaidAmount2",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExecutedPaidAmount3",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedPaidCurrency",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedPaidCurrency2",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedPaidCurrency3",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutedPaidAmount2",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedPaidAmount3",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedPaidCurrency",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedPaidCurrency2",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedPaidCurrency3",
                table: "Documents");
        }
    }
}
