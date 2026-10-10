using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubHeadSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_BranchId",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_BranchRequiredForBranchRoles",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "CreatedById",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientSectionId",
                table: "ReviewLetters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "ExecutionCircuits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RedirectedToSectionId",
                table: "DocumentDelegations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "DocumentDelegations",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "DocumentDelegations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ForwardReason",
                table: "DocumentAppeals",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForwardState",
                table: "DocumentAppeals",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Owned");

            migrationBuilder.AddColumn<DateTime>(
                name: "ForwardedAt",
                table: "DocumentAppeals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ForwardedById",
                table: "DocumentAppeals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "DocumentAppeals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RecipientSectionId",
                table: "Correspondences",
                type: "INTEGER",
                nullable: true);

            // ترحيل الصفوف القائمة (§4.10): `Version = 1` (القيمة المعرفة —
            // مساوية لسلوك الصفوف الجديدة التي تضبطها الخدمة عند الإنشاء).
            migrationBuilder.Sql("UPDATE \"DocumentAppeals\" SET \"Version\" = 1");
            migrationBuilder.Sql("UPDATE \"DocumentDelegations\" SET \"Version\" = 1");

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NameNorm = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HeadSuccessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Event = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadSuccessions", x => x.Id);
                    table.CheckConstraint("CK_HeadSuccessions_Event", "\"Event\" IN ('appointed', 'deactivated', 'succeeded', 'circuit-transferred', 'renamed')");
                    table.ForeignKey(
                        name: "FK_HeadSuccessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadSuccessions_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadSuccessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId",
                table: "Users",
                column: "BranchId",
                unique: true,
                filter: "\"Role\" = 'Head' AND \"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedById",
                table: "Users",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SectionId",
                table: "Users",
                column: "SectionId",
                unique: true,
                filter: "\"Role\" = 'SubHead' AND \"IsActive\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_BranchRequiredForBranchRoles",
                table: "Users",
                sql: "\"BranchId\" IS NOT NULL OR \"Role\" NOT IN ('Lawyer', 'Head', 'SubHead')");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_RecipientSectionId",
                table: "ReviewLetters",
                column: "RecipientSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_SectionId",
                table: "ExecutionCircuits",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_RedirectedToSectionId",
                table: "DocumentDelegations",
                column: "RedirectedToSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_ForwardedById",
                table: "DocumentAppeals",
                column: "ForwardedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_ForwardState",
                table: "DocumentAppeals",
                column: "ForwardState");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentAppeals_ForwardState",
                table: "DocumentAppeals",
                sql: "\"ForwardState\" IN ('Owned', 'ForwardedToHead')");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_RecipientSectionId",
                table: "Correspondences",
                column: "RecipientSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_At",
                table: "HeadSuccessions",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_BranchId",
                table: "HeadSuccessions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_SectionId",
                table: "HeadSuccessions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_UserId",
                table: "HeadSuccessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_BranchId",
                table: "Sections",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_BranchId_NameNorm",
                table: "Sections",
                columns: new[] { "BranchId", "NameNorm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_IsActive",
                table: "Sections",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_Correspondences_Sections_RecipientSectionId",
                table: "Correspondences",
                column: "RecipientSectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentDelegations_Sections_RedirectedToSectionId",
                table: "DocumentDelegations",
                column: "RedirectedToSectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionCircuits_Sections_SectionId",
                table: "ExecutionCircuits",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewLetters_Sections_RecipientSectionId",
                table: "ReviewLetters",
                column: "RecipientSectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Sections_SectionId",
                table: "Users",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_CreatedById",
                table: "Users",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Correspondences_Sections_RecipientSectionId",
                table: "Correspondences");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentDelegations_Sections_RedirectedToSectionId",
                table: "DocumentDelegations");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionCircuits_Sections_SectionId",
                table: "ExecutionCircuits");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewLetters_Sections_RecipientSectionId",
                table: "ReviewLetters");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Sections_SectionId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_CreatedById",
                table: "Users");

            migrationBuilder.DropTable(
                name: "HeadSuccessions");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Users_BranchId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedById",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SectionId",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_BranchRequiredForBranchRoles",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ReviewLetters_RecipientSectionId",
                table: "ReviewLetters");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionCircuits_SectionId",
                table: "ExecutionCircuits");

            migrationBuilder.DropIndex(
                name: "IX_DocumentDelegations_RedirectedToSectionId",
                table: "DocumentDelegations");

            migrationBuilder.DropIndex(
                name: "IX_DocumentAppeals_ForwardedById",
                table: "DocumentAppeals");

            migrationBuilder.DropIndex(
                name: "IX_DocumentAppeals_ForwardState",
                table: "DocumentAppeals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentAppeals_ForwardState",
                table: "DocumentAppeals");

            migrationBuilder.DropIndex(
                name: "IX_Correspondences_RecipientSectionId",
                table: "Correspondences");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RecipientSectionId",
                table: "ReviewLetters");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "ExecutionCircuits");

            migrationBuilder.DropColumn(
                name: "RedirectedToSectionId",
                table: "DocumentDelegations");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "DocumentDelegations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "DocumentDelegations");

            migrationBuilder.DropColumn(
                name: "ForwardReason",
                table: "DocumentAppeals");

            migrationBuilder.DropColumn(
                name: "ForwardState",
                table: "DocumentAppeals");

            migrationBuilder.DropColumn(
                name: "ForwardedAt",
                table: "DocumentAppeals");

            migrationBuilder.DropColumn(
                name: "ForwardedById",
                table: "DocumentAppeals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "DocumentAppeals");

            migrationBuilder.DropColumn(
                name: "RecipientSectionId",
                table: "Correspondences");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId",
                table: "Users",
                column: "BranchId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_BranchRequiredForBranchRoles",
                table: "Users",
                sql: "\"BranchId\" IS NOT NULL OR \"Role\" NOT IN ('Lawyer', 'Head')");
        }
    }
}
