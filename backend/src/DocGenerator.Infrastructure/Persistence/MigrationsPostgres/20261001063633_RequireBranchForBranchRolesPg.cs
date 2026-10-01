using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class RequireBranchForBranchRolesPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_BranchRequiredForBranchRoles",
                table: "Users",
                sql: "\"BranchId\" IS NOT NULL OR \"Role\" NOT IN ('Lawyer', 'Head')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_BranchRequiredForBranchRoles",
                table: "Users");
        }
    }
}
