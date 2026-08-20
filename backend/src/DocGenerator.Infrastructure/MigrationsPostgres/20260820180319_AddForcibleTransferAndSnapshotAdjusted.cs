using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddForcibleTransferAndSnapshotAdjusted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ForcibleTransferDate",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForcibleTransferNoticeNumber",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SnapshotAdjusted",
                table: "DelegationAssets",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForcibleTransferDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ForcibleTransferNoticeNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SnapshotAdjusted",
                table: "DelegationAssets");
        }
    }
}
