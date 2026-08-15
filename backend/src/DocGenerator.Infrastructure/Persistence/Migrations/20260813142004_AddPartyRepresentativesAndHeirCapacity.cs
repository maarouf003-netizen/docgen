using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyRepresentativesAndHeirCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeirCapacity",
                table: "Heirs",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "Guarantors",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeLegalRepresentative",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "ExecutionApplicants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "ExecutedNaturalPersons",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "ExecutedNaturalPersons",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "ExecutedNaturalPersons",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "ExecutedNaturalPersons",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "ExecutedNaturalPersons",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "ExecutedNaturalPersons",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeAddress",
                table: "Documents",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeAddressType",
                table: "Documents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeCapacity",
                table: "Documents",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeFamily",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeFather",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorrowerRepresentativeName",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeirCapacity",
                table: "Heirs");

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
        }
    }
}
