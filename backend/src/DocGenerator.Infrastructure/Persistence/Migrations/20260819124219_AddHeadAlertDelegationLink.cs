using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadAlertDelegationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DelegationId",
                table: "HeadAlerts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_DelegationId",
                table: "HeadAlerts",
                column: "DelegationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_DelegationId",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "DelegationId",
                table: "HeadAlerts");
        }
    }
}
