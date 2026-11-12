using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentInterventionHeirConfiguration : IEntityTypeConfiguration<DocumentInterventionHeir>
{
    public void Configure(EntityTypeBuilder<DocumentInterventionHeir> builder)
    {
        builder.ToTable("DocumentInterventionHeirs");
        builder.HasKey(h => h.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب (يمر عبر المتدخل) — يمنع تحذير
        // EF عن علاقة إلزامية بطرف مفلتر بلا فلتر مقابل.
        builder.HasQueryFilter(h => h.DocumentIntervention == null || !h.DocumentIntervention.Document.IsDeleted);
        builder.Property(h => h.HeirName).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(h => h.HeirFather).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(h => h.HeirFamily).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(h => h.Capacity).HasMaxLength(InterventionLimitsCatalog.MaxRepresentativeCapacity);
        builder.Property(h => h.AddressType).HasMaxLength(InterventionLimitsCatalog.MaxAddressType);
        builder.Property(h => h.HeirAddress).HasMaxLength(InterventionLimitsCatalog.MaxAddress);

        builder.Property(h => h.RepresentativeName).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(h => h.RepresentativeFather).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(h => h.RepresentativeFamily).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(h => h.RepresentativeCapacity).HasMaxLength(InterventionLimitsCatalog.MaxRepresentativeCapacity);
        builder.Property(h => h.RepresentativeAddressType).HasMaxLength(InterventionLimitsCatalog.MaxAddressType);
        builder.Property(h => h.RepresentativeAddress).HasMaxLength(InterventionLimitsCatalog.MaxAddress);

        builder.HasIndex(h => h.DocumentInterventionId);

        builder.HasOne(h => h.DocumentIntervention)
            .WithMany(i => i.Heirs)
            .HasForeignKey(h => h.DocumentInterventionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
