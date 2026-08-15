using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddPostgresPendingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeirCapacity",
                table: "Heirs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuarantorNature",
                table: "Guarantors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "natural");

            migrationBuilder.AddColumn<string>(
                name: "GuarantorRegistrationNumber",
                table: "Guarantors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuarantorRepresentedBy",
                table: "Guarantors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "Guarantors",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "Guarantors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "Guarantors",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "Guarantors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "Guarantors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "Guarantors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAddress",
                table: "ExecutionApplicants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAddressType",
                table: "ExecutionApplicants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantNature",
                table: "ExecutionApplicants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "natural");

            migrationBuilder.AddColumn<string>(
                name: "ApplicantRegistrationNumber",
                table: "ExecutionApplicants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantRepresentedBy",
                table: "ExecutionApplicants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "ExecutionApplicants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "ExecutionApplicants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "ExecutionApplicants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeLegalRepresentative",
                table: "ExecutionApplicants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "ExecutionApplicants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ExecutedPublicEntities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressType",
                table: "ExecutedPublicEntities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityNature",
                table: "ExecutedPublicEntities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "public");

            migrationBuilder.AddColumn<string>(
                name: "Governorate",
                table: "ExecutedPublicEntities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "ExecutedPublicEntities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentedBy",
                table: "ExecutedPublicEntities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "ExecutedNaturalPersons",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "ExecutedNaturalPersons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "ExecutedNaturalPersons",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "ExecutedNaturalPersons",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "ExecutedNaturalPersons",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "ExecutedNaturalPersons",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UnderFilingNumber",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerNature",
                table: "Documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "natural");

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRegistrationNumber",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeAddress",
                table: "Documents",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeAddressType",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeCapacity",
                table: "Documents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeFamily",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeFather",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeName",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentedBy",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutedDepositDate",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileArrivalDate",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileArrivalNumber",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalDate",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenewalFileNumber",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalFileReceiptDate",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenewalFileReceiptNumber",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RenewalFileType",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicantPublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantPublicEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicantPublicEntities_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LawyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAssignments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    OccurrenceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantPublicEntities_DocumentId",
                table: "ApplicantPublicEntities",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAssignments_DocumentId",
                table: "DocumentAssignments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CreatedById",
                table: "DocumentOccurrences",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_DocumentId",
                table: "DocumentOccurrences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_EventDate",
                table: "DocumentOccurrences",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_OccurrenceType",
                table: "DocumentOccurrences",
                column: "OccurrenceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantPublicEntities");

            migrationBuilder.DropTable(
                name: "DocumentAssignments");

            migrationBuilder.DropTable(
                name: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "HeirCapacity",
                table: "Heirs");

            migrationBuilder.DropColumn(
                name: "GuarantorNature",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuarantorRegistrationNumber",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuarantorRepresentedBy",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "RepresentativeAddress",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "RepresentativeAddressType",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "RepresentativeCapacity",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "RepresentativeFamily",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "RepresentativeFather",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "RepresentativeName",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "ApplicantAddress",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "ApplicantAddressType",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "ApplicantNature",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "ApplicantRegistrationNumber",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "ApplicantRepresentedBy",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "RepresentativeCapacity",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "RepresentativeFamily",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "RepresentativeFather",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "RepresentativeLegalRepresentative",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "RepresentativeName",
                table: "ExecutionApplicants");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "AddressType",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "EntityNature",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "Governorate",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "RepresentedBy",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "RepresentativeAddress",
                table: "ExecutedNaturalPersons");

            migrationBuilder.DropColumn(
                name: "RepresentativeAddressType",
                table: "ExecutedNaturalPersons");

            migrationBuilder.DropColumn(
                name: "RepresentativeCapacity",
                table: "ExecutedNaturalPersons");

            migrationBuilder.DropColumn(
                name: "RepresentativeFamily",
                table: "ExecutedNaturalPersons");

            migrationBuilder.DropColumn(
                name: "RepresentativeFather",
                table: "ExecutedNaturalPersons");

            migrationBuilder.DropColumn(
                name: "RepresentativeName",
                table: "ExecutedNaturalPersons");

            migrationBuilder.DropColumn(
                name: "BorrowerNature",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRegistrationNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentativeAddress",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentativeAddressType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentativeCapacity",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentativeFamily",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentativeFather",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentativeName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentedBy",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExecutedDepositDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileArrivalDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileArrivalNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileReceiptDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileReceiptNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RenewalFileType",
                table: "Documents");

            migrationBuilder.AlterColumn<string>(
                name: "UnderFilingNumber",
                table: "Documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
