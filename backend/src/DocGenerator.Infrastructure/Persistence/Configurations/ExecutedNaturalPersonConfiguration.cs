using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ExecutedNaturalPersonConfiguration : IEntityTypeConfiguration<ExecutedNaturalPerson>
{
    public void Configure(EntityTypeBuilder<ExecutedNaturalPerson> builder)
    {
        builder.ToTable("ExecutedNaturalPersons");
        builder.HasKey(p => p.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(p => p.Document == null || !p.Document.IsDeleted);
        builder.Property(p => p.Name).HasMaxLength(100);
        builder.Property(p => p.Father).HasMaxLength(100);
        builder.Property(p => p.Family).HasMaxLength(100);
        builder.Property(p => p.AddressType).HasMaxLength(30);
        builder.Property(p => p.AddressOrRepresentative).HasMaxLength(300);
        builder.Property(p => p.RepresentationType).HasMaxLength(30);
        builder.Property(p => p.DeceasedName).HasMaxLength(100);
        builder.Property(p => p.DeceasedFather).HasMaxLength(100);
        builder.Property(p => p.DeceasedFamily).HasMaxLength(100);
        builder.Property(p => p.RepresentativeName).HasMaxLength(100);
        builder.Property(p => p.RepresentativeFather).HasMaxLength(100);
        builder.Property(p => p.RepresentativeFamily).HasMaxLength(100);
        builder.Property(p => p.RepresentativeCapacity).HasMaxLength(30);
        builder.Property(p => p.RepresentativeAddressType).HasMaxLength(50);
        builder.Property(p => p.RepresentativeAddress).HasMaxLength(300);
        builder.HasIndex(p => p.DocumentId);

        builder.HasOne(p => p.Document)
            .WithMany(d => d.ExecutedNaturalPersons)
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
