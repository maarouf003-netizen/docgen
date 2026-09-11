using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddParentEditSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentEditSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: false),
                    ProposedCanonicalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedEntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ProposedCitationFormula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedBranchId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentEditSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentEditSuggestions_PublicEntities_EntryId",
                        column: x => x.EntryId,
                        principalTable: "PublicEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentEditSuggestions_PublicEntityGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PublicEntityGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentEditSuggestions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentEditSuggestions_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_CreatedBranchId",
                table: "ParentEditSuggestions",
                column: "CreatedBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_CreatedById",
                table: "ParentEditSuggestions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_EntryId",
                table: "ParentEditSuggestions",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_GroupId_CreatedBranchId",
                table: "ParentEditSuggestions",
                columns: new[] { "GroupId", "CreatedBranchId" },
                unique: true,
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_ReviewedById",
                table: "ParentEditSuggestions",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_Status",
                table: "ParentEditSuggestions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentEditSuggestions");
        }
    }
}
