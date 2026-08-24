using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddEntityRegistryPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Governorate",
                table: "Branches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicEntityGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanonicalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntityGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CitationFormula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicEntities_PublicEntityGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PublicEntityGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicEntities_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicEntityAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicEntityId = table.Column<int>(type: "integer", nullable: false),
                    AliasText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntityAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicEntityAliases_PublicEntities_PublicEntityId",
                        column: x => x.PublicEntityId,
                        principalTable: "PublicEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicEntityProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProposedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CitationFormula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProposedById = table.Column<int>(type: "integer", nullable: false),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectedById = table.Column<int>(type: "integer", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedPublicEntityId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_PublicEntities_CreatedById",
                table: "PublicEntities",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_Governorate",
                table: "PublicEntities",
                column: "Governorate");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_GroupId",
                table: "PublicEntities",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_GroupId_Governorate_BranchName",
                table: "PublicEntities",
                columns: new[] { "GroupId", "Governorate", "BranchName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_Status",
                table: "PublicEntities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityAliases_AliasText",
                table: "PublicEntityAliases",
                column: "AliasText");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityAliases_PublicEntityId",
                table: "PublicEntityAliases",
                column: "PublicEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityGroups_CanonicalName",
                table: "PublicEntityGroups",
                column: "CanonicalName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityGroups_EntityType",
                table: "PublicEntityGroups",
                column: "EntityType");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicEntityAliases");

            migrationBuilder.DropTable(
                name: "PublicEntityProposals");

            migrationBuilder.DropTable(
                name: "PublicEntities");

            migrationBuilder.DropTable(
                name: "PublicEntityGroups");

            migrationBuilder.DropColumn(
                name: "Governorate",
                table: "Branches");
        }
    }
}
