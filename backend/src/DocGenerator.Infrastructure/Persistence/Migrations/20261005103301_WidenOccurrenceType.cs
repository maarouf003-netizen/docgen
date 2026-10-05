using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidenOccurrenceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // فارغة عمدًا (S3): عمود SQLite من نوع TEXT بلا حد طول أصلًا،
            // فتوسيع HasMaxLength(20←30) لا يغيّر المخطط هنا — الأثر الفعلي
            // في هجرة Postgres المقابلة (varchar(20)←varchar(30)).
            // تُبقى الهجرة لتزامن لقطة النموذج مع الإعدادات.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
