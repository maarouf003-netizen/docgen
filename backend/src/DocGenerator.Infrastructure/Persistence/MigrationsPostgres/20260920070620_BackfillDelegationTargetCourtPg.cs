using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class BackfillDelegationTargetCourtPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // تعبئة دائرة الملفات المنابة القائمة بالدائرة المنابة المسجَّلة فيها
            // (كانت تُنسخ خطأً من دائرة المنيب): تُمسّ فقط صفوف المناب ذات
            // الإنابة المرتبطة بدائرة منابة غير فارغة، فلا تُمسّ الملفات العادية
            // ولا المنقولات اللاحقة، والهجرة آمنة الإعادة.
            migrationBuilder.Sql(
                """
                UPDATE "Documents"
                SET "Court" = (
                    SELECT "DocumentDelegations"."DelegatedCourt"
                    FROM "DocumentDelegations"
                    WHERE "DocumentDelegations"."Id" = "Documents"."SourceDelegationId"
                )
                WHERE "SourceDelegationId" IS NOT NULL
                  AND EXISTS (
                    SELECT 1
                    FROM "DocumentDelegations"
                    WHERE "DocumentDelegations"."Id" = "Documents"."SourceDelegationId"
                      AND "DocumentDelegations"."DelegatedCourt" IS NOT NULL
                      AND trim("DocumentDelegations"."DelegatedCourt") <> ''
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // لا تراجع للبيانات: يستحيل تمييز القيم المعبأة هنا عن دوائر ضُبطت
            // بعد الإصلاح، فالتصفير سيدمر بيانات صحيحة — يُترك التراجع فارغًا عمدًا.
        }
    }
}
