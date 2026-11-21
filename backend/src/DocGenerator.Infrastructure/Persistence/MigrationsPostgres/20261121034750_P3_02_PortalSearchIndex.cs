using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class P3_02_PortalSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // فهرس GIN ثلاثي على نص البحث. `CREATE INDEX` عادي (لا CONCURRENTLY) لأن
            // سكربت الهجرات الـidempotent يلفّ الأوامر في DO/معاملة، و`CONCURRENTLY`
            // غير صالح هناك. الجدول في نطاق عشرات الآلاف؛ يُنفَّذ في نافذة نشر.
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_Documents_SearchText_Trgm"
                ON "Documents" USING gin ("SearchText" gin_trgm_ops)
                WHERE "SearchText" IS NOT NULL AND NOT "IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Documents_SearchText_Trgm";""");
        }
    }
}
