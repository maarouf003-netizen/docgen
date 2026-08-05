using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequiredDocumentOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // منح الملفات اليتيمة (بلا محامٍ مختص) لأدنى مستخدم موجود — عادةً المشرف العام
            // (admin) المُنشأ أولاً — قبل فرض NOT NULL، حتى لا تفشل المهاجرة ولا تُسند
            // أرقاماً وهمية. وإن كانت القاعدة فارغة تماماً من المستخدمين وبقيت صفوف NULL،
            // يفشل فرض NOT NULL بصوت عالٍ ليُتخذ قرار بشري، لا صمتاً وفقداناً للبيانات.
            migrationBuilder.Sql(
                """
                UPDATE "Documents"
                SET "CreatedById" = (SELECT MIN("Id") FROM "Users")
                WHERE "CreatedById" IS NULL
                  AND (SELECT COUNT(*) FROM "Users") > 0;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedById",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CreatedById",
                table: "Documents",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
