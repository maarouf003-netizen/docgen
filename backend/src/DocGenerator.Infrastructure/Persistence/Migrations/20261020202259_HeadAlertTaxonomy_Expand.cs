using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// M1 — مرحلة التوسّع (Expand) لجدول التنبيهات (SQLite).
    ///
    /// الخطوات المنفَّذة:
    /// <list type="number">
    /// <item>أعمدة جديدة nullable بالكامل للتوافق الخلفي مع الكود القديم:
    /// <c>Kind</c> (64)، <c>DedupeKey</c> (200)، <c>ExecutionCircuitId</c>،
    /// <c>LastContentChangedAt</c>، <c>ArchivedAt</c>.</item>
    /// <item><c>IsArchived</c> عمود NOT NULL بقيمة افتراضية <c>false</c> — فلا تُفصل
    /// الصفوف القديمة ولا تحتاج تعبئة رجعية.</item>
    /// <item><c>Message</c> إلى نص بلا سقف عمود.</item>
    /// </list>
    ///
    /// ملاحظات مزوّد SQLite (راجَعْ الخطة §6.3):
    /// <list type="bullet">
    /// <item>عمود <c>Message</c> كان <c>TEXT</c> أصلًا (SQLite لا يفرض <c>maxLength</c>)،
    /// فالتوسيع نوعية-فقط على مستوى النموذج؛ أما القصّ الصامت فكان في التطبيق
    /// (<c>message[..2000]</c>) ويزول في مرحلة PR-2 مع نقل القوالب إلى
    /// <c>HeadAlertMessages</c>.</item>
    /// <item>بلا قيد <c>Kind</c> وبلا فهرس فريد على <c>DedupeKey</c> وبلا قيد
    /// <c>(HeadAlertId, UserId)</c>: هذه تُنشأ في مرحلة الترحيل (PR-2) بعد حل
    /// تصادم المفاتيح ودمج المستلمين.</item>
    /// <item>لا تُعاد كتابة أي صف: التحويل idempotent وآمن إن تأخر عن فتح الكتابة.</item>
    /// </list>
    /// </summary>
    public partial class HeadAlertTaxonomy_Expand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "HeadAlerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedupeKey",
                table: "HeadAlerts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionCircuitId",
                table: "HeadAlerts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "HeadAlerts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "HeadAlerts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastContentChangedAt",
                table: "HeadAlerts",
                type: "TEXT",
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
        }
    }
}
