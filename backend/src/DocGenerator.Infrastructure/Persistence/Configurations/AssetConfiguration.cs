using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.AssetKind).HasMaxLength(50).IsRequired();
        builder.Property(a => a.ShareType).HasMaxLength(100);
        // العقار
        builder.Property(a => a.Property).HasMaxLength(200);
        builder.Property(a => a.PropertyNumber).HasMaxLength(100);
        builder.Property(a => a.PropertyDistrict).HasMaxLength(200);
        builder.Property(a => a.LandRegistry).HasMaxLength(200);
        // المركبة
        builder.Property(a => a.VehicleType).HasMaxLength(200);
        builder.Property(a => a.VehicleClass).HasMaxLength(200);
        builder.Property(a => a.PlateNumber).HasMaxLength(100);
        builder.Property(a => a.VehicleGovernorate).HasMaxLength(100);
        // المتجر المسجل
        builder.Property(a => a.RegisterNumber).HasMaxLength(100);
        builder.Property(a => a.RegistrationDate).HasColumnType("datetime2");
        builder.Property(a => a.ShopGovernorate).HasMaxLength(100);
        builder.Property(a => a.ShopDescription).HasMaxLength(300);
        builder.Property(a => a.ShopLocation).HasMaxLength(300);
        // كفالة الرواتب
        builder.Property(a => a.PublicEntity).HasMaxLength(300);
        // المتجر غير المسجل
        builder.Property(a => a.LicenseNumber).HasMaxLength(100);
        builder.Property(a => a.LicenseDate).HasColumnType("datetime2");
        builder.Property(a => a.LicenseIssuer).HasMaxLength(300);
        // الملاحظات
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.Assets)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
