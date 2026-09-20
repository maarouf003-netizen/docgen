using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddDelegationAssetReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DelegationAssetReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<int>(type: "integer", nullable: false),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: false),
                    AssetId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationAssetReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelegationAssetReservations_DocumentDelegations_DelegationId",
                        column: x => x.DelegationId,
                        principalTable: "DocumentDelegations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAssetReservations_DelegationId",
                table: "DelegationAssetReservations",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAssetReservations_SourceDocumentId_AssetId",
                table: "DelegationAssetReservations",
                columns: new[] { "SourceDocumentId", "AssetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegationAssetReservations");
        }
    }
}
