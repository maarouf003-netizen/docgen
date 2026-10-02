using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(150).IsRequired();
        builder.Property(b => b.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(b => b.Code).IsUnique();
        builder.Property(b => b.Address).HasMaxLength(300);
        builder.Property(b => b.Phone).HasMaxLength(30);
        builder.Property(b => b.Governorate).HasMaxLength(100);
    }
}
