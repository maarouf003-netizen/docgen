using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentOccurrenceConfiguration : IEntityTypeConfiguration<DocumentOccurrence>
{
    public void Configure(EntityTypeBuilder<DocumentOccurrence> builder)
    {
        builder.ToTable("DocumentOccurrences");
        builder.HasKey(o => o.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(o => o.Document == null || !o.Document.IsDeleted);
        // S3: عرض 30 (لا 20) — أطول رمز حالي («circuit-reregistered») طوله 20 بالضبط،
        // وPostgres وحده يفرض الحد فيفشل أي رمز أطول مستقبلًا عليه فقط.
        builder.Property(o => o.OccurrenceType).HasMaxLength(30).IsRequired();
        builder.HasIndex(o => o.OccurrenceType);
        builder.Property(o => o.Source).HasMaxLength(10).IsRequired().HasDefaultValue("manual");
        builder.Property(o => o.EventDate).HasColumnType("datetime2");
        builder.HasIndex(o => o.EventDate);
        builder.Property(o => o.FileNumber).HasMaxLength(100);
        builder.Property(o => o.FileType).HasMaxLength(100);
        builder.Property(o => o.Year);
        builder.Property(o => o.ReceiptNumber).HasMaxLength(200);
        builder.Property(o => o.ReceiptDate).HasColumnType("datetime2");
        builder.Property(o => o.Details).HasColumnType("text");
        builder.Property(o => o.FromCircuitName).HasMaxLength(200);
        builder.Property(o => o.ToCircuitName).HasMaxLength(200);
        builder.HasIndex(o => o.DocumentId);

        builder.HasOne(o => o.Document)
            .WithMany(d => d.Occurrences)
            .HasForeignKey(o => o.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
