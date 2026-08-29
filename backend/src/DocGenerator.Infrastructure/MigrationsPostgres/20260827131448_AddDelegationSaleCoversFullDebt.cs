using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddDelegationSaleCoversFullDebt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SaleCoversFullDebt",
                table: "DocumentDelegations",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaleCoversFullDebt",
                table: "DocumentDelegations");
        }
    }
}
