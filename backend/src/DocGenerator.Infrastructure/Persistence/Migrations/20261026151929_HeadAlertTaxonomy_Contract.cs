using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// M3 — مرحلة التشديد (Contract) لجدول التنبيهات (SQLite).
    ///
    /// تُطبَّق فقط بعد نجاح <c>M1</c>/<c>M2</c> وتأكيد أن كل النسخ تعمل بكود
    /// <c>PR-2</c> (الخطة §6.3 و§16).
    /// <list type="number">
    /// <item>حارس صريح يوقف الهجرة قبل أي تغيير إن وُجدت قيم <c>NULL</c> في
    /// <c>Kind</c>/<c>LastContentChangedAt</c> أو نوع فارغ/أبيض — فلا تُغلَّف
    /// البيانات غير المؤهلة بقيمة صامتة.</item>
    /// <item>جعل <c>Kind</c> و<code>LastContentChangedAt</code> غير قابلين للفراغ،
    /// <b>بلا</b> قيمة افتراضية دائمة على العمودين.</item>
    /// <item>إضافة قيد <c>CK_HeadAlerts_Kind_NotEmpty</c> الذي يرفض النوع الفارغ/الأبيض.</item>
    /// </list>
    ///
    /// ملاحظة SQLite: إعادة بناء الجدول التي يولّدها EF تحافظ على الفهارس
    /// (بما فيها الفهرس الفريد الجزئي) والمفاتيح الأجنبية، ويغطيها اختبار
    /// <c>Migration_Contract_*</c>.
    /// </summary>
    public partial class HeadAlertTaxonomy_Contract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // حارس ما قبل التشديد: أي مخالفة تُسقط الهجرة كاملة (والمعاملة تتراجع)
            // بدل أن يحوّل EF قيم NULL إلى Default صامت أثناء إعادة بناء الجدول.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "_HeadAlertContractGuard" (
                    "NullKind" INTEGER NOT NULL
                        CONSTRAINT "CK_HeadAlertContractGuard_NullKind" CHECK ("NullKind" = 0),
                    "NullLastContentChangedAt" INTEGER NOT NULL
                        CONSTRAINT "CK_HeadAlertContractGuard_NullLastChanged" CHECK ("NullLastContentChangedAt" = 0),
                    "EmptyKind" INTEGER NOT NULL
                        CONSTRAINT "CK_HeadAlertContractGuard_EmptyKind" CHECK ("EmptyKind" = 0)
                );

                INSERT INTO "_HeadAlertContractGuard" ("NullKind", "NullLastContentChangedAt", "EmptyKind")
                SELECT COALESCE(SUM("Kind" IS NULL), 0),
                       COALESCE(SUM("LastContentChangedAt" IS NULL), 0),
                       COALESCE(SUM("Kind" IS NOT NULL AND TRIM("Kind") = ''), 0)
                FROM "HeadAlerts";

                DROP TABLE "_HeadAlertContractGuard";
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastContentChangedAt",
                table: "HeadAlerts",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "HeadAlerts",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_HeadAlerts_Kind_NotEmpty",
                table: "HeadAlerts",
                sql: "TRIM(\"Kind\") <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // التراجع التطبيقي فقط: يعيد العمودين nullable ويسقط القيد، ولا يعيد
            // أي قيمة إلى NULL ولا يمسّ الصفوف (الخطة §16.2).
            migrationBuilder.DropCheckConstraint(
                name: "CK_HeadAlerts_Kind_NotEmpty",
                table: "HeadAlerts");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastContentChangedAt",
                table: "HeadAlerts",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "HeadAlerts",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);
        }
    }
}
