using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentRegistrationDateConfiguration : IEntityTypeConfiguration<DocumentRegistrationDate>
{
    public void Configure(EntityTypeBuilder<DocumentRegistrationDate> builder)
    {
        builder.ToTable("DocumentRegistrationDates");
        builder.HasKey(r => r.DocumentId);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(r => r.Document == null || !r.Document.IsDeleted);
        builder.Property(r => r.Date).HasMaxLength(50);
        // التاريخ المحلول تُجرى عليه فلترة الفترات في SQL (يُستبدل في سياق Postgres بنوع زمني صالح).
        builder.Property(r => r.DateParsed).HasColumnType("datetime2");
        builder.HasIndex(r => r.DateParsed);

        builder.HasOne(r => r.Document)
            .WithOne(d => d.RegistrationDate)
            .HasForeignKey<DocumentRegistrationDate>(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
