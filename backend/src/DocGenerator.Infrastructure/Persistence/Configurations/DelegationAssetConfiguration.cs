using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DelegationAssetConfiguration : IEntityTypeConfiguration<DelegationAsset>
{
    public void Configure(EntityTypeBuilder<DelegationAsset> builder)
    {
        builder.ToTable("DelegationAssets");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AssetKind).HasMaxLength(50).IsRequired();
        builder.Property(a => a.AssetLabel).HasMaxLength(300).IsRequired();
        builder.Property(a => a.SalePrice).HasColumnType("decimal(20,2)");
        builder.Property(a => a.SnapshotAdjusted).IsRequired().HasDefaultValue(false);
        builder.HasIndex(a => a.DelegationId);

        // مطابق لعامل الحذف المنطقي للإنابة (وأصلها): تُخفى الأصول التابعة بحذف المصدر.
        builder.HasQueryFilter(a => a.Delegation == null
            || a.Delegation.SourceDocument == null
            || !a.Delegation.SourceDocument.IsDeleted);

        builder.HasOne(a => a.Delegation)
            .WithMany(d => d.Assets)
            .HasForeignKey(a => a.DelegationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
