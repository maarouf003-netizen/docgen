using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeirLegalRepresentative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "Heirs",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "Heirs",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "Heirs",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "Heirs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "Heirs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "Heirs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "ExecutedHeirs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepresentativeAddress",
                table: "Heirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeAddressType",
                table: "Heirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeCapacity",
                table: "Heirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeFamily",
                table: "Heirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeFather",
                table: "Heirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeName",
                table: "Heirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeAddress",
                table: "ExecutedHeirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeAddressType",
                table: "ExecutedHeirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeCapacity",
                table: "ExecutedHeirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeFamily",
                table: "ExecutedHeirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeFather",
                table: "ExecutedHeirs");

            migrationBuilder.DropColumn(
                name: "RepresentativeName",
                table: "ExecutedHeirs");
        }
    }
}
