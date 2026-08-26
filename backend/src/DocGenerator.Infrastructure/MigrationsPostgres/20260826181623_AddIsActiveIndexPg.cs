using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddIsActiveIndexPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_IsActive",
                table: "PublicEntities",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicEntities_IsActive",
                table: "PublicEntities");
        }
    }
}
