using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewLetterNotifications : Migration
    {
/// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeenByLawyer",
                table: "ReviewLetterMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReviewLetterId",
                table: "HeadAlerts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_ReviewLetterId",
                table: "HeadAlerts",
                column: "ReviewLetterId");

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_ReviewLetters_ReviewLetterId",
                table: "HeadAlerts",
                column: "ReviewLetterId",
                principalTable: "ReviewLetters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HeadAlerts_ReviewLetters_ReviewLetterId",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_ReviewLetterId",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "IsSeenByLawyer",
                table: "ReviewLetterMessages");

            migrationBuilder.DropColumn(
                name: "ReviewLetterId",
                table: "HeadAlerts");
        }
    }
}
