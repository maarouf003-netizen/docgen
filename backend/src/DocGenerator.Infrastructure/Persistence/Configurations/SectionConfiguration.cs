using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.NameNorm).HasMaxLength(200).IsRequired();
        builder.Property(s => s.IsActive).HasDefaultValue(true);
        builder.Property(s => s.CreatedAt);
        builder.Property(s => s.UpdatedAt);

        // وحدانية الاسم المعياري داخل الفرع (مرآة قاعدة الدوائر).
        builder.HasIndex(s => new { s.BranchId, s.NameNorm }).IsUnique();
        builder.HasIndex(s => s.BranchId);
        builder.HasIndex(s => s.IsActive);

        builder.HasOne(s => s.Branch)
            .WithMany()
            .HasForeignKey(s => s.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
