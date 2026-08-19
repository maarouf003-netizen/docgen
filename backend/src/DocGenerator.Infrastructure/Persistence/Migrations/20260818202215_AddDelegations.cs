using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
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
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceDocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    DelegatedCourt = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IsExternal = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExternalBranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    DelegationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DelegationText = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DepositBookNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DepositBookDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SendBookNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SendBookDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedLawyerId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DelegationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetKind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AssetLabel = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(20,2)", nullable: true)
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
