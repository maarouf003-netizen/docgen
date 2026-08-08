using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// سياق بيانات مخصص لـ Postgres (يُستخدم كهوية لمجموعة هجرات منفصلة عن هجرات SQLite).
/// التسجيل عبر AddDbContext&lt;DocGeneratorDbContext, DocGeneratorPostgresDbContext&gt;
/// يجعل كل التبعيات القائمة تتعامل معه وهو النوع الفعلي عند استخدام Postgres.
/// </summary>
public class DocGeneratorPostgresDbContext : DocGeneratorDbContext
{
    public DocGeneratorPostgresDbContext(DbContextOptions<DocGeneratorPostgresDbContext> options)
        : base((DbContextOptions)options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // «datetime2» نوع خاص بـ SQLite وغير معروف في Postgres؛ نستبدله بنوع زمني صالح
        // أثناء بناء نموذج سياق Postgres فقط، دون المساس بنموذج SQLite وهجراته.
        modelBuilder.Entity<Document>()
            .Property(d => d.FileReceiptDate)
            .HasColumnType("timestamp with time zone");
    }
}
