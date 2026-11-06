using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class DelegationLocalAdditionSummary_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocalAdditionCount",
                table: "DocumentDelegations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocalAdditionLastAt",
                table: "DocumentDelegations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalAdditionLastMessage",
                table: "DocumentDelegations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalAdditionCount",
                table: "DocumentDelegations");

            migrationBuilder.DropColumn(
                name: "LocalAdditionLastAt",
                table: "DocumentDelegations");

            migrationBuilder.DropColumn(
                name: "LocalAdditionLastMessage",
                table: "DocumentDelegations");
        }
    }
}
