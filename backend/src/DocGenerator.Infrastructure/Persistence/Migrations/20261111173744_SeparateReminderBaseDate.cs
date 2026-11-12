using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateReminderBaseDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderBaseDate",
                table: "ExecutionActions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderBaseDate",
                table: "AppealActions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderBaseDate",
                table: "ExecutionActions");

            migrationBuilder.DropColumn(
                name: "ReminderBaseDate",
                table: "AppealActions");
        }
    }
}
