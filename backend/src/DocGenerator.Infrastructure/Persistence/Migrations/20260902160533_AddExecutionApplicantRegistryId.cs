using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionApplicantRegistryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegistryId",
                table: "ExecutionApplicants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionApplicants_RegistryId",
                table: "ExecutionApplicants",
                column: "RegistryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionApplicants_PublicEntities_RegistryId",
                table: "ExecutionApplicants",
                column: "RegistryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionApplicants_PublicEntities_RegistryId",
                table: "ExecutionApplicants");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionApplicants_RegistryId",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "RegistryId",
                table: "ExecutionApplicants");
        }
    }
}
