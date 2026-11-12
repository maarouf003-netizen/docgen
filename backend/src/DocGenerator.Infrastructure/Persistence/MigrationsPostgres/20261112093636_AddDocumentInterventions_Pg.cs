using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddDocumentInterventions_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentInterventionId",
                table: "DocumentOccurrences",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentInterventions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Nature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "natural"),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptanceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Subject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Birth = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Register = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NationalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentInterventions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentInterventions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentInterventions_PublicEntities_RegistryId",
                        column: x => x.RegistryId,
                        principalTable: "PublicEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentInterventions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentInterventionHeirs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentInterventionId = table.Column<int>(type: "integer", nullable: false),
                    HeirName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HeirFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HeirFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeirAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentInterventionHeirs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentInterventionHeirs_DocumentInterventions_DocumentInt~",
                        column: x => x.DocumentInterventionId,
                        principalTable: "DocumentInterventions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_DocumentInterventionId",
                table: "DocumentOccurrences",
                column: "DocumentInterventionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventionHeirs_DocumentInterventionId",
                table: "DocumentInterventionHeirs",
                column: "DocumentInterventionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventions_CreatedById",
                table: "DocumentInterventions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventions_DocumentId",
                table: "DocumentInterventions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventions_RegistryId",
                table: "DocumentInterventions",
                column: "RegistryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentOccurrences_DocumentInterventions_DocumentIntervent~",
                table: "DocumentOccurrences",
                column: "DocumentInterventionId",
                principalTable: "DocumentInterventions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentOccurrences_DocumentInterventions_DocumentIntervent~",
                table: "DocumentOccurrences");

            migrationBuilder.DropTable(
                name: "DocumentInterventionHeirs");

            migrationBuilder.DropTable(
                name: "DocumentInterventions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentOccurrences_DocumentInterventionId",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "DocumentInterventionId",
                table: "DocumentOccurrences");
        }
    }
}
