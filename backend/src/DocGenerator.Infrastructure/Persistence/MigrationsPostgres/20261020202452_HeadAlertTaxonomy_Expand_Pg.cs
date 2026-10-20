using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <summary>
    /// M1 — مرحلة التوسّع (Expand) لجدول التنبيهات (PostgreSQL).
    ///
    /// الخطوات المنفَّذة:
    /// <list type="number">
    /// <item>توسيع <c>Message</c> من <c>character varying(2000)</c> إلى <c>text</c> —
    /// بلا فقد بيانات (البيانات الموجودة أقصر من 2000 حرفًا أصلًا؛ والقصّ الصامت
    /// كان في التطبيق ويزول في مرحلة PR-2).</item>
    /// <item>أعمدة جديدة nullable بالكامل للتوافق الخلفي مع الكود القديم:
    /// <c>Kind</c> (64)، <c>DedupeKey</c> (200)، <c>ExecutionCircuitId</c>،
    /// <c>LastContentChangedAt</c>، <c>ArchivedAt</c>.</item>
    /// <item><c>IsArchived</c> عمود NOT NULL بقيمة افتراضية <c>false</c> — فلا تُفصل
    /// الصفوف القديمة ولا تحتاج تعبئة رجعية.</item>
    /// </list>
    ///
    /// ملاحظات:
    /// <list type="bullet">
    /// <item>بلا قيد <c>Kind</c> وبلا فهرس فريد على <c>DedupeKey</c> وبلا قيد
    /// <c>(HeadAlertId, UserId)</c>: هذه تُنشأ في مرحلة الترحيل (PR-2) بعد حل
    /// تصادم المفاتيح ودمج المستلمين.</item>
    /// <item>لا تُعاد كتابة أي صف: التحويل idempotent وآمن إن تأخر عن فتح الكتابة.</item>
    /// </list>
    /// </summary>
    public partial class HeadAlertTaxonomy_Expand_Pg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "HeadAlerts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "HeadAlerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedupeKey",
                table: "HeadAlerts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionCircuitId",
                table: "HeadAlerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "HeadAlerts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "HeadAlerts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastContentChangedAt",
                table: "HeadAlerts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "DedupeKey",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "ExecutionCircuitId",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "LastContentChangedAt",
                table: "HeadAlerts");

            // ملاحظة (الخطة §16.2 — «التراجع»): لا تُعاد `Message` إلى
            // `character varying(2000)` — التضييق يفقد بيانات، فالتراجع ملموس
            // هو التراجع التطبيقي مع بقاء العمود نصيًا. أما التراجع اليدوي إلى
            // تضييق القيد فيجب أن يسبق إدخال أي نص أطول من 2000 حرفًا.
        }
    }
}
