using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentBaseNumberConfiguration : IEntityTypeConfiguration<DocumentBaseNumber>
{
    public void Configure(EntityTypeBuilder<DocumentBaseNumber> builder)
    {
        builder.ToTable("DocumentBaseNumbers");
        builder.HasKey(b => b.Id);
        // سجلات متعددة لكل (ملف، سنة): كل تدوير/تجديد يُنشئ سجلًا جديدًا — الأحدث (Year ثم CreatedAt) هو المعتبر.
        builder.HasIndex(b => new { b.DocumentId, b.Year });
        builder.HasIndex(b => b.DocumentId);
        builder.Property(b => b.BaseNumber).HasMaxLength(50).IsRequired();
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب.
        builder.HasQueryFilter(b => b.Document == null || !b.Document.IsDeleted);

        builder.HasOne(b => b.Document)
            .WithMany(d => d.BaseNumbers)
            .HasForeignKey(b => b.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.CreatedBy)
            .WithMany()
            .HasForeignKey(b => b.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
