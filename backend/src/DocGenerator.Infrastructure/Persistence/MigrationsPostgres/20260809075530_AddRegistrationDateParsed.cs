using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddRegistrationDateParsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateParsed",
                table: "DocumentRegistrationDates",
                type: "timestamp with time zone",
                nullable: true);

            // دالة آمنة: ترجع التاريخ أو null عند خروج القيم عن المدى (لا ترمي خطأً)،
            // بجولة عكسية للتحقق من صحة اليوم في الشهر.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.docgen_parse_ymd(y integer, m integer, d integer)
                RETURNS date
                LANGUAGE sql
                IMMUTABLE
                AS $$
                    WITH computed AS (
                        SELECT (date '0001-01-01'
                                + (y - 1) * interval '1 year'
                                + (m - 1) * interval '1 month'
                                + (d - 1) * interval '1 day')::date AS cd
                    )
                    SELECT CASE
                        WHEN y BETWEEN 1 AND 9999
                         AND m BETWEEN 1 AND 12
                         AND d BETWEEN 1 AND 31
                         AND extract(year FROM cd) = y
                         AND extract(month FROM cd) = m
                         AND extract(day FROM cd) = d
                        THEN cd
                        ELSE NULL
                    END
                    FROM computed;
                $$;
                """);

            // تعبئة القيم الموجودة من النص الحر بنفس صيغ C# المعتمدة في ActionDateParser
            // (الاستدارة: / أو -؛ yyyy-MM-dd تبقى كما هي؛ سنتان للسنة ذات الخانتين مع
            // عتبة 49 تطابق TwoDigitYearMax في .NET). القيم غير الصالحة تبقى null
            // فيُعتمد تاريخ الإدخال CreatedAt عند الاستعلام.
            migrationBuilder.Sql(
                """
                UPDATE "DocumentRegistrationDates" AS r
                SET "DateParsed" = CASE
                    WHEN t.S ~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' THEN
                        public.docgen_parse_ymd(
                            split_part(t.S, '-', 1)::integer,
                            split_part(t.S, '-', 2)::integer,
                            split_part(t.S, '-', 3)::integer)
                    WHEN t.S ~ '^[0-9]{1,2}-[0-9]{1,2}-[0-9]{4}$' THEN
                        public.docgen_parse_ymd(
                            split_part(t.S, '-', 3)::integer,
                            split_part(t.S, '-', 2)::integer,
                            split_part(t.S, '-', 1)::integer)
                    WHEN t.S ~ '^[0-9]{1,2}-[0-9]{1,2}-[0-9]{2}$' THEN
                        public.docgen_parse_ymd(
                            CASE WHEN split_part(t.S, '-', 3)::integer < 50
                                 THEN 2000 + split_part(t.S, '-', 3)::integer
                                 ELSE 1900 + split_part(t.S, '-', 3)::integer END,
                            split_part(t.S, '-', 2)::integer,
                            split_part(t.S, '-', 1)::integer)
                    ELSE NULL
                END
                FROM (
                    SELECT "DocumentId", replace(trim("Date"), '/', '-') AS S
                    FROM "DocumentRegistrationDates"
                ) t
                WHERE r."DocumentId" = t."DocumentId"
                  AND r."DateParsed" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRegistrationDates_DateParsed",
                table: "DocumentRegistrationDates",
                column: "DateParsed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP FUNCTION IF EXISTS public.docgen_parse_ymd(integer, integer, integer);
                """);

            migrationBuilder.DropIndex(
                name: "IX_DocumentRegistrationDates_DateParsed",
                table: "DocumentRegistrationDates");

            migrationBuilder.DropColumn(
                name: "DateParsed",
                table: "DocumentRegistrationDates");
        }
    }
}
