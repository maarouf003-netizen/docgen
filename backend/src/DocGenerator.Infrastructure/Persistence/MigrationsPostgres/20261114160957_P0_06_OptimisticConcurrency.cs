using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class P0_06_OptimisticConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Sections",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ReviewLetters",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ReviewLetterMessages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "PublicEntityGroups",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "PublicEntities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "PersonalReminders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ForumMessages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ExecutionActions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "DocumentInterventions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Correspondences",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Branches",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ReviewLetters");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ReviewLetterMessages");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PublicEntityGroups");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PublicEntities");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PersonalReminders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ForumMessages");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ExecutionActions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "DocumentInterventions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Correspondences");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Branches");
        }
    }
}
