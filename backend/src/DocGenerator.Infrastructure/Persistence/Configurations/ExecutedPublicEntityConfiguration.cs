using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ExecutedPublicEntityConfiguration : IEntityTypeConfiguration<ExecutedPublicEntity>
{
    public void Configure(EntityTypeBuilder<ExecutedPublicEntity> builder)
    {
        builder.ToTable("ExecutedPublicEntities");
        builder.HasKey(e => e.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(e => e.Document == null || !e.Document.IsDeleted);
        builder.Property(e => e.EntityName).HasMaxLength(200);
        builder.Property(e => e.EntityBranch).HasMaxLength(200);
        builder.Property(e => e.Governorate).HasMaxLength(100);

        builder.Property(e => e.EntityNature).HasMaxLength(20).HasDefaultValue(PartyNatureCatalog.PublicEntity);
        builder.Property(e => e.RegistrationNumber).HasMaxLength(100);
        builder.Property(e => e.RepresentedBy).HasMaxLength(200);
        builder.Property(e => e.AddressType).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(300);
        builder.HasIndex(e => e.DocumentId);
        // ربط السجل المرجعي للجهات العامة فقط: SetNull بحذف القيد + فهرس.
        builder.HasIndex(e => e.RegistryId);
        builder.Property(e => e.RegistryId);

        builder.HasOne(e => e.Document)
            .WithMany(d => d.ExecutedPublicEntities)
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Registry)
            .WithMany()
            .HasForeignKey(e => e.RegistryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
