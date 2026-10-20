using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// إنشاء جدول صندوق الصادر (Outbox) لتسليم التنبيهات النظامية (SQLite).
    ///
    /// الجدول مستقل تمامًا: بلا علاقات (Navigation) ولا مفاتيح أجنبية — الحمولة
    /// JSON دلالية فقط.
    ///
    /// القيود:
    /// <list type="bullet">
    /// <item><c>CK_HeadAlertOutbox_Operation</c>: <c>upsert</c> أو <c>circuit_renamed</c> فقط.</item>
    /// <item><c>CK_HeadAlertOutbox_Status</c>: <c>pending|processing|processed|skipped|failed</c> فقط.</item>
    /// <item><c>CK_HeadAlertOutbox_Kind_Operation</c>: <c>Kind</c> إلزامي لـ<c>upsert</c>
    /// و<code>NULL</code> لـ<c>circuit_renamed</c>.</item>
    /// <item><c>CK_HeadAlertOutbox_PayloadVersion</c> و<code>CK_HeadAlertOutbox_Attempts</code>.</item>
    /// </list>
    ///
    /// الفهارس: <c>(Status, NextAttemptAt, Id)</c> للالتقاط المرتّب، و<code>(Status, LockedUntil)</c>
    /// لاستعادة الـlease المنتهي، و<code>(Status, TargetUserId)</code> لإعادة توجيه التعاقب،
    /// و<code>(Status, ProcessedAt)</code> للتنظيف.
    ///
    /// هذه الهجرة **إضافية بحتة** ولا تعتمد على <c>Kind</c>/<c>Message</c>، وتُطبَّق مع
    /// <c>HeadAlertTaxonomy_Expand</c> قبل أي كود يسجّل رسائل (الخطة §6.3).
    /// </summary>
    public partial class HeadAlertOutbox_Create : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeadAlertOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    TargetUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadAlertOutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_HeadAlertOutbox_Attempts", "\"Attempts\" >= 0");
                    table.CheckConstraint("CK_HeadAlertOutbox_Kind_Operation", "(\"Operation\" = 'upsert' AND \"Kind\" IS NOT NULL) OR (\"Operation\" = 'circuit_renamed' AND \"Kind\" IS NULL)");
                    table.CheckConstraint("CK_HeadAlertOutbox_Operation", "\"Operation\" IN ('upsert','circuit_renamed')");
                    table.CheckConstraint("CK_HeadAlertOutbox_PayloadVersion", "\"PayloadVersion\" > 0");
                    table.CheckConstraint("CK_HeadAlertOutbox_Status", "\"Status\" IN ('pending','processing','processed','skipped','failed')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_LockedUntil",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_NextAttemptAt_Id",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "NextAttemptAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_ProcessedAt",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_TargetUserId",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "TargetUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // حارس الإسقاط (الخطة §6.3): لا يُسقط الجدول وبه رسائل غير محسومة
            // (pending/processing/failed) — تُعالَج أو تُصدَّر أولًا ويُوثَّق ذلك.
            // التنفيذ عبر قيد CHECK مؤقت (سلوك SQLite: لا RAISE خارج المشغِّلات).
            migrationBuilder.Sql(
                """
                CREATE TEMPORARY TABLE "__outbox_down_guard" ("Guard" INTEGER NOT NULL CONSTRAINT "CK_outbox_down_guard" CHECK ("Guard" = 0));
                INSERT INTO "__outbox_down_guard" ("Guard")
                SELECT CASE WHEN EXISTS (
                           SELECT 1 FROM "HeadAlertOutboxMessages"
                           WHERE "Status" IN ('pending','processing','failed'))
                       THEN 1 ELSE 0 END;
                DROP TABLE "__outbox_down_guard";
                """);

            migrationBuilder.DropTable(
                name: "HeadAlertOutboxMessages");
        }
    }
}
