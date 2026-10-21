using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <summary>
    /// إنشاء جدول صندوق الصادر (Outbox) لتسليم التنبيهات النظامية (PostgreSQL).
    ///
    /// الجدول مستقل تمامًا: بلا علاقات (Navigation) ولا مفاتيح أجنبية — الحمولة
    /// JSON دلالية فقط. المفتاح <c>bigint identity</c> المتزايد هو أساس الالتقاط
    /// المرتّب (<c>Id ASC</c>) في المشغّل.
    ///
    /// القيود:
    /// <list type="bullet">
    /// <item><c>CK_HeadAlertOutbox_Operation</c>: <c>upsert</c> أو <c>circuit_renamed</c> فقط.</item>
    /// <item><c>CK_HeadAlertOutbox_Status</c>: <c>pending|processing|processed|skipped|failed</c> فقط.</item>
    /// <item><c>CK_HeadAlertOutbox_Kind_Operation</c>: <c>Kind</c> إلزامي لـ<c>upsert</c>
    /// و<code>NULL</code> لـ<c>circuit_renamed</c>.</item>
    /// <item><c>CK_HeadAlertOutbox_PayloadVersion</c> و<code>CK_HeadAlertOutbox_Attempts</c>.</item>
    /// </list>
    ///
    /// الفهارس: <c>(Status, NextAttemptAt, Id)</c> للالتقاط المرتّب، و<code>(Status, LockedUntil)</c>
    /// لاستعادة الـlease المنتهي، و<code>(Status, TargetUserId)</c> لإعادة توجيه التعاقب،
    /// و<code>(Status, ProcessedAt)</c> للتنظيف.
    ///
    /// هذه الهجرة **إضافية بحتة** ولا تعتمد على <c>Kind</c>/<c>Message</c>، وتُطبَّق مع
    /// <c>HeadAlertTaxonomy_Expand_Pg</c> قبل أي كود يسجّل رسائل (الخطة §6.3).
    /// </summary>
    public partial class HeadAlertOutbox_Create_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeadAlertOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    TargetUserId = table.Column<int>(type: "integer", nullable: true)
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
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "HeadAlertOutboxMessages"
                               WHERE "Status" IN ('pending','processing','failed')) THEN
                        RAISE EXCEPTION 'HeadAlertOutboxMessages يحتوي رسائل غير محسومة؛ عالجها أو صدّرها قبل إسقاط الجدول.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "HeadAlertOutboxMessages");
        }
    }
}
