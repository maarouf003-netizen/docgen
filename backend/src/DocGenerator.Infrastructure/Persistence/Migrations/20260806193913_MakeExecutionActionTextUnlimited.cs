using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeExecutionActionTextUnlimited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite لا يفرض حد طول على أعمدة TEXT، لذا لا حاجة لأي تغيير.
            // بوستغرس يفرض varchar(2000)، فيُرفع إلى text لدعم النصوص المنسقة الأطول.
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("ALTER TABLE \"ExecutionActions\" ALTER COLUMN \"Text\" TYPE text;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("ALTER TABLE \"ExecutionActions\" ALTER COLUMN \"Text\" TYPE varchar(2000);");
            }
        }
    }
}
