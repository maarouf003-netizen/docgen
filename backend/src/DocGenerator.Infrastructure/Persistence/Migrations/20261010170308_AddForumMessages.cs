using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForumMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForumMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AuthorId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AuthorLocation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthorSection = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    QuotedMessageId = table.Column<int>(type: "INTEGER", nullable: true),
                    QuotedAuthorName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    QuotedExcerpt = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EditedById = table.Column<int>(type: "INTEGER", nullable: true),
                    EditedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumMessages_ForumMessages_QuotedMessageId",
                        column: x => x.QuotedMessageId,
                        principalTable: "ForumMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ForumMessages_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForumMessageReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumMessageReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumMessageReads_ForumMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ForumMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessageReads_MessageId_UserId",
                table: "ForumMessageReads",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessageReads_UserId_MessageId",
                table: "ForumMessageReads",
                columns: new[] { "UserId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_AuthorId",
                table: "ForumMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_CreatedAt",
                table: "ForumMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_IsPinned",
                table: "ForumMessages",
                column: "IsPinned",
                unique: true,
                filter: "\"IsPinned\"");

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_QuotedMessageId",
                table: "ForumMessages",
                column: "QuotedMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForumMessageReads");

            migrationBuilder.DropTable(
                name: "ForumMessages");
        }
    }
}
