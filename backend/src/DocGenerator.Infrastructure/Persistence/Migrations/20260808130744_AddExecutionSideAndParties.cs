using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionSideAndParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExecutedAmount",
                table: "Documents",
                type: "decimal(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedDescription",
                table: "Documents",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedStatus",
                table: "Documents",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeneralEntitySide",
                table: "Documents",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "applicant");

            migrationBuilder.AddColumn<DateTime>(
                name: "StruckOffDate",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExecutedNaturalPersons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Father = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Family = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AddressType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    AddressOrRepresentative = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    RepresentationType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    DeceasedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeceasedFather = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeceasedFamily = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutedNaturalPersons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutedNaturalPersons_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutedPublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EntityBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutedPublicEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutedPublicEntities_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionApplicants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Father = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Family = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LegalRepresentative = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    RepresentationType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    DeceasedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeceasedFather = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeceasedFamily = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionApplicants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionApplicants_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutedHeirs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutionApplicantId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExecutedNaturalPersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    HeirName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddressType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    HeirAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutedHeirs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutedHeirs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutedHeirs_ExecutedNaturalPersons_ExecutedNaturalPersonId",
                        column: x => x.ExecutedNaturalPersonId,
                        principalTable: "ExecutedNaturalPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutedHeirs_ExecutionApplicants_ExecutionApplicantId",
                        column: x => x.ExecutionApplicantId,
                        principalTable: "ExecutionApplicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ExecutedStatus",
                table: "Documents",
                column: "ExecutedStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_GeneralEntitySide",
                table: "Documents",
                column: "GeneralEntitySide");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedHeirs_DocumentId",
                table: "ExecutedHeirs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedHeirs_ExecutedNaturalPersonId",
                table: "ExecutedHeirs",
                column: "ExecutedNaturalPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedHeirs_ExecutionApplicantId",
                table: "ExecutedHeirs",
                column: "ExecutionApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedNaturalPersons_DocumentId",
                table: "ExecutedNaturalPersons",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedPublicEntities_DocumentId",
                table: "ExecutedPublicEntities",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionApplicants_DocumentId",
                table: "ExecutionApplicants",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutedHeirs");

            migrationBuilder.DropTable(
                name: "ExecutedPublicEntities");

            migrationBuilder.DropTable(
                name: "ExecutedNaturalPersons");

            migrationBuilder.DropTable(
                name: "ExecutionApplicants");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ExecutedStatus",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_GeneralEntitySide",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedAmount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedDescription",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "GeneralEntitySide",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "StruckOffDate",
                table: "Documents");
        }
    }
}
