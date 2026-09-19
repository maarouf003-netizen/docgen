using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class BackfillMirrorLawyerPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // تعبئة «المحامي المختص» للملفات المنابة القائمة التي أُنشئت قبل ضبطه
            // عند الاعتماد: الاسم الكامل للمالك (المحامي الموكول) وإلا اسم الدخول —
            // بنفس صيغة الإنشاء العادي. تُمسّ فقط صفوف المناب ذات الاسم المفقود،
            // فلا تُمسّ الملفات العادية ولا المنقولات اللاحقة، والهجرة آمنة الإعادة.
            migrationBuilder.Sql(
                """
                UPDATE "Documents"
                SET "Lawyer" = (
                    SELECT CASE
                        WHEN "Users"."FullName" IS NULL OR trim("Users"."FullName") = '' THEN "Users"."Username"
                        ELSE "Users"."FullName"
                    END
                    FROM "Users"
                    WHERE "Users"."Id" = "Documents"."CreatedById"
                )
                WHERE "SourceDelegationId" IS NOT NULL
                  AND ("Lawyer" IS NULL OR "Lawyer" = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // لا تراجع للبيانات: يستحيل تمييز القيم المعبأة هنا عن أسماء ضُبطت
            // بعد الإصلاح، فالتصفير سيدمر بيانات صحيحة — يُترك التراجع فارغًا عمدًا.
        }
    }
}
