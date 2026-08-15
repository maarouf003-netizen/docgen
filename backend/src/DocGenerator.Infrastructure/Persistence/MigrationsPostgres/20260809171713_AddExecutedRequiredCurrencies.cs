using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddExecutedRequiredCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExecutedRequiredAmount2",
                table: "Documents",
                type: "numeric(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExecutedRequiredAmount3",
                table: "Documents",
                type: "numeric(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedRequiredCurrency",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedRequiredCurrency2",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedRequiredCurrency3",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutedRequiredAmount2",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedRequiredAmount3",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedRequiredCurrency",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedRequiredCurrency2",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedRequiredCurrency3",
                table: "Documents");
        }
    }
}
