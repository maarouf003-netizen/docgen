using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddEntityRegistryReviewPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicEntityProposals");

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "PublicEntities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "PublicEntities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedById",
                table: "PublicEntities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_NeedsReview",
                table: "PublicEntities",
                column: "NeedsReview");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_ReviewedById",
                table: "PublicEntities",
                column: "ReviewedById",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PublicEntities_Users_ReviewedById",
                table: "PublicEntities",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublicEntities_Users_ReviewedById",
                table: "PublicEntities");

            migrationBuilder.DropIndex(
                name: "IX_PublicEntities_NeedsReview",
                table: "PublicEntities");

            migrationBuilder.DropIndex(
                name: "IX_PublicEntities_ReviewedById",
                table: "PublicEntities");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "PublicEntities");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "PublicEntities");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "PublicEntities");

            migrationBuilder.CreateTable(
                name: "PublicEntityProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedPublicEntityId = table.Column<int>(type: "integer", nullable: true),
                    ProposedById = table.Column<int>(type: "integer", nullable: false),
                    RejectedById = table.Column<int>(type: "integer", nullable: true),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: true),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CitationFormula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProposedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntityProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicEntityProposals_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PublicEntityProposals_PublicEntities_CreatedPublicEntityId",
                        column: x => x.CreatedPublicEntityId,
                        principalTable: "PublicEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PublicEntityProposals_Users_ProposedById",
                        column: x => x.ProposedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicEntityProposals_Users_RejectedById",
                        column: x => x.RejectedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityProposals_CreatedPublicEntityId",
                table: "PublicEntityProposals",
                column: "CreatedPublicEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityProposals_Governorate",
                table: "PublicEntityProposals",
                column: "Governorate");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityProposals_ProposedById",
                table: "PublicEntityProposals",
                column: "ProposedById");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityProposals_RejectedById",
                table: "PublicEntityProposals",
                column: "RejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityProposals_SourceDocumentId",
                table: "PublicEntityProposals",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityProposals_Status",
                table: "PublicEntityProposals",
                column: "Status");
        }
    }
}
