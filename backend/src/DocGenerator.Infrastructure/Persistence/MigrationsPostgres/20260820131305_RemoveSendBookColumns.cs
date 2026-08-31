using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class RemoveSendBookColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SendBookDate",
                table: "DocumentDelegations");

            migrationBuilder.DropColumn(
                name: "SendBookNumber",
                table: "DocumentDelegations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SendBookDate",
                table: "DocumentDelegations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendBookNumber",
                table: "DocumentDelegations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
