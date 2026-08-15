using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddStatusChangeFieldsAndCollectedCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CollectedAmount2",
                table: "Documents",
                type: "numeric(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CollectedAmount3",
                table: "Documents",
                type: "numeric(20,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectedCurrency",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectedCurrency2",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectedCurrency3",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SayerDate",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SayerNumber",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SayerRegDate",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SayerRegNumber",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoldEstateIds",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "DocumentOccurrences",
                type: "text",
                nullable: true);

            // الملفات القائمة حاملة «المبلغ المحصل» بلا عملة تُضبط عملتها الأولى «ليرة سورية»
            // حتى يظهر المبلغ القديم ويعود في الإحصائيات ضمن عملة الليرة.
            migrationBuilder.Sql(
                "UPDATE \"Documents\" SET \"CollectedCurrency\" = 'ليرة سورية' "
                + "WHERE \"CollectedAmount\" IS NOT NULL "
                + "AND (\"CollectedCurrency\" IS NULL OR \"CollectedCurrency\" = '');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectedAmount2",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CollectedAmount3",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CollectedCurrency",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CollectedCurrency2",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CollectedCurrency3",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SayerDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SayerNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SayerRegDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SayerRegNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SoldEstateIds",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "DocumentOccurrences");
        }
    }
}
