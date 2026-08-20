using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
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
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForcibleTransferNoticeNumber",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SnapshotAdjusted",
                table: "DelegationAssets",
                type: "INTEGER",
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
