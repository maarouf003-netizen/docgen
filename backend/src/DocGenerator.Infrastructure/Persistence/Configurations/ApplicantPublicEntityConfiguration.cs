using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ApplicantPublicEntityConfiguration : IEntityTypeConfiguration<ApplicantPublicEntity>
{
    public void Configure(EntityTypeBuilder<ApplicantPublicEntity> builder)
    {
        builder.ToTable("ApplicantPublicEntities");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Name).HasMaxLength(200);
        builder.Property(a => a.Branch).HasMaxLength(200);
        builder.Property(a => a.Governorate).HasMaxLength(100);
        builder.HasIndex(a => a.DocumentId);
        // ربط السجل المرجعي: يُفكّ الارتباط (SetNull) بحذف القيد مع فهرس للتصفية.
        builder.HasIndex(a => a.RegistryId);
        builder.Property(a => a.RegistryId);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.ApplicantPublicEntities)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Registry)
            .WithMany()
            .HasForeignKey(a => a.RegistryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
