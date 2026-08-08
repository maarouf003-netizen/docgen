using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutedSideAmountsAndReceiptDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExecutedAmount",
                table: "Documents",
                newName: "ExecutedRequiredAmount");

            migrationBuilder.AddColumn<string>(
                name: "HeirFamily",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeirFather",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExecutedPaidAmount",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FileReceiptDate",
                table: "Documents",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeirFamily",
                table: "ExecutedHeirs");

            migrationBuilder.DropColumn(
                name: "HeirFather",
                table: "ExecutedHeirs");

            migrationBuilder.DropColumn(
                name: "ExecutedPaidAmount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileReceiptDate",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "ExecutedRequiredAmount",
                table: "Documents",
                newName: "ExecutedAmount");
        }
    }
}
