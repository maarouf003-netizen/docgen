using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class AssetOwnerConfiguration : IEntityTypeConfiguration<AssetOwner>
{
    public void Configure(EntityTypeBuilder<AssetOwner> builder)
    {
        builder.ToTable("AssetOwners");
        builder.HasKey(o => o.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(o => o.Asset == null || o.Asset.Document == null || !o.Asset.Document.IsDeleted);
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(o => o.AssetId);

        builder.HasOne(o => o.Asset)
            .WithMany(a => a.Owners)
            .HasForeignKey(o => o.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
