using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewLetters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewLetters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    LetterNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LetterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAnswered = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLetters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLetters_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewLetters_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReviewLetters_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewLetterMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReviewLetterId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_ReviewLetterMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLetterMessages_ReviewLetters_ReviewLetterId",
                        column: x => x.ReviewLetterId,
                        principalTable: "ReviewLetters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetterMessages_BodyPlainText",
                table: "ReviewLetterMessages",
                column: "BodyPlainText");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetterMessages_ReviewLetterId",
                table: "ReviewLetterMessages",
                column: "ReviewLetterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_BranchId",
                table: "ReviewLetters",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_CreatedById",
                table: "ReviewLetters",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_DocumentId",
                table: "ReviewLetters",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_IsAnswered",
                table: "ReviewLetters",
                column: "IsAnswered");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_LetterNumber",
                table: "ReviewLetters",
                column: "LetterNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_UpdatedAt",
                table: "ReviewLetters",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewLetterMessages");

            migrationBuilder.DropTable(
                name: "ReviewLetters");
        }
    }
}
