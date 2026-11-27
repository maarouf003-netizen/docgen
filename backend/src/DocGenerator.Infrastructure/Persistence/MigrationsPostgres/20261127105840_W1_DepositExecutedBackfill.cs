using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class W1_DepositExecutedBackfill : Migration
    {
        /// <summary>بيان الإصلاح — يُعاد استخدامه في اختبار الأثر (لا نسخة ثانية).</summary>
        public const string BackfillSql = """
            UPDATE "Documents"
            SET "WasDepositExecuted" = TRUE
            WHERE "GeneralEntitySide" = 'deposit'
              AND "ExecutedStatus" = 'منفذ'
              AND "WasDepositExecuted" = FALSE;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // W1/B1: ملفات «عرض وايداع» التي أُنشئت/عُدّلت بحالة «منفذ» من النموذج لم تكن
            // تضبط العلامة الدائمة (كان الضبط في مسار تغيير الحالة وحده)، فيسقط مبلغها من
            // إحصاءات المدير بعد الإرجاع إلى «متداول». نُثبّت العلامة للصفوف الحالية المنفذة.
            // الصفوف التي أُرجعت سابقًا لا يمكن استنتاجها من الحالة وحدها (تحتاج سجل الوقوعات)
            // — تبقى كما هي، وهذا مذكور في docs/audit/331.
            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // لا تراجع: العلامة تاريخية صحيحة، وإرجاعها إلى FALSE يمحو حقيقة تنفيذ سابقة.
        }
    }
}
