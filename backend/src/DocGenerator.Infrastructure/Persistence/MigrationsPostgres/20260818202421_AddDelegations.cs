using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddDelegations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceDelegationId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: false),
                    DelegatedCourt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalBranchId = table.Column<int>(type: "integer", nullable: true),
                    DelegationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DelegationText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DepositBookNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DepositBookDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SendBookNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SendBookDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedLawyerId = table.Column<int>(type: "integer", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentDelegations_Branches_ExternalBranchId",
                        column: x => x.ExternalBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentDelegations_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentDelegations_Users_AssignedLawyerId",
                        column: x => x.AssignedLawyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentDelegations_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DelegationAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<int>(type: "integer", nullable: false),
                    AssetKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetLabel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(20,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelegationAssets_DocumentDelegations_DelegationId",
                        column: x => x.DelegationId,
                        principalTable: "DocumentDelegations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SourceDelegationId",
                table: "Documents",
                column: "SourceDelegationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAssets_DelegationId",
                table: "DelegationAssets",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_AssignedLawyerId",
                table: "DocumentDelegations",
                column: "AssignedLawyerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_CreatedById",
                table: "DocumentDelegations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_ExternalBranchId",
                table: "DocumentDelegations",
                column: "ExternalBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_SourceDocumentId",
                table: "DocumentDelegations",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_Status",
                table: "DocumentDelegations",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentDelegations_SourceDelegationId",
                table: "Documents",
                column: "SourceDelegationId",
                principalTable: "DocumentDelegations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DocumentDelegations_SourceDelegationId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "DelegationAssets");

            migrationBuilder.DropTable(
                name: "DocumentDelegations");

            migrationBuilder.DropIndex(
                name: "IX_Documents_SourceDelegationId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SourceDelegationId",
                table: "Documents");
        }
    }
}
