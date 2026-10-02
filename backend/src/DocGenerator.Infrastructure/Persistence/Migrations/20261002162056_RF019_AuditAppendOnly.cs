using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// RF-019 (SEC-008): سجل التدقيق إلحاق-فقط على مستوى القاعدة — 4 مشغّلات
    /// ترفض أي `UPDATE`/`DELETE` على `AuditLogs` و`DocumentFieldChanges`.
    /// لا كتابة ممنوعة في الشفرة أصلًا (الكاتب إدخال فقط) فلا كسر لمسار قائم.
    /// </summary>
    public partial class RF019_AuditAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TRIGGER TRG_AuditLogs_NoUpdate BEFORE UPDATE ON AuditLogs BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع التعديل'); END;");
            migrationBuilder.Sql("CREATE TRIGGER TRG_AuditLogs_NoDelete BEFORE DELETE ON AuditLogs BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع الحذف'); END;");
            migrationBuilder.Sql("CREATE TRIGGER TRG_DocumentFieldChanges_NoUpdate BEFORE UPDATE ON DocumentFieldChanges BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع التعديل'); END;");
            migrationBuilder.Sql("CREATE TRIGGER TRG_DocumentFieldChanges_NoDelete BEFORE DELETE ON DocumentFieldChanges BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع الحذف'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_AuditLogs_NoUpdate;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_AuditLogs_NoDelete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_DocumentFieldChanges_NoUpdate;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TRG_DocumentFieldChanges_NoDelete;");
        }
    }
}
