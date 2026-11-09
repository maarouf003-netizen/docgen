using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class BaseNumberRegistry_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentBaseNumbers_Documents_DocumentId",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_DocumentId",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year",
                table: "DocumentBaseNumbers");

            migrationBuilder.AddColumn<string>(
                name: "CircuitName",
                table: "DocumentBaseNumbers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionCircuitId",
                table: "DocumentBaseNumbers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "DocumentBaseNumbers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumberKey",
                table: "DocumentBaseNumbers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "DocumentBaseNumbers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeKey",
                table: "DocumentBaseNumbers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UpdatedById",
                table: "DocumentBaseNumbers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "DocumentBaseNumbers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "DocumentBaseNumbers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoidedById",
                table: "DocumentBaseNumbers",
                type: "integer",
                nullable: true);

            // ── تعبئة v2: بوابات توقف صريحة قبل أي نسبة صامتة، ثم تعبئة الصفوف القائمة
            // وإنشاء صف القيد لكل ملف مرقّم (وللمحذوف صف ملغى توثيقي). الترتيب إلزامي:
            // الأعمدة ← البوابات ← التعبئة ← صفوف القيد ← بوابة التكرار ← إلزامية الأعمدة ← الفهرس.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "DocumentBaseNumbers" b
                        JOIN "Documents" d ON d."Id" = b."DocumentId"
                        WHERE d."ExecutionCircuitId" IS NULL)
                       OR EXISTS (
                        SELECT 1 FROM "Documents" d
                        WHERE d."ExecutionCircuitId" IS NULL
                          AND d."FileNumber" IS NOT NULL AND btrim(d."FileNumber") <> '')
                    THEN
                        RAISE EXCEPTION 'BaseNumberRegistry: يوجد ملف مرقّم/صف رقم أساس بلا دائرة — عالجه يدويًا قبل الهجرة';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM "DocumentBaseNumbers" b
                        JOIN (
                            SELECT "DocumentId", MIN("CreatedAt") AS "FirstAt"
                            FROM "DocumentOccurrences"
                            WHERE "OccurrenceType" = 'circuit-referred'
                              AND "Details" LIKE '%"via":"edit"%'
                            GROUP BY "DocumentId"
                        ) s ON s."DocumentId" = b."DocumentId"
                        WHERE b."CreatedAt" < s."FirstAt")
                    THEN
                        RAISE EXCEPTION 'BaseNumberRegistry: صفوف أقدم من تبديل دائرة من التعديل — تحتاج مراجعة يدوية (R3)';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM "Documents" d
                        WHERE d."FileNumber" IS NOT NULL AND btrim(d."FileNumber") <> ''
                          AND (d."FileYear" IS NULL OR btrim(d."FileYear") = ''
                               OR (CASE WHEN d."FileYear" ~ '^[0-9]+$' THEN d."FileYear"::int ELSE 0 END) < 1900
                               OR (CASE WHEN d."FileYear" ~ '^[0-9]+$' THEN d."FileYear"::int ELSE 0 END) > EXTRACT(YEAR FROM CURRENT_DATE)::int))
                    THEN
                        RAISE EXCEPTION 'BaseNumberRegistry: ملف مرقّم بسنة قيد غير صالحة (1900..السنة الحالية) — عالجه يدويًا';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM "DocumentBaseNumbers"
                        WHERE "Year" < 1900 OR "Year" > EXTRACT(YEAR FROM CURRENT_DATE)::int)
                    THEN
                        RAISE EXCEPTION 'BaseNumberRegistry: صف رقم أساس بسنة خارج 1900..السنة الحالية — عالجه يدويًا';
                    END IF;
                END $$;

                UPDATE "DocumentBaseNumbers" b
                SET "ExecutionCircuitId" = d."ExecutionCircuitId",
                    "CircuitName" = COALESCE(NULLIF(btrim(d."Court"), ''), '—'),
                    "FileType" = d."FileType",
                    "Source" = 'rotation',
                    "VoidedAt" = CASE WHEN d."IsDeleted" THEN COALESCE(d."DeletedAt", now()) ELSE NULL END,
                    "VoidReason" = CASE WHEN d."IsDeleted" THEN 'document-deleted' ELSE NULL END,
                    "VoidedById" = CASE WHEN d."IsDeleted" THEN d."CreatedById" ELSE NULL END
                FROM "Documents" d
                WHERE d."Id" = b."DocumentId";


                CREATE OR REPLACE FUNCTION pg_temp.docgen_normalize_type(value text)
                RETURNS text AS $$
                    SELECT lower(btrim(regexp_replace(
                        regexp_replace(
                            regexp_replace(
                                translate(coalesce(value, ''), 'أإآةى', 'اااهي'),
                                '[ً-ٰ]', '', 'g'),
                            '[ۖ-ۭ]', '', 'g'),
                        'ـ', '', 'g'),
                        '\s+', ' ', 'g')));
                $$ LANGUAGE SQL IMMUTABLE;

                UPDATE "DocumentBaseNumbers"
                SET "TypeKey" = pg_temp.docgen_normalize_type(COALESCE("FileType", ''));

                UPDATE "DocumentBaseNumbers"
                SET "NumberKey" = regexp_replace(
                    translate(btrim("BaseNumber"),
                        '٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹',
                        '01234567890123456789'),
                    '\s', '', 'g');

                INSERT INTO "DocumentBaseNumbers"
                    ("DocumentId", "ExecutionCircuitId", "CircuitName", "Year", "FileType",
                     "TypeKey", "BaseNumber", "NumberKey", "Source", "CreatedById",
                     "CreatedAt", "UpdatedAt", "VoidedAt", "VoidReason", "VoidedById")
                SELECT d."Id", d."ExecutionCircuitId",
                       COALESCE(NULLIF(btrim(d."Court"), ''), '—'),
                       d."FileYear"::int, d."FileType",
                       pg_temp.docgen_normalize_type(COALESCE(d."FileType", '')),
                       d."FileNumber",
                       regexp_replace(translate(btrim(d."FileNumber"),
                           '٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹',
                           '01234567890123456789'), '\s', '', 'g'),
                       'registration', d."CreatedById", d."CreatedAt", now(), NULL, NULL, NULL
                FROM "Documents" d
                WHERE d."IsDeleted" = false
                  AND d."ExecutionCircuitId" IS NOT NULL
                  AND d."FileNumber" IS NOT NULL AND btrim(d."FileNumber") <> ''
                  AND d."FileYear" IS NOT NULL AND btrim(d."FileYear") <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "DocumentBaseNumbers" b
                      WHERE b."DocumentId" = d."Id"
                        AND b."ExecutionCircuitId" = d."ExecutionCircuitId"
                        AND b."Year" = d."FileYear"::int
                        AND b."Source" = 'registration'
                        AND b."VoidedAt" IS NULL);

                INSERT INTO "DocumentBaseNumbers"
                    ("DocumentId", "ExecutionCircuitId", "CircuitName", "Year", "FileType",
                     "TypeKey", "BaseNumber", "NumberKey", "Source", "CreatedById",
                     "CreatedAt", "UpdatedAt", "VoidedAt", "VoidReason", "VoidedById")
                SELECT d."Id", d."ExecutionCircuitId",
                       COALESCE(NULLIF(btrim(d."Court"), ''), '—'),
                       d."FileYear"::int, d."FileType",
                       pg_temp.docgen_normalize_type(COALESCE(d."FileType", '')),
                       d."FileNumber",
                       regexp_replace(translate(btrim(d."FileNumber"),
                           '٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹',
                           '01234567890123456789'), '\s', '', 'g'),
                       'registration', d."CreatedById", d."CreatedAt", now(),
                       COALESCE(d."DeletedAt", now()), 'document-deleted', d."CreatedById"
                FROM "Documents" d
                WHERE d."IsDeleted" = true
                  AND d."ExecutionCircuitId" IS NOT NULL
                  AND d."FileNumber" IS NOT NULL AND btrim(d."FileNumber") <> ''
                  AND d."FileYear" IS NOT NULL AND btrim(d."FileYear") <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "DocumentBaseNumbers" b
                      WHERE b."DocumentId" = d."Id"
                        AND b."ExecutionCircuitId" = d."ExecutionCircuitId"
                        AND b."Year" = d."FileYear"::int
                        AND b."Source" = 'registration');

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "DocumentBaseNumbers"
                        WHERE "VoidedAt" IS NULL
                        GROUP BY "ExecutionCircuitId", "TypeKey", "Year", "NumberKey"
                        HAVING COUNT(*) > 1)
                    THEN
                        RAISE EXCEPTION 'BaseNumberRegistry: تكرار في مفاتيح أرقام الأساس بعد التطبيع — عالجه يدويًا قبل الهجرة';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year_CreatedAt",
                table: "DocumentBaseNumbers",
                columns: new[] { "DocumentId", "Year", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_ExecutionCircuitId_Year",
                table: "DocumentBaseNumbers",
                columns: new[] { "ExecutionCircuitId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_UpdatedById",
                table: "DocumentBaseNumbers",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_VoidedById",
                table: "DocumentBaseNumbers",
                column: "VoidedById");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentBaseNumbers_Active",
                table: "DocumentBaseNumbers",
                columns: new[] { "ExecutionCircuitId", "TypeKey", "Year", "NumberKey" },
                unique: true,
                filter: "\"VoidedAt\" IS NULL");

            // V-18 على مستوى قاعدة PostgreSQL: لا ملف مرقّم بلا دائرة (المسودة/بلا رقم مسموحة).
            migrationBuilder.Sql("""
                ALTER TABLE "Documents"
                ADD CONSTRAINT "CK_Documents_FileNumberRequiresCircuit"
                CHECK ("ExecutionCircuitId" IS NOT NULL
                    OR "FileNumber" IS NULL
                    OR btrim("FileNumber") = '');
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Documents_DocumentId",
                table: "DocumentBaseNumbers",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_ExecutionCircuits_ExecutionCircuitId",
                table: "DocumentBaseNumbers",
                column: "ExecutionCircuitId",
                principalTable: "ExecutionCircuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Users_UpdatedById",
                table: "DocumentBaseNumbers",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Users_VoidedById",
                table: "DocumentBaseNumbers",
                column: "VoidedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Documents\" DROP CONSTRAINT IF EXISTS \"CK_Documents_FileNumberRequiresCircuit\";");

            // إزالة صفوف القيد التي أنشأتها التعبئة (الصفوف القديمة وُسمت rotation).
            migrationBuilder.Sql("DELETE FROM \"DocumentBaseNumbers\" WHERE \"Source\" = 'registration';");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentBaseNumbers_Documents_DocumentId",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentBaseNumbers_ExecutionCircuits_ExecutionCircuitId",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentBaseNumbers_Users_UpdatedById",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentBaseNumbers_Users_VoidedById",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year_CreatedAt",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_ExecutionCircuitId_Year",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_UpdatedById",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "IX_DocumentBaseNumbers_VoidedById",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropIndex(
                name: "UX_DocumentBaseNumbers_Active",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "CircuitName",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "ExecutionCircuitId",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "NumberKey",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "TypeKey",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "DocumentBaseNumbers");

            migrationBuilder.DropColumn(
                name: "VoidedById",
                table: "DocumentBaseNumbers");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId",
                table: "DocumentBaseNumbers",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year",
                table: "DocumentBaseNumbers",
                columns: new[] { "DocumentId", "Year" });

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Documents_DocumentId",
                table: "DocumentBaseNumbers",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
