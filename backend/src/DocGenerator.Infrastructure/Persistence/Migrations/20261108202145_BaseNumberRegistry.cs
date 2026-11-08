using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BaseNumberRegistry : Migration
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
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionCircuitId",
                table: "DocumentBaseNumbers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "DocumentBaseNumbers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumberKey",
                table: "DocumentBaseNumbers",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "DocumentBaseNumbers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeKey",
                table: "DocumentBaseNumbers",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UpdatedById",
                table: "DocumentBaseNumbers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "DocumentBaseNumbers",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "DocumentBaseNumbers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoidedById",
                table: "DocumentBaseNumbers",
                type: "INTEGER",
                nullable: true);

            // ── تعبئة v2: بوابات توقف صريحة قبل أي نسبة صامتة، ثم تعبئة الصفوف القائمة
            // وإنشاء صف القيد لكل ملف مرقّم (وللمحذوف صف ملغى توثيقي). الترتيب إلزامي:
            // الأعمدة ← البوابات ← التعبئة ← صفوف القيد ← بوابة التكرار ← إلزامية الأعمدة ← الفهرس.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE IF NOT EXISTS "BaseNumberRegistry_blocked_document_without_circuit"
                    ("x" INTEGER NOT NULL CONSTRAINT "BaseNumberRegistry_document_without_circuit" CHECK ("x" = 0));
                DELETE FROM "BaseNumberRegistry_blocked_document_without_circuit";
                INSERT INTO "BaseNumberRegistry_blocked_document_without_circuit" ("x")
                SELECT 1
                FROM "DocumentBaseNumbers" b
                JOIN "Documents" d ON d."Id" = b."DocumentId"
                WHERE d."ExecutionCircuitId" IS NULL
                LIMIT 1;
                INSERT INTO "BaseNumberRegistry_blocked_document_without_circuit" ("x")
                SELECT 1
                FROM "Documents" d
                WHERE d."ExecutionCircuitId" IS NULL
                  AND d."FileNumber" IS NOT NULL AND trim(d."FileNumber") <> ''
                LIMIT 1;

                CREATE TEMP TABLE IF NOT EXISTS "BaseNumberRegistry_blocked_edit_circuit_switch_manual_review"
                    ("x" INTEGER NOT NULL CONSTRAINT "BaseNumberRegistry_edit_circuit_switch_manual_review" CHECK ("x" = 0));
                DELETE FROM "BaseNumberRegistry_blocked_edit_circuit_switch_manual_review";
                INSERT INTO "BaseNumberRegistry_blocked_edit_circuit_switch_manual_review" ("x")
                SELECT 1
                FROM "DocumentBaseNumbers" b
                JOIN (
                    SELECT "DocumentId", MIN("CreatedAt") AS "FirstAt"
                    FROM "DocumentOccurrences"
                    WHERE "OccurrenceType" = 'circuit-referred'
                      AND "Details" LIKE '%"via":"edit"%'
                    GROUP BY "DocumentId"
                ) s ON s."DocumentId" = b."DocumentId"
                WHERE b."CreatedAt" < s."FirstAt"
                LIMIT 1;

                CREATE TEMP TABLE IF NOT EXISTS "BaseNumberRegistry_blocked_invalid_year"
                    ("x" INTEGER NOT NULL CONSTRAINT "BaseNumberRegistry_invalid_year" CHECK ("x" = 0));
                DELETE FROM "BaseNumberRegistry_blocked_invalid_year";
                INSERT INTO "BaseNumberRegistry_blocked_invalid_year" ("x")
                SELECT 1
                FROM "Documents" d
                WHERE d."FileNumber" IS NOT NULL AND trim(d."FileNumber") <> ''
                  AND (d."FileYear" IS NULL OR trim(d."FileYear") = ''
                       OR CAST(d."FileYear" AS INTEGER) < 1900
                       OR CAST(d."FileYear" AS INTEGER) > CAST(strftime('%Y','now') AS INTEGER))
                LIMIT 1;
                INSERT INTO "BaseNumberRegistry_blocked_invalid_year" ("x")
                SELECT 1 FROM "DocumentBaseNumbers"
                WHERE "Year" < 1900 OR "Year" > CAST(strftime('%Y','now') AS INTEGER)
                LIMIT 1;

                UPDATE "DocumentBaseNumbers"
                SET "ExecutionCircuitId" = (
                        SELECT d."ExecutionCircuitId" FROM "Documents" d
                        WHERE d."Id" = "DocumentBaseNumbers"."DocumentId"),
                    "CircuitName" = COALESCE((
                        SELECT NULLIF(trim(d."Court"), '') FROM "Documents" d
                        WHERE d."Id" = "DocumentBaseNumbers"."DocumentId"), '—'),
                    "FileType" = (
                        SELECT d."FileType" FROM "Documents" d
                        WHERE d."Id" = "DocumentBaseNumbers"."DocumentId"),
                    "Source" = 'rotation',
                    "VoidedAt" = CASE WHEN (
                        SELECT d."IsDeleted" FROM "Documents" d
                        WHERE d."Id" = "DocumentBaseNumbers"."DocumentId") = 1
                        THEN CURRENT_TIMESTAMP ELSE NULL END,
                    "VoidReason" = CASE WHEN (
                        SELECT d."IsDeleted" FROM "Documents" d
                        WHERE d."Id" = "DocumentBaseNumbers"."DocumentId") = 1
                        THEN 'document-deleted' ELSE NULL END,
                    "VoidedById" = CASE WHEN (
                        SELECT d."IsDeleted" FROM "Documents" d
                        WHERE d."Id" = "DocumentBaseNumbers"."DocumentId") = 1
                        THEN (SELECT d."CreatedById" FROM "Documents" d
                              WHERE d."Id" = "DocumentBaseNumbers"."DocumentId") ELSE NULL END;

                UPDATE "DocumentBaseNumbers"
                SET "TypeKey" = lower(trim(replace(replace(replace(replace(replace(
                        COALESCE("FileType", ''), 'أ', 'ا'), 'إ', 'ا'), 'آ', 'ا'), 'ة', 'ه'), 'ى', 'ي')));

                UPDATE "DocumentBaseNumbers"
                SET "NumberKey" = replace(replace(
                        replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(
                        replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(
                        trim("BaseNumber"),
                        '٠','0'),'١','1'),'٢','2'),'٣','3'),'٤','4'),
                        '٥','5'),'٦','6'),'٧','7'),'٨','8'),'٩','9'),
                        '۰','0'),'۱','1'),'۲','2'),'۳','3'),'۴','4'),
                        '۵','5'),'۶','6'),'۷','7'),'۸','8'),'۹','9'),
                        ' ', ''), char(9), '');

                INSERT INTO "DocumentBaseNumbers"
                    ("DocumentId", "ExecutionCircuitId", "CircuitName", "Year", "FileType",
                     "TypeKey", "BaseNumber", "NumberKey", "Source", "CreatedById",
                     "CreatedAt", "UpdatedAt", "VoidedAt", "VoidReason", "VoidedById")
                SELECT d."Id", d."ExecutionCircuitId",
                       COALESCE(NULLIF(trim(d."Court"), ''), '—'),
                       CAST(d."FileYear" AS INTEGER), d."FileType",
                       lower(trim(replace(replace(replace(replace(replace(
                           COALESCE(d."FileType", ''), 'أ', 'ا'), 'إ', 'ا'), 'آ', 'ا'), 'ة', 'ه'), 'ى', 'ي'))),
                       d."FileNumber",
                       replace(replace(
                           replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(
                           replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(
                           trim(d."FileNumber"),
                           '٠','0'),'١','1'),'٢','2'),'٣','3'),'٤','4'),
                           '٥','5'),'٦','6'),'٧','7'),'٨','8'),'٩','9'),
                           '۰','0'),'۱','1'),'۲','2'),'۳','3'),'۴','4'),
                           '۵','5'),'۶','6'),'۷','7'),'۸','8'),'۹','9'),
                           ' ', ''), char(9), ''),
                       'registration', d."CreatedById", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP,
                       NULL, NULL, NULL
                FROM "Documents" d
                WHERE d."IsDeleted" = 0
                  AND d."ExecutionCircuitId" IS NOT NULL
                  AND d."FileNumber" IS NOT NULL AND trim(d."FileNumber") <> ''
                  AND d."FileYear" IS NOT NULL AND trim(d."FileYear") <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "DocumentBaseNumbers" b
                      WHERE b."DocumentId" = d."Id"
                        AND b."ExecutionCircuitId" = d."ExecutionCircuitId"
                        AND b."Year" = CAST(d."FileYear" AS INTEGER)
                        AND b."Source" = 'registration'
                        AND b."VoidedAt" IS NULL);

                INSERT INTO "DocumentBaseNumbers"
                    ("DocumentId", "ExecutionCircuitId", "CircuitName", "Year", "FileType",
                     "TypeKey", "BaseNumber", "NumberKey", "Source", "CreatedById",
                     "CreatedAt", "UpdatedAt", "VoidedAt", "VoidReason", "VoidedById")
                SELECT d."Id", d."ExecutionCircuitId",
                       COALESCE(NULLIF(trim(d."Court"), ''), '—'),
                       CAST(d."FileYear" AS INTEGER), d."FileType",
                       lower(trim(replace(replace(replace(replace(replace(
                           COALESCE(d."FileType", ''), 'أ', 'ا'), 'إ', 'ا'), 'آ', 'ا'), 'ة', 'ه'), 'ى', 'ي'))),
                       d."FileNumber",
                       replace(replace(
                           replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(
                           replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(
                           trim(d."FileNumber"),
                           '٠','0'),'١','1'),'٢','2'),'٣','3'),'٤','4'),
                           '٥','5'),'٦','6'),'٧','7'),'٨','8'),'٩','9'),
                           '۰','0'),'۱','1'),'۲','2'),'۳','3'),'۴','4'),
                           '۵','5'),'۶','6'),'۷','7'),'۸','8'),'۹','9'),
                           ' ', ''), char(9), ''),
                       'registration', d."CreatedById", CURRENT_TIMESTAMP, CURRENT_TIMESTAMP,
                       COALESCE(d."DeletedAt", CURRENT_TIMESTAMP), 'document-deleted', d."CreatedById"
                FROM "Documents" d
                WHERE d."IsDeleted" = 1
                  AND d."ExecutionCircuitId" IS NOT NULL
                  AND d."FileNumber" IS NOT NULL AND trim(d."FileNumber") <> ''
                  AND d."FileYear" IS NOT NULL AND trim(d."FileYear") <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "DocumentBaseNumbers" b
                      WHERE b."DocumentId" = d."Id"
                        AND b."ExecutionCircuitId" = d."ExecutionCircuitId"
                        AND b."Year" = CAST(d."FileYear" AS INTEGER)
                        AND b."Source" = 'registration');

                CREATE TEMP TABLE IF NOT EXISTS "BaseNumberRegistry_blocked_duplicate_keys_manual_review"
                    ("x" INTEGER NOT NULL CONSTRAINT "BaseNumberRegistry_duplicate_keys_manual_review" CHECK ("x" = 0));
                DELETE FROM "BaseNumberRegistry_blocked_duplicate_keys_manual_review";
                INSERT INTO "BaseNumberRegistry_blocked_duplicate_keys_manual_review" ("x")
                SELECT 1
                FROM "DocumentBaseNumbers"
                WHERE "VoidedAt" IS NULL
                GROUP BY "ExecutionCircuitId", "TypeKey", "Year", "NumberKey"
                HAVING COUNT(*) > 1
                LIMIT 1;
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
