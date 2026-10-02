using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// تاريخ أرقام الأساس الاستئنافية لكل سنة (التدوير السنوي): سجلات متعددة لكل
/// (استئناف، سنة) بفهرس غير فريد يحفظ أرقام السنوات السابقة دون فقدانها.
/// </summary>
public class AppealBaseNumberConfiguration : IEntityTypeConfiguration<AppealBaseNumber>
{
    public void Configure(EntityTypeBuilder<AppealBaseNumber> builder)
    {
        builder.ToTable("AppealBaseNumbers");
        builder.HasKey(b => b.Id);
        // سجلات متعددة لكل (استئناف، سنة): كل تدوير يُنشئ سجلًا جديدًا — الأحدث (Year ثم CreatedAt) هو المعتبر.
        builder.HasIndex(b => new { b.AppealId, b.Year });
        builder.HasIndex(b => b.AppealId);
        builder.Property(b => b.BaseNumber).HasMaxLength(50).IsRequired();
        // عامل مطابق لقفل الحذف المنطقي للملف عبر سلسلة الاستئناف.
        builder.HasQueryFilter(b => b.Appeal == null
            || b.Appeal.Document == null
            || !b.Appeal.Document.IsDeleted);

        builder.HasOne(b => b.Appeal)
            .WithMany(a => a.BaseNumbers)
            .HasForeignKey(b => b.AppealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.CreatedBy)
            .WithMany()
            .HasForeignKey(b => b.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
