using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ExecutedHeirConfiguration : IEntityTypeConfiguration<ExecutedHeir>
{
    public void Configure(EntityTypeBuilder<ExecutedHeir> builder)
    {
        builder.ToTable("ExecutedHeirs");
        builder.HasKey(h => h.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(h => h.Document == null || !h.Document.IsDeleted);
        builder.Property(h => h.HeirName).HasMaxLength(200);
        builder.Property(h => h.HeirFather).HasMaxLength(200);
        builder.Property(h => h.HeirFamily).HasMaxLength(200);
        builder.Property(h => h.AddressType).HasMaxLength(50);
        builder.Property(h => h.HeirAddress).HasMaxLength(300);
        builder.HasIndex(h => h.DocumentId);
        builder.HasIndex(h => h.ExecutionApplicantId);
        builder.HasIndex(h => h.ExecutedNaturalPersonId);

        builder.HasOne(h => h.Document)
            .WithMany(d => d.ExecutedHeirs)
            .HasForeignKey(h => h.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ExecutionApplicant)
            .WithMany(a => a.Heirs)
            .HasForeignKey(h => h.ExecutionApplicantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ExecutedNaturalPerson)
            .WithMany(p => p.Heirs)
            .HasForeignKey(h => h.ExecutedNaturalPersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
