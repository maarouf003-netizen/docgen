using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PortalEntryId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PortalGroupId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PortalEntryId",
                table: "Users",
                column: "PortalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PortalGroupId",
                table: "Users",
                column: "PortalGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_PublicEntities_PortalEntryId",
                table: "Users",
                column: "PortalEntryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_PublicEntityGroups_PortalGroupId",
                table: "Users",
                column: "PortalGroupId",
                principalTable: "PublicEntityGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_PublicEntities_PortalEntryId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_PublicEntityGroups_PortalGroupId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PortalEntryId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PortalGroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PortalEntryId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PortalGroupId",
                table: "Users");
        }
    }
}
