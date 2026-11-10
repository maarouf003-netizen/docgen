using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetCaseNumberRightType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaseNumber",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightType",
                table: "Assets",
                type: "TEXT",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RightType",
                table: "Assets");
        }
    }
}
