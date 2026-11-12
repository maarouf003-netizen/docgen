using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentInterventionConfiguration : IEntityTypeConfiguration<DocumentIntervention>
{
    public void Configure(EntityTypeBuilder<DocumentIntervention> builder)
    {
        builder.ToTable("DocumentInterventions");
        builder.HasKey(i => i.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(i => i.Document == null || !i.Document.IsDeleted);

        builder.Property(i => i.Nature)
            .HasMaxLength(InterventionLimitsCatalog.MaxNature)
            .IsRequired()
            .HasDefaultValue(PartyNatureCatalog.Natural);
        builder.Property(i => i.ReceiptNumber).HasMaxLength(InterventionLimitsCatalog.MaxReceiptNumber);
        builder.Property(i => i.Subject).HasMaxLength(InterventionLimitsCatalog.MaxSubject);
        builder.Property(i => i.Notes).HasMaxLength(InterventionLimitsCatalog.MaxNotes);

        builder.Property(i => i.Name).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(i => i.Father).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(i => i.Family).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(i => i.Mother).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(i => i.Birth).HasMaxLength(InterventionLimitsCatalog.MaxBirth);
        builder.Property(i => i.Register).HasMaxLength(InterventionLimitsCatalog.MaxRegister);
        builder.Property(i => i.NationalId).HasMaxLength(InterventionLimitsCatalog.MaxNationalId);
        builder.Property(i => i.AddressType).HasMaxLength(InterventionLimitsCatalog.MaxAddressType);
        builder.Property(i => i.Address).HasMaxLength(InterventionLimitsCatalog.MaxAddress);

        builder.Property(i => i.RepresentativeName).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(i => i.RepresentativeFather).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(i => i.RepresentativeFamily).HasMaxLength(InterventionLimitsCatalog.MaxName);
        builder.Property(i => i.RepresentativeCapacity).HasMaxLength(InterventionLimitsCatalog.MaxRepresentativeCapacity);
        builder.Property(i => i.RepresentativeAddressType).HasMaxLength(InterventionLimitsCatalog.MaxAddressType);
        builder.Property(i => i.RepresentativeAddress).HasMaxLength(InterventionLimitsCatalog.MaxAddress);

        builder.Property(i => i.EntityName).HasMaxLength(InterventionLimitsCatalog.MaxEntityName);
        builder.Property(i => i.EntityBranch).HasMaxLength(InterventionLimitsCatalog.MaxEntityBranch);
        builder.Property(i => i.RegistrationNumber).HasMaxLength(InterventionLimitsCatalog.MaxRegistrationNumber);
        builder.Property(i => i.RepresentedBy).HasMaxLength(InterventionLimitsCatalog.MaxRepresentedBy);
        builder.Property(i => i.Governorate).HasMaxLength(InterventionLimitsCatalog.MaxGovernorate);

        builder.HasIndex(i => i.DocumentId);
        // ربط السجل المرجعي للجهة العامة فقط: SetNull بحذف القيد + فهرس للتصفية.
        builder.HasIndex(i => i.RegistryId);

        builder.HasOne(i => i.Document)
            .WithMany(d => d.Interventions)
            .HasForeignKey(i => i.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Registry)
            .WithMany()
            .HasForeignKey(i => i.RegistryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.CreatedBy)
            .WithMany()
            .HasForeignKey(i => i.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
