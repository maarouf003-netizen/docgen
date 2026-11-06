using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddHeirLegalRepresentative_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "Heirs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "Heirs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "Heirs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "Heirs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "Heirs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "Heirs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddress",
                table: "ExecutedHeirs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeAddressType",
                table: "ExecutedHeirs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeCapacity",
                table: "ExecutedHeirs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFamily",
                table: "ExecutedHeirs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeFather",
                table: "ExecutedHeirs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeName",
                table: "ExecutedHeirs",
                type: "character varying(100)",
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
