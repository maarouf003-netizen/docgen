using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HeadAlertTaxonomy_Backfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_TargetLawyerId",
                table: "HeadAlerts");

            // M2 — backfill: الصفوف القديمة فقط إلى legacy مع زمن المحتوى.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "Kind" = 'legacy',
                    "LastContentChangedAt" = COALESCE("LastContentChangedAt", "CreatedAt")
                WHERE "Kind" IS NULL OR TRIM("Kind") = '';
                """);

            // استخراج دائرة التنبيه المعلق من المستند أو من نص الرسالة قبل التصنيف.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "ExecutionCircuitId" = COALESCE(
                    (SELECT d."ExecutionCircuitId"
                     FROM "Documents" d
                     WHERE d."Id" = "HeadAlerts"."DocumentId"
                       AND d."ExecutionCircuitId" IS NOT NULL),
                    (SELECT MIN(c."Id")
                     FROM "ExecutionCircuits" c
                     WHERE c."BranchId" = "HeadAlerts"."BranchId"
                       AND "HeadAlerts"."Message" =
                           'أحال لك رئيس القسم ملفات من دائرة ' || c."Name" || ' — يرجى تحديث معلوماتها'))
                WHERE NOT "IsArchived"
                  AND "Kind" = 'legacy'
                  AND "TargetType" = 2
                  AND "Message" LIKE 'أحال لك رئيس القسم ملفات من دائرة %'
                  AND "ExecutionCircuitId" IS NULL;
                """);

            // تصنيف التدفقات المفتوحة القديمة وفق الأنماط المعروفة فقط.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "Kind" = CASE
                    WHEN "Message" LIKE 'تنبيه من رئيس القسم:%'
                      OR "Message" LIKE 'تنبيه من رئيس الشعبة:%' THEN "Kind"
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'بانتظار اعتماد الإنابة%' THEN 'delegation_pending_approval'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'الإنابة من اختصاص%' THEN 'delegation_sent_to_subhead'
                    WHEN "DelegationId" IS NOT NULL
                      AND ("Message" LIKE 'بانتظار الإتمام%'
                        OR "Message" LIKE 'بانتظار إتمام الإنابة%') THEN 'delegation_pending_completion'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'ورد كتاب تريث%' THEN 'delegation_mirror_deferred'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'يرجى متابعة السير%' THEN 'delegation_mirror_returned'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'يرجى وقف إجراءات%' THEN 'delegation_mirror_settled'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'استُرد ملفك%' THEN 'delegation_mirror_fully_forcibly'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'تم تحديث نسخة الملف المناب%' THEN 'delegation_mirror_updated'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'تم تعديل بيانات الملف المناب%' THEN 'delegation_mirror_local_addition'
                    WHEN "DelegationId" IS NOT NULL
                      AND "Message" LIKE 'تغيّرت حالة الملف%' THEN 'delegation_mirror_status_changed'
                    WHEN "TargetType" = 1 AND "DocumentId" IS NOT NULL
                      AND "Message" LIKE 'أحال إليك رئيس القسم ملف إنابة%' THEN 'delegation_assigned_to_lawyer'
                    WHEN "TargetType" = 1 AND "DocumentId" IS NOT NULL
                      AND "Message" LIKE 'رُفضت الإنابة%' THEN 'delegation_rejected_wrong_court'
                    WHEN "TargetType" = 1 AND "DocumentId" IS NOT NULL
                      AND "Message" LIKE 'أُعيدت الإنابة المسطرة%' THEN 'delegation_completed_returned'
                    WHEN "AppealId" IS NOT NULL
                      AND "Message" LIKE 'يرجى التفضل بإسناد الاستئناف%' THEN 'appeal_referral_to_head'
                    WHEN "AppealId" IS NOT NULL
                      AND "Message" LIKE 'أُعيد الاستئناف%' THEN 'appeal_returned_to_head'
                    WHEN "AppealId" IS NOT NULL
                      AND "Message" LIKE 'وقع استئناف بملف%' THEN 'appeal_pending_to_owner'
                    WHEN "AppealId" IS NOT NULL
                      AND "Message" LIKE 'أحال إليك رئيس القسم استئناف%' THEN 'appeal_assigned_to_lawyer'
                    WHEN "AppealId" IS NOT NULL
                      AND "Message" LIKE 'أصبح استئناف قرار رئيس التنفيذ%' THEN 'appeal_final_to_base_lawyer'
                    WHEN "ReviewLetterId" IS NOT NULL
                      AND "Message" LIKE '%ردّ على كتاب مطالعتك%' THEN 'review_letter_reply'
                    WHEN "TargetType" = 2
                      AND "ExecutionCircuitId" IS NOT NULL
                      AND "Message" LIKE 'أحال لك رئيس القسم ملفات من دائرة %' THEN 'circuit_pending_documents'
                    WHEN "PublicEntityId" IS NOT NULL
                      AND "TargetLawyerId" IS NOT NULL
                      AND "Message" LIKE 'تم تعديل اسم الجهة العامة التي أدخلتها%'
                      THEN 'entity_rename_notice_to_creator'
                    ELSE "Kind"
                END
                WHERE "Kind" = 'legacy' AND NOT "IsArchived";
                """);

            // مفاتيح الإنابة الجماعية (مستلم واحد قديم).
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "DedupeKey" = 'delegation:' || CAST("DelegationId" AS TEXT)
                    || ':recipient:' || CAST(COALESCE(
                        "TargetLawyerId",
                        (SELECT MIN(r."UserId") FROM "HeadAlertRecipients" r
                         WHERE r."HeadAlertId" = "HeadAlerts"."Id")) AS TEXT)
                    || ':' || "Kind"
                WHERE NOT "IsArchived"
                  AND "DelegationId" IS NOT NULL
                  AND "Kind" IN (
                      'delegation_pending_approval',
                      'delegation_sent_to_subhead',
                      'delegation_pending_completion',
                      'delegation_mirror_deferred',
                      'delegation_mirror_returned',
                      'delegation_mirror_settled',
                      'delegation_mirror_fully_forcibly',
                      'delegation_mirror_updated',
                      'delegation_mirror_local_addition',
                      'delegation_mirror_status_changed')
                  AND ("TargetLawyerId" IS NOT NULL
                       OR (SELECT COUNT(DISTINCT r."UserId") FROM "HeadAlertRecipients" r
                           WHERE r."HeadAlertId" = "HeadAlerts"."Id") = 1);
                """);

            // مفاتيح تدفقات منشئ الملف الخاصة بالإنابة.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "DedupeKey" = CASE
                    WHEN "DocumentId" IS NOT NULL
                      AND EXISTS (SELECT 1 FROM "Documents" d
                                  WHERE d."Id" = "HeadAlerts"."DocumentId")
                    THEN 'document:' || CAST("DocumentId" AS TEXT)
                        || ':recipient:' || CAST((SELECT d."CreatedById" FROM "Documents" d
                                                  WHERE d."Id" = "HeadAlerts"."DocumentId") AS TEXT)
                        || ':' || "Kind"
                    WHEN "TargetLawyerId" IS NOT NULL
                      OR (SELECT COUNT(DISTINCT r."UserId") FROM "HeadAlertRecipients" r
                          WHERE r."HeadAlertId" = "HeadAlerts"."Id") = 1
                    THEN 'document-recipient:recipient:' || CAST(COALESCE(
                            "TargetLawyerId",
                            (SELECT MIN(r."UserId") FROM "HeadAlertRecipients" r
                             WHERE r."HeadAlertId" = "HeadAlerts"."Id")) AS TEXT)
                        || ':' || "Kind"
                    ELSE NULL
                END
                WHERE NOT "IsArchived"
                  AND "Kind" IN (
                      'delegation_assigned_to_lawyer',
                      'delegation_rejected_wrong_court',
                      'delegation_completed_returned');
                """);

            // مفاتيح الاستئناف لمستلم واحد.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "DedupeKey" = 'appeal:' || CAST("AppealId" AS TEXT)
                    || ':recipient:' || CAST(COALESCE(
                        "TargetLawyerId",
                        (SELECT MIN(r."UserId") FROM "HeadAlertRecipients" r
                         WHERE r."HeadAlertId" = "HeadAlerts"."Id")) AS TEXT)
                    || ':' || "Kind"
                WHERE NOT "IsArchived"
                  AND "AppealId" IS NOT NULL
                  AND "Kind" IN (
                      'appeal_referral_to_head',
                      'appeal_returned_to_head',
                      'appeal_pending_to_owner',
                      'appeal_assigned_to_lawyer')
                  AND ("TargetLawyerId" IS NOT NULL
                       OR (SELECT COUNT(DISTINCT r."UserId") FROM "HeadAlertRecipients" r
                           WHERE r."HeadAlertId" = "HeadAlerts"."Id") = 1);
                """);

            // مفتاح الاستئناف النهائي لمنشئ ملف الاستئناف.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "DedupeKey" = CASE
                    WHEN "DocumentId" IS NOT NULL
                      AND EXISTS (SELECT 1 FROM "Documents" d
                                  WHERE d."Id" = "HeadAlerts"."DocumentId")
                    THEN 'document:' || CAST("DocumentId" AS TEXT)
                        || ':recipient:' || CAST((SELECT d."CreatedById" FROM "Documents" d
                                                  WHERE d."Id" = "HeadAlerts"."DocumentId") AS TEXT)
                        || ':' || "Kind"
                    WHEN "TargetLawyerId" IS NOT NULL
                      OR (SELECT COUNT(DISTINCT r."UserId") FROM "HeadAlertRecipients" r
                          WHERE r."HeadAlertId" = "HeadAlerts"."Id") = 1
                    THEN 'document-recipient:recipient:' || CAST(COALESCE(
                            "TargetLawyerId",
                            (SELECT MIN(r."UserId") FROM "HeadAlertRecipients" r
                             WHERE r."HeadAlertId" = "HeadAlerts"."Id")) AS TEXT)
                        || ':' || "Kind"
                    ELSE NULL
                END
                WHERE NOT "IsArchived"
                  AND "Kind" = 'appeal_final_to_base_lawyer';
                """);

            // مفتاح رد كتاب المطالعة.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "DedupeKey" = 'review:' || CAST("ReviewLetterId" AS TEXT)
                    || ':recipient:' || CAST(COALESCE(
                        "TargetLawyerId",
                        (SELECT MIN(r."UserId") FROM "HeadAlertRecipients" r
                         WHERE r."HeadAlertId" = "HeadAlerts"."Id")) AS TEXT)
                    || ':' || "Kind"
                WHERE NOT "IsArchived"
                  AND "Kind" = 'review_letter_reply'
                  AND "ReviewLetterId" IS NOT NULL
                  AND ("TargetLawyerId" IS NOT NULL
                       OR (SELECT COUNT(DISTINCT r."UserId") FROM "HeadAlertRecipients" r
                           WHERE r."HeadAlertId" = "HeadAlerts"."Id") = 1);
                """);

            // مفتاح المعلقات بعد ربط الدائرة.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "DedupeKey" = 'circuit-pending:' || CAST("BranchId" AS TEXT)
                    || ':' || CAST("ExecutionCircuitId" AS TEXT)
                    || ':recipient:' || CAST(COALESCE(
                        "TargetLawyerId",
                        (SELECT MIN(r."UserId") FROM "HeadAlertRecipients" r
                         WHERE r."HeadAlertId" = "HeadAlerts"."Id")) AS TEXT)
                    || ':' || "Kind"
                WHERE NOT "IsArchived"
                  AND "Kind" = 'circuit_pending_documents'
                  AND "ExecutionCircuitId" IS NOT NULL
                  AND ("TargetLawyerId" IS NOT NULL
                       OR (SELECT COUNT(DISTINCT r."UserId") FROM "HeadAlertRecipients" r
                           WHERE r."HeadAlertId" = "HeadAlerts"."Id") = 1);
                """);

            // مفتاح إشعار إعادة تسمية الجهة (الصفوف الجديدة فقط عمليًا).
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "DedupeKey" = 'entity:rename:' || CAST("PublicEntityId" AS TEXT)
                    || ':recipient:' || CAST("TargetLawyerId" AS TEXT)
                    || ':' || "Kind"
                WHERE NOT "IsArchived"
                  AND "Kind" = 'entity_rename_notice_to_creator'
                  AND "PublicEntityId" IS NOT NULL
                  AND "TargetLawyerId" IS NOT NULL;
                """);

            // ما تعذّر بناء مفتاحه يعود legacy بدل نظامي بلا مفتاح.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "Kind" = 'legacy'
                WHERE NOT "IsArchived"
                  AND "DedupeKey" IS NULL
                  AND "Kind" IN (
                      'delegation_pending_approval',
                      'delegation_sent_to_subhead',
                      'delegation_pending_completion',
                      'delegation_assigned_to_lawyer',
                      'delegation_rejected_wrong_court',
                      'delegation_completed_returned',
                      'delegation_mirror_deferred',
                      'delegation_mirror_returned',
                      'delegation_mirror_settled',
                      'delegation_mirror_fully_forcibly',
                      'delegation_mirror_updated',
                      'delegation_mirror_local_addition',
                      'delegation_mirror_status_changed',
                      'appeal_referral_to_head',
                      'appeal_returned_to_head',
                      'appeal_pending_to_owner',
                      'appeal_assigned_to_lawyer',
                      'appeal_final_to_base_lawyer',
                      'review_letter_reply',
                      'circuit_pending_documents',
                      'entity_rename_notice_to_creator');
                """);

            // أرشفة تنبيهات المراجعة المتقاعدة القديمة.
            migrationBuilder.Sql(
                """
                UPDATE "HeadAlerts"
                SET "IsArchived" = 1,
                    "ArchivedAt" = datetime('now'),
                    "DedupeKey" = NULL
                WHERE NOT "IsArchived"
                  AND ("Message" LIKE 'المحامي % أدخل جهة عامة جديدة «%'
                    OR ("PublicEntityId" IS NOT NULL
                        AND "Message" LIKE '%اقترح تعديل جهة%'));
                """);

            // حل تصادم المفاتيح: حامل واحد، ودمج قراءة المستلمين، وأرشفة الخاسرين.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "_HeadAlertDupGroups" AS
                SELECT "DedupeKey", "HolderId"
                FROM (
                    SELECT "DedupeKey", "Id" AS "HolderId",
                           ROW_NUMBER() OVER (
                               PARTITION BY "DedupeKey"
                               ORDER BY COALESCE("LastContentChangedAt", "CreatedAt") DESC,
                                        "CreatedAt" DESC, "Id" DESC) AS rn,
                           COUNT(*) OVER (PARTITION BY "DedupeKey") AS cnt
                    FROM "HeadAlerts"
                    WHERE NOT "IsArchived" AND "DedupeKey" IS NOT NULL
                ) ranked
                WHERE rn = 1 AND cnt > 1;

                CREATE TEMP TABLE "_HeadAlertMergedRecipients" AS
                SELECT g."HolderId" AS "HeadAlertId", r."UserId",
                       MAX(r."IsRead") AS "IsRead", MIN(r."ReadAt") AS "ReadAt"
                FROM "_HeadAlertDupGroups" g
                JOIN "HeadAlerts" a
                  ON a."DedupeKey" = g."DedupeKey" AND NOT a."IsArchived"
                JOIN "HeadAlertRecipients" r ON r."HeadAlertId" = a."Id"
                GROUP BY g."HolderId", r."UserId";

                DELETE FROM "HeadAlertRecipients"
                WHERE "HeadAlertId" IN (SELECT "HolderId" FROM "_HeadAlertDupGroups")
                   OR "HeadAlertId" IN (
                       SELECT a."Id"
                       FROM "HeadAlerts" a
                       JOIN "_HeadAlertDupGroups" g ON g."DedupeKey" = a."DedupeKey"
                       WHERE NOT a."IsArchived" AND a."Id" <> g."HolderId");

                INSERT INTO "HeadAlertRecipients" ("HeadAlertId", "UserId", "IsRead", "ReadAt")
                SELECT "HeadAlertId", "UserId", "IsRead", "ReadAt"
                FROM "_HeadAlertMergedRecipients";

                UPDATE "HeadAlerts"
                SET "IsArchived" = 1,
                    "ArchivedAt" = datetime('now'),
                    "Kind" = 'legacy',
                    "DedupeKey" = NULL
                WHERE NOT "IsArchived"
                  AND "DedupeKey" IN (SELECT "DedupeKey" FROM "_HeadAlertDupGroups")
                  AND "Id" NOT IN (SELECT "HolderId" FROM "_HeadAlertDupGroups");

                DROP TABLE "_HeadAlertMergedRecipients";
                DROP TABLE "_HeadAlertDupGroups";
                """);

            // دمج تكرارات المستلمين (HeadAlertId, UserId) قبل القيد الفريد.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "_HeadAlertDupRecipients" AS
                SELECT "HeadAlertId", "UserId",
                       MAX("IsRead") AS "IsRead", MIN("ReadAt") AS "ReadAt"
                FROM "HeadAlertRecipients"
                GROUP BY "HeadAlertId", "UserId"
                HAVING COUNT(*) > 1;

                DELETE FROM "HeadAlertRecipients"
                WHERE EXISTS (
                    SELECT 1 FROM "_HeadAlertDupRecipients" d
                    WHERE d."HeadAlertId" = "HeadAlertRecipients"."HeadAlertId"
                      AND d."UserId" = "HeadAlertRecipients"."UserId");

                INSERT INTO "HeadAlertRecipients" ("HeadAlertId", "UserId", "IsRead", "ReadAt")
                SELECT "HeadAlertId", "UserId", "IsRead", "ReadAt"
                FROM "_HeadAlertDupRecipients";

                DROP TABLE "_HeadAlertDupRecipients";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_ExecutionCircuits_ExecutionCircuitId",
                table: "HeadAlerts",
                column: "ExecutionCircuitId",
                principalTable: "ExecutionCircuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_BranchId_CreatedById_Active_List",
                table: "HeadAlerts",
                columns: new[] { "BranchId", "CreatedById", "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_BranchId_IsArchived_LastContentChangedAt_Id",
                table: "HeadAlerts",
                columns: new[] { "BranchId", "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived",
                table: "HeadAlerts",
                columns: new[] { "ExecutionCircuitId", "Kind", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_IsArchived_LastContentChangedAt_Id",
                table: "HeadAlerts",
                columns: new[] { "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_Kind_Active_LastChanged",
                table: "HeadAlerts",
                columns: new[] { "Kind", "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_TargetLawyerId_Kind_IsArchived",
                table: "HeadAlerts",
                columns: new[] { "TargetLawyerId", "Kind", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "UX_HeadAlerts_DedupeKey_Active",
                table: "HeadAlerts",
                column: "DedupeKey",
                unique: true,
                filter: "\"DedupeKey\" IS NOT NULL AND \"IsArchived\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_HeadAlertId_IsRead",
                table: "HeadAlertRecipients",
                columns: new[] { "HeadAlertId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_UserId_IsRead",
                table: "HeadAlertRecipients",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "UX_HeadAlertRecipients_HeadAlertId_UserId",
                table: "HeadAlertRecipients",
                columns: new[] { "HeadAlertId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HeadAlerts_ExecutionCircuits_ExecutionCircuitId",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_BranchId_CreatedById_Active_List",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_BranchId_IsArchived_LastContentChangedAt_Id",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_IsArchived_LastContentChangedAt_Id",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_Kind_Active_LastChanged",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_TargetLawyerId_Kind_IsArchived",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "UX_HeadAlerts_DedupeKey_Active",
                table: "HeadAlerts");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlertRecipients_HeadAlertId_IsRead",
                table: "HeadAlertRecipients");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlertRecipients_UserId_IsRead",
                table: "HeadAlertRecipients");

            migrationBuilder.DropIndex(
                name: "UX_HeadAlertRecipients_HeadAlertId_UserId",
                table: "HeadAlertRecipients");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_TargetLawyerId",
                table: "HeadAlerts",
                column: "TargetLawyerId");
        }
    }
}
