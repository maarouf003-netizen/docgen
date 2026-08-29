using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadAlertPublicEntityLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicEntities_ReviewedById",
                table: "PublicEntities");

            migrationBuilder.AddColumn<int>(
                name: "PublicEntityId",
                table: "HeadAlerts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_ReviewedById",
                table: "PublicEntities",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_PublicEntityId",
                table: "HeadAlerts",
                column: "PublicEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_PublicEntities_PublicEntityId",
                table: "HeadAlerts",
                column: "PublicEntityId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HeadAlerts_PublicEntities_PublicEntityId",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_PublicEntities_ReviewedById",
                table: "PublicEntities");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_PublicEntityId",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "PublicEntityId",
                table: "HeadAlerts");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_ReviewedById",
                table: "PublicEntities",
                column: "ReviewedById",
                unique: true);
        }
    }
}
