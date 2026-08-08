using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeadAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetType = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetLawyerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Users_TargetLawyerId",
                        column: x => x.TargetLawyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HeadAlertRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HeadAlertId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadAlertRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeadAlertRecipients_HeadAlerts_HeadAlertId",
                        column: x => x.HeadAlertId,
                        principalTable: "HeadAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HeadAlertRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_HeadAlertId",
                table: "HeadAlertRecipients",
                column: "HeadAlertId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_UserId",
                table: "HeadAlertRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_BranchId",
                table: "HeadAlerts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_CreatedAt",
                table: "HeadAlerts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_CreatedById",
                table: "HeadAlerts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_DocumentId",
                table: "HeadAlerts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_TargetLawyerId",
                table: "HeadAlerts",
                column: "TargetLawyerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeadAlertRecipients");

            migrationBuilder.DropTable(
                name: "HeadAlerts");
        }
    }
}
