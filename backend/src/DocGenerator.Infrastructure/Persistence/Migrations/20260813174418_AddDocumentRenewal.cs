using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalDate",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenewalFileNumber",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalFileReceiptDate",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenewalFileReceiptNumber",
                table: "Documents",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenewalFileType",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenewalDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileReceiptDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileReceiptNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileType",
                table: "Documents");
        }
    }
}
