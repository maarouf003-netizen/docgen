using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DelegationLocalAdditionSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocalAdditionCount",
                table: "DocumentDelegations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocalAdditionLastAt",
                table: "DocumentDelegations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalAdditionLastMessage",
                table: "DocumentDelegations",
                type: "TEXT",
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
