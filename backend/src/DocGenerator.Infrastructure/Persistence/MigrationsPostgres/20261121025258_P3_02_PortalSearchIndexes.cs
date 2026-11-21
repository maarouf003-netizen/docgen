using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class P3_02_PortalSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // P3-02: فهرس ثلاثي (trigram) لدعم `LIKE '%term%'` على نص البحث المطبَّع
            // بدل المسح التسلسلي. الإضافة قياسية في PostgreSQL (contrib) ولا تغيّر العقد.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // لا تُسقط الإضافة: قد تعتمد عليها فهارس/كائنات أخرى خارج هذه الهجرة.
        }
    }
}
