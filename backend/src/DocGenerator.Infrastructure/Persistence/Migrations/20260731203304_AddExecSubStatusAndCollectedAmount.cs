using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecSubStatusAndCollectedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CollectedAmount",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecSubStatus",
                table: "Documents",
                type: "TEXT",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectedAmount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecSubStatus",
                table: "Documents");
        }
    }
}
