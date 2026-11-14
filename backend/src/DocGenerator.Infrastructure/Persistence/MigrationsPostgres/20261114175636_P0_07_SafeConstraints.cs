using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class P0_07_SafeConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId_SectionId",
                table: "Users",
                columns: new[] { "BranchId", "SectionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_VersionNonNegative",
                table: "Users",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sections_VersionNonNegative",
                table: "Sections",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReviewLetters_VersionNonNegative",
                table: "ReviewLetters",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReviewLetterMessages_VersionNonNegative",
                table: "ReviewLetterMessages",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PublicEntityGroups_VersionNonNegative",
                table: "PublicEntityGroups",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PublicEntities_VersionNonNegative",
                table: "PublicEntities",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalReminders_VersionNonNegative",
                table: "PersonalReminders",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ForumMessages_VersionNonNegative",
                table: "ForumMessages",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExecutionCircuits_VersionNonNegative",
                table: "ExecutionCircuits",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExecutionActions_VersionNonNegative",
                table: "ExecutionActions",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Documents_VersionNonNegative",
                table: "Documents",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentInterventions_VersionNonNegative",
                table: "DocumentInterventions",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentDelegations_VersionNonNegative",
                table: "DocumentDelegations",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentAppeals_VersionNonNegative",
                table: "DocumentAppeals",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Correspondences_VersionNonNegative",
                table: "Correspondences",
                sql: "\"Version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Branches_VersionNonNegative",
                table: "Branches",
                sql: "\"Version\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_BranchId_SectionId",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_VersionNonNegative",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sections_VersionNonNegative",
                table: "Sections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReviewLetters_VersionNonNegative",
                table: "ReviewLetters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReviewLetterMessages_VersionNonNegative",
                table: "ReviewLetterMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PublicEntityGroups_VersionNonNegative",
                table: "PublicEntityGroups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PublicEntities_VersionNonNegative",
                table: "PublicEntities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalReminders_VersionNonNegative",
                table: "PersonalReminders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ForumMessages_VersionNonNegative",
                table: "ForumMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExecutionCircuits_VersionNonNegative",
                table: "ExecutionCircuits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExecutionActions_VersionNonNegative",
                table: "ExecutionActions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Documents_VersionNonNegative",
                table: "Documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentInterventions_VersionNonNegative",
                table: "DocumentInterventions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentDelegations_VersionNonNegative",
                table: "DocumentDelegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentAppeals_VersionNonNegative",
                table: "DocumentAppeals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Correspondences_VersionNonNegative",
                table: "Correspondences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Branches_VersionNonNegative",
                table: "Branches");
        }
    }
}
