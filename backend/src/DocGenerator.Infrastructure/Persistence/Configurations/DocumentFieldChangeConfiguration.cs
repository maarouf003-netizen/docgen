using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentFieldChangeConfiguration : IEntityTypeConfiguration<DocumentFieldChange>
{
    public void Configure(EntityTypeBuilder<DocumentFieldChange> builder)
    {
        builder.ToTable("DocumentFieldChanges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FieldKey).HasMaxLength(120).IsRequired();
        builder.Property(c => c.FieldLabel).HasMaxLength(150).IsRequired();
        builder.Property(c => c.OldValue).HasMaxLength(2000);
        builder.Property(c => c.NewValue).HasMaxLength(2000);

        // الفهرس المركب يخدم صفحة «سجل التعديلات» للملف: تجميع إدخالات التدقيق
        // ذات التغييرات لملف محدد بترتيبها الزمني.
        builder.HasIndex(c => new { c.DocumentId, c.Id });
        builder.HasIndex(c => c.AuditLogId);

        // صفوف التغييرات لا معنى لها بمعزل عن إدخال التدقيق الأب: تُحذف تبعًا له.
        builder.HasOne(c => c.AuditLog)
            .WithMany(a => a.FieldChanges)
            .HasForeignKey(c => c.AuditLogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
