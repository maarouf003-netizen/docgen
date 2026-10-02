using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class GuarantorConfiguration : IEntityTypeConfiguration<Guarantor>
{
    public void Configure(EntityTypeBuilder<Guarantor> builder)
    {
        builder.ToTable("Guarantors");
        builder.HasKey(g => g.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(g => g.Document == null || !g.Document.IsDeleted);
        builder.Property(g => g.GuarantorName).HasMaxLength(100);
        builder.Property(g => g.GuarantorFather).HasMaxLength(100);
        builder.Property(g => g.GuarantorFamily).HasMaxLength(100);
        builder.Property(g => g.GuarantorMother).HasMaxLength(100);
        builder.Property(g => g.GuarantorBirth).HasMaxLength(50);
        builder.Property(g => g.GuarantorRegister).HasMaxLength(100);
        builder.Property(g => g.GuarantorNationalId).HasMaxLength(50);
        builder.Property(g => g.GuarantorAddress).HasMaxLength(300);
        builder.Property(g => g.AddressType).HasMaxLength(50);

        builder.Property(g => g.GuarantorNature).HasMaxLength(20).HasDefaultValue(PartyNatureCatalog.Natural);
        builder.Property(g => g.GuarantorRegistrationNumber).HasMaxLength(100);
        builder.Property(g => g.GuarantorRepresentedBy).HasMaxLength(200);

        builder.Property(g => g.RepresentativeName).HasMaxLength(100);
        builder.Property(g => g.RepresentativeFather).HasMaxLength(100);
        builder.Property(g => g.RepresentativeFamily).HasMaxLength(100);
        builder.Property(g => g.RepresentativeCapacity).HasMaxLength(30);
        builder.Property(g => g.RepresentativeAddressType).HasMaxLength(50);
        builder.Property(g => g.RepresentativeAddress).HasMaxLength(300);

        builder.HasOne(g => g.Document)
            .WithMany(d => d.Guarantors)
            .HasForeignKey(g => g.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
