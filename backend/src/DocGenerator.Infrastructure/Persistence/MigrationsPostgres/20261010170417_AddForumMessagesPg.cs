using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddForumMessagesPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForumMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuthorSection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    QuotedMessageId = table.Column<int>(type: "integer", nullable: true),
                    QuotedAuthorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    QuotedExcerpt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedById = table.Column<int>(type: "integer", nullable: true),
                    EditedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
