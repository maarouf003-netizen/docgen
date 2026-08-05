using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionActionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "ExecutionActions",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "action");

            migrationBuilder.Sql(
                "UPDATE \"ExecutionActions\" SET \"Type\" = 'action' WHERE \"Type\" IS NULL OR \"Type\" = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "ExecutionActions");
        }
    }
}
