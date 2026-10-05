using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddExecutionCircuitRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_Court_FileNumber_FileType_FileYear",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CourtNorm_FileNumber_FileType_FileYear",
                table: "Documents");

            migrationBuilder.AddColumn<int>(
                name: "ExecutionCircuitId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsRegistration",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FromCircuitName",
                table: "DocumentOccurrences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToCircuitName",
                table: "DocumentOccurrences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DelegatedCircuitId",
                table: "DocumentDelegations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExecutionCircuits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameNorm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionCircuits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionCircuits_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutionCircuits_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CourtNorm_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "CourtNorm", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL AND \"ExecutionCircuitId\" IS NULL AND \"CourtNorm\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ExecutionCircuitId",
                table: "Documents",
                column: "ExecutionCircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ExecutionCircuitId_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "ExecutionCircuitId", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL AND \"ExecutionCircuitId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_DelegatedCircuitId",
                table: "DocumentDelegations",
                column: "DelegatedCircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_BranchId",
                table: "ExecutionCircuits",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_BranchId_NameNorm",
                table: "ExecutionCircuits",
                columns: new[] { "BranchId", "NameNorm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_CreatedById",
                table: "ExecutionCircuits",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_IsActive",
                table: "ExecutionCircuits",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentDelegations_ExecutionCircuits_DelegatedCircuitId",
                table: "DocumentDelegations",
                column: "DelegatedCircuitId",
                principalTable: "ExecutionCircuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_ExecutionCircuits_ExecutionCircuitId",
                table: "Documents",
                column: "ExecutionCircuitId",
                principalTable: "ExecutionCircuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentDelegations_ExecutionCircuits_DelegatedCircuitId",
                table: "DocumentDelegations");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_ExecutionCircuits_ExecutionCircuitId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "ExecutionCircuits");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CourtNorm_FileNumber_FileType_FileYear",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ExecutionCircuitId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ExecutionCircuitId_FileNumber_FileType_FileYear",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_DocumentDelegations_DelegatedCircuitId",
                table: "DocumentDelegations");

            migrationBuilder.DropColumn(
                name: "ExecutionCircuitId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "NeedsRegistration",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FromCircuitName",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "ToCircuitName",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "DelegatedCircuitId",
                table: "DocumentDelegations");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Court_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "Court", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CourtNorm_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "CourtNorm", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL AND \"CourtNorm\" IS NOT NULL");
        }
    }
}
