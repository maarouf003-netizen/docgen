using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ExecutionApplicantConfiguration : IEntityTypeConfiguration<ExecutionApplicant>
{
    public void Configure(EntityTypeBuilder<ExecutionApplicant> builder)
    {
        builder.ToTable("ExecutionApplicants");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Name).HasMaxLength(100);
        builder.Property(a => a.Father).HasMaxLength(100);
        builder.Property(a => a.Family).HasMaxLength(100);
        builder.Property(a => a.LegalRepresentative).HasMaxLength(300);
        builder.Property(a => a.RepresentationType).HasMaxLength(30);
        builder.Property(a => a.DeceasedName).HasMaxLength(100);
        builder.Property(a => a.DeceasedFather).HasMaxLength(100);
        builder.Property(a => a.DeceasedFamily).HasMaxLength(100);
        builder.Property(a => a.RepresentativeName).HasMaxLength(100);
        builder.Property(a => a.RepresentativeFather).HasMaxLength(100);
        builder.Property(a => a.RepresentativeFamily).HasMaxLength(100);
        builder.Property(a => a.RepresentativeCapacity).HasMaxLength(30);
        builder.Property(a => a.RepresentativeLegalRepresentative).HasMaxLength(300);

        builder.Property(a => a.ApplicantNature).HasMaxLength(20).HasDefaultValue(PartyNatureCatalog.Natural);
        builder.Property(a => a.ApplicantRegistrationNumber).HasMaxLength(100);
        builder.Property(a => a.ApplicantRepresentedBy).HasMaxLength(200);
        builder.Property(a => a.ApplicantAddressType).HasMaxLength(50);
        builder.Property(a => a.ApplicantAddress).HasMaxLength(300);
        builder.HasIndex(a => a.DocumentId);
        // ربط السجل المرجعي للجهات العامة فقط: SetNull بحذف القيد + فهرس.
        builder.HasIndex(a => a.RegistryId);
        builder.Property(a => a.RegistryId);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.ExecutionApplicants)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Registry)
            .WithMany()
            .HasForeignKey(a => a.RegistryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
