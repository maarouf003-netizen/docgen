using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentBaseNumberConfiguration : IEntityTypeConfiguration<DocumentBaseNumber>
{
    public void Configure(EntityTypeBuilder<DocumentBaseNumber> builder)
    {
        builder.ToTable("DocumentBaseNumbers");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.CircuitName).HasMaxLength(200);
        builder.Property(b => b.FileType).HasMaxLength(100);
        builder.Property(b => b.TypeKey).HasMaxLength(100).IsRequired();
        builder.Property(b => b.BaseNumber).HasMaxLength(50).IsRequired();
        builder.Property(b => b.NumberKey).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Source).HasMaxLength(20);
        builder.Property(b => b.VoidReason).HasMaxLength(30);

        // الفهرس الفريد الجزئي: يمنع تكرار أي رقم غير ملغى في نفس الدائرة/النوع/السنة.
        builder.HasIndex(b => new { b.ExecutionCircuitId, b.TypeKey, b.Year, b.NumberKey })
            .IsUnique()
            .HasDatabaseName("UX_DocumentBaseNumbers_Active")
            .HasFilter("\"VoidedAt\" IS NULL");

        // الهوية اليومية: أحدث صف لسنة ≤ السنة المطلوبة في دائرة الملف.
        builder.HasIndex(b => new { b.ExecutionCircuitId, b.Year });
        builder.HasIndex(b => new { b.DocumentId, b.Year, b.CreatedAt });

        // عامل مطابق لقفل الحذف المنطقي للمستند الأب.
        builder.HasQueryFilter(b => b.Document == null || !b.Document.IsDeleted);

        builder.HasOne(b => b.Document)
            .WithMany(d => d.BaseNumbers)
            .HasForeignKey(b => b.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.ExecutionCircuit)
            .WithMany()
            .HasForeignKey(b => b.ExecutionCircuitId)
            // حذف الدائرة الإداري: يبقى صف الرقم التاريخي (CircuitName لقطة)
            // ويُفكّ مرجع الدائرة (SetNull) حفاظًا على الضمان؛ الكتابة الجديدة تفرض دائرة.
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.CreatedBy)
            .WithMany()
            .HasForeignKey(b => b.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.UpdatedBy)
            .WithMany()
            .HasForeignKey(b => b.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.VoidedBy)
            .WithMany()
            .HasForeignKey(b => b.VoidedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
