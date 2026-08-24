using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddEntityRegistryLinksPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegistryId",
                table: "ExecutedPublicEntities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicantRegistryId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistryId",
                table: "ApplicantPublicEntities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedPublicEntities_RegistryId",
                table: "ExecutedPublicEntities",
                column: "RegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ApplicantRegistryId",
                table: "Documents",
                column: "ApplicantRegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantPublicEntities_RegistryId",
                table: "ApplicantPublicEntities",
                column: "RegistryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicantPublicEntities_PublicEntities_RegistryId",
                table: "ApplicantPublicEntities",
                column: "RegistryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutedPublicEntities_PublicEntities_RegistryId",
                table: "ExecutedPublicEntities",
                column: "RegistryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicantPublicEntities_PublicEntities_RegistryId",
                table: "ApplicantPublicEntities");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutedPublicEntities_PublicEntities_RegistryId",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropIndex(
                name: "IX_ExecutedPublicEntities_RegistryId",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ApplicantRegistryId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_ApplicantPublicEntities_RegistryId",
                table: "ApplicantPublicEntities");

            migrationBuilder.DropColumn(
                name: "RegistryId",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "ApplicantRegistryId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RegistryId",
                table: "ApplicantPublicEntities");
        }
    }
}
