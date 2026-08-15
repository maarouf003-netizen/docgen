using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
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
                type: "datetime2",
                nullable: true);

            // تعبئة القيم الموجودة من النص الحر بنفس صيغ C# المعتمدة في ActionDateParser
            // (الاستدارة: / أو -؛ yyyy-MM-dd تبقى كما هي؛ سنتان للسنة ذات الخانتين مع
            // عتبة 49 تطابق TwoDigitYearMax في .NET). القيم غير الصالحة أو خارج المدى
            // (مثل 31/02) تبقى null فيُعتمد تاريخ الإدخال CreatedAt عند الاستعلام.
            // قيد معروف: البديل المرن (DateTime.TryParse بالثقافة الحالية) غير قابل للمحاكاة
            // هنا، فأي صيغة حرة خارج السبع تُترك null بنفس سلوك الاستعلام أعلاه.
            migrationBuilder.Sql(
                """
                UPDATE "DocumentRegistrationDates"
                SET "DateParsed" = (
                    SELECT t4.ValidCandidate
                    FROM (
                        SELECT t3."DocumentId",
                               CASE
                                   WHEN t3.Candidate IS NOT NULL
                                    AND t3.Candidate GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'
                                    AND CAST(substr(t3.Candidate, 1, 4) AS INTEGER) BETWEEN 1 AND 9999
                                    AND date(julianday(t3.Candidate)) = t3.Candidate
                                   THEN t3.Candidate || ' 00:00:00'
                                   ELSE NULL
                               END AS ValidCandidate
                        FROM (
                            SELECT t2."DocumentId",
                                   CASE
                                       WHEN t2.S GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]' THEN t2.S
                                       WHEN (t2.C1 GLOB '[0-9]' OR t2.C1 GLOB '[0-9][0-9]')
                                        AND (t2.C2 GLOB '[0-9]' OR t2.C2 GLOB '[0-9][0-9]')
                                        AND t2.C3 GLOB '[0-9][0-9][0-9][0-9]' THEN
                                           t2.C3 || '-' || substr('0' || t2.C2, -2) || '-' || substr('0' || t2.C1, -2)
                                       WHEN (t2.C1 GLOB '[0-9]' OR t2.C1 GLOB '[0-9][0-9]')
                                        AND (t2.C2 GLOB '[0-9]' OR t2.C2 GLOB '[0-9][0-9]')
                                        AND t2.C3 GLOB '[0-9][0-9]' THEN
                                           (CASE WHEN CAST(t2.C3 AS INTEGER) < 50 THEN '20' || t2.C3 ELSE '19' || t2.C3 END)
                                           || '-' || substr('0' || t2.C2, -2) || '-' || substr('0' || t2.C1, -2)
                                       ELSE NULL
                                   END AS Candidate
                            FROM (
                                SELECT t1."DocumentId", t1.S, t1.C1,
                                       substr(t1.R, 1, instr(t1.R, '-') - 1) AS C2,
                                       substr(t1.R, instr(t1.R, '-') + 1) AS C3
                                FROM (
                                    SELECT "DocumentId",
                                           replace(trim("Date"), '/', '-') AS S,
                                           substr(replace(trim("Date"), '/', '-'), 1, instr(replace(trim("Date"), '/', '-'), '-') - 1) AS C1,
                                           substr(replace(trim("Date"), '/', '-'), instr(replace(trim("Date"), '/', '-'), '-') + 1) AS R
                                    FROM "DocumentRegistrationDates"
                                ) t1
                            ) t2
                        ) t3
                    ) t4
                    WHERE t4."DocumentId" = "DocumentRegistrationDates"."DocumentId"
                )
                WHERE "DateParsed" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRegistrationDates_DateParsed",
                table: "DocumentRegistrationDates",
                column: "DateParsed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentRegistrationDates_DateParsed",
                table: "DocumentRegistrationDates");

            migrationBuilder.DropColumn(
                name: "DateParsed",
                table: "DocumentRegistrationDates");
        }
    }
}
