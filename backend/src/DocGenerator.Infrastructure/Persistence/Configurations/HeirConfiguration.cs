using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class HeirConfiguration : IEntityTypeConfiguration<Heir>
{
    public void Configure(EntityTypeBuilder<Heir> builder)
    {
        builder.ToTable("Heirs");
        builder.HasKey(h => h.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(h => h.Document == null || !h.Document.IsDeleted);
        builder.Property(h => h.HeirName).HasMaxLength(200);
        builder.Property(h => h.HeirFather).HasMaxLength(200);
        builder.Property(h => h.HeirFamily).HasMaxLength(200);
        builder.Property(h => h.HeirCapacity).HasMaxLength(30);
        builder.Property(h => h.AddressType).HasMaxLength(50);
        builder.Property(h => h.HeirAddress).HasMaxLength(300);
        builder.HasIndex(h => h.DocumentId);

        builder.HasOne(h => h.Document)
            .WithMany(d => d.Heirs)
            .HasForeignKey(h => h.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
