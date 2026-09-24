using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrespondences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Correspondences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    Governorate = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    CorrespondenceNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CorrespondenceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Importance = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correspondences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Correspondences_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Correspondences_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Correspondences_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CorrespondenceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BodyHtml = table.Column<string>(type: "TEXT", nullable: false),
                    BodyPlainText = table.Column<string>(type: "TEXT", nullable: false),
                    MessageNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MessageDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceMessages_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CorrespondenceId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReceipts_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceMessages_BodyPlainText",
                table: "CorrespondenceMessages",
                column: "BodyPlainText");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceMessages_CorrespondenceId",
                table: "CorrespondenceMessages",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReceipts_CorrespondenceId_UserId",
                table: "CorrespondenceReceipts",
                columns: new[] { "CorrespondenceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReceipts_UserId",
                table: "CorrespondenceReceipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_BranchId",
                table: "Correspondences",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_CorrespondenceNumber",
                table: "Correspondences",
                column: "CorrespondenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_CreatedById",
                table: "Correspondences",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_DocumentId",
                table: "Correspondences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Governorate",
                table: "Correspondences",
                column: "Governorate");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Importance",
                table: "Correspondences",
                column: "Importance");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_TargetUserId",
                table: "Correspondences",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_UpdatedAt",
                table: "Correspondences",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrespondenceMessages");

            migrationBuilder.DropTable(
                name: "CorrespondenceReceipts");

            migrationBuilder.DropTable(
                name: "Correspondences");
        }
    }
}
