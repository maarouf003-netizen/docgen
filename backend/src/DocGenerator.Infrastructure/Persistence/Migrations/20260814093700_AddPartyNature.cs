using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyNature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuarantorNature",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "natural");

            migrationBuilder.AddColumn<string>(
                name: "GuarantorRegistrationNumber",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuarantorRepresentedBy",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAddress",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAddressType",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantNature",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "natural");

            migrationBuilder.AddColumn<string>(
                name: "ApplicantRegistrationNumber",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantRepresentedBy",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ExecutedPublicEntities",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressType",
                table: "ExecutedPublicEntities",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityNature",
                table: "ExecutedPublicEntities",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "public");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "ExecutedPublicEntities",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentedBy",
                table: "ExecutedPublicEntities",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerNature",
                table: "Documents",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "natural");

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRegistrationNumber",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentedBy",
                table: "Documents",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "Address",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "AddressType",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "EntityNature",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "RepresentedBy",
                table: "ExecutedPublicEntities");

            migrationBuilder.DropColumn(
                name: "BorrowerNature",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRegistrationNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BorrowerRepresentedBy",
                table: "Documents");
        }
    }
}
