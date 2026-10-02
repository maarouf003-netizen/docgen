using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// إجراءات وملاحظات الاستئناف (قائمة مستقلة عن إجراءات الملف الأساس) مع حقول التذكير،
/// تُحذف بحذف استئنافها وتُخفى عند الحذف المنطقي للملف الأصل.
/// </summary>
public class AppealActionConfiguration : IEntityTypeConfiguration<AppealAction>
{
    public void Configure(EntityTypeBuilder<AppealAction> builder)
    {
        builder.ToTable("AppealActions");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للملف عبر سلسلة الاستئناف.
        builder.HasQueryFilter(a => a.Appeal == null
            || a.Appeal.Document == null
            || !a.Appeal.Document.IsDeleted);

        builder.Property(a => a.Type).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Text).IsRequired();
        builder.Property(a => a.ActionDate).HasMaxLength(50);
        builder.Property(a => a.ReminderDuration).HasMaxLength(20);
        builder.Property(a => a.ReminderColor).HasMaxLength(20);
        builder.HasIndex(a => a.AppealId);
        builder.HasIndex(a => a.CreatedAt);

        builder.HasOne(a => a.Appeal)
            .WithMany(ap => ap.Actions)
            .HasForeignKey(a => a.AppealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
