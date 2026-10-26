using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <summary>
    /// M3 — مرحلة التشديد (Contract) لجدول التنبيهات (PostgreSQL).
    ///
    /// تُطبَّق فقط بعد نجاح <c>M1</c>/<c>M2</c> وتأكيد أن كل النسخ تعمل بكود
    /// <c>PR-2</c> (الخطة §6.3 و§16).
    /// <list type="number">
    /// <item>حارس <c>DO</c> صريح يوقف الهجرة قبل أي تغيير إن وُجدت قيم <c>NULL</c>
    /// في <c>Kind</c>/<c>LastContentChangedAt</c> أو نوع فارغ/أبيض — فلا تُغلَّف
    /// البيانات غير المؤهلة بقيمة صامتة.</item>
    /// <item>جعل <c>Kind</c> و<code>LastContentChangedAt</code> غير قابلين للفراغ،
    /// <b>بلا</b> قيمة افتراضية دائمة على العمودين.</item>
    /// <item>إضافة قيد <c>CK_HeadAlerts_Kind_NotEmpty</c> الذي يرفض النوع الفارغ/الأبيض.</item>
    /// </list>
    /// </summary>
    public partial class HeadAlertTaxonomy_Contract_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // حارس ما قبل التشديد: أي مخالفة تُسقط الهجرة كاملة (والمعاملة تتراجع)
            // بدل أن يحوّل EF قيم NULL إلى Default صامت أثناء ALTER.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    null_kind bigint;
                    null_last_changed bigint;
                    empty_kind bigint;
                BEGIN
                    SELECT COUNT(*) FILTER (WHERE "Kind" IS NULL),
                           COUNT(*) FILTER (WHERE "LastContentChangedAt" IS NULL),
                           COUNT(*) FILTER (WHERE "Kind" IS NOT NULL AND TRIM("Kind") = '')
                      INTO null_kind, null_last_changed, empty_kind
                      FROM "HeadAlerts";

                    IF null_kind > 0 THEN
                        RAISE EXCEPTION 'HeadAlertTaxonomy_Contract_Pg: % صفًا بـ Kind = NULL — نفّذ Backfill أولًا', null_kind;
                    END IF;
                    IF null_last_changed > 0 THEN
                        RAISE EXCEPTION 'HeadAlertTaxonomy_Contract_Pg: % صفًا بـ LastContentChangedAt = NULL — نفّذ Backfill أولًا', null_last_changed;
                    END IF;
                    IF empty_kind > 0 THEN
                        RAISE EXCEPTION 'HeadAlertTaxonomy_Contract_Pg: % صفًا بـ Kind فارغ أو أبيض — عالجه قبل Contract', empty_kind;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastContentChangedAt",
                table: "HeadAlerts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "HeadAlerts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
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
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "HeadAlerts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
