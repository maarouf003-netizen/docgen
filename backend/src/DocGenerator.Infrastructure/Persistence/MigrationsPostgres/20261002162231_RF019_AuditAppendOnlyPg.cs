using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <summary>
    /// RF-019 (SEC-008): سجل التدقيق إلحاق-فقط على مستوى القاعدة — دالة `plpgsql`
    /// واحدة ترفض أي `UPDATE`/`DELETE` على `AuditLogs` و`DocumentFieldChanges`
    /// (4 مشغّلات مربوطة بها). لا كتابة ممنوعة في الشفرة أصلًا فلا كسر لمسار قائم.
    /// </summary>
    public partial class RF019_AuditAppendOnlyPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE OR REPLACE FUNCTION fn_prevent_audit_mutation() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'سجل التدقيق إلحاق-فقط: ممنوع % على %', TG_OP, TG_TABLE_NAME; RETURN NULL; END; $$ LANGUAGE plpgsql;");
            migrationBuilder.Sql("CREATE TRIGGER trg_auditlogs_no_update BEFORE UPDATE ON \"AuditLogs\" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();");
            migrationBuilder.Sql("CREATE TRIGGER trg_auditlogs_no_delete BEFORE DELETE ON \"AuditLogs\" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();");
            migrationBuilder.Sql("CREATE TRIGGER trg_documentfieldchanges_no_update BEFORE UPDATE ON \"DocumentFieldChanges\" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();");
            migrationBuilder.Sql("CREATE TRIGGER trg_documentfieldchanges_no_delete BEFORE DELETE ON \"DocumentFieldChanges\" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_auditlogs_no_update ON \"AuditLogs\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_auditlogs_no_delete ON \"AuditLogs\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_documentfieldchanges_no_update ON \"DocumentFieldChanges\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_documentfieldchanges_no_delete ON \"DocumentFieldChanges\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_prevent_audit_mutation();");
        }
    }
}
