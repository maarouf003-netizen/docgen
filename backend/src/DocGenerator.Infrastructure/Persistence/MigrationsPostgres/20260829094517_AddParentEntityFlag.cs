using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddParentEntityFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsParentEntity",
                table: "PublicEntities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_IsParentEntity",
                table: "PublicEntities",
                column: "IsParentEntity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicEntities_IsParentEntity",
                table: "PublicEntities");

            migrationBuilder.DropColumn(
                name: "IsParentEntity",
                table: "PublicEntities");
        }
    }
}
