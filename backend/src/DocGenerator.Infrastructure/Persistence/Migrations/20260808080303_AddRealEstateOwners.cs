using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRealEstateOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RealEstateOwners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RealEstateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealEstateOwners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RealEstateOwners_RealEstates_RealEstateId",
                        column: x => x.RealEstateId,
                        principalTable: "RealEstates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RealEstateOwners_RealEstateId",
                table: "RealEstateOwners",
                column: "RealEstateId");

            // ترحيل قيم المالك المفرد الموجودة إلى قائمة الملاك الجديدة قبل حذف العمود،
            // مع قصّ الاسم من الطرفين لمطابقة تطبيع الحفظ في الخدمة.
            migrationBuilder.Sql(@"
                INSERT INTO ""RealEstateOwners"" (""RealEstateId"", ""Name"", ""Order"")
                SELECT ""Id"", TRIM(""Owner""), 0
                FROM ""RealEstates""
                WHERE ""Owner"" IS NOT NULL AND TRIM(""Owner"") != '';");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "RealEstates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "RealEstates",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            // إعادة المالك الأول لكل عقار إلى العمود القديم (أقرب ما يمكن).
            migrationBuilder.Sql(@"
                UPDATE ""RealEstates""
                SET ""Owner"" = (
                    SELECT ""Name"" FROM ""RealEstateOwners""
                    WHERE ""RealEstateOwners"".""RealEstateId"" = ""RealEstates"".""Id""
                    ORDER BY ""Order"" ASC LIMIT 1);");

            migrationBuilder.DropTable(
                name: "RealEstateOwners");
        }
    }
}
