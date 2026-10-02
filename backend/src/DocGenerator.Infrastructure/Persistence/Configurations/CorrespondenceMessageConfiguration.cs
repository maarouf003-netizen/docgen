using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class CorrespondenceMessageConfiguration : IEntityTypeConfiguration<CorrespondenceMessage>
{
    public void Configure(EntityTypeBuilder<CorrespondenceMessage> builder)
    {
        builder.ToTable("CorrespondenceMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Kind).HasMaxLength(20).IsRequired();
        builder.Property(m => m.MessageNumber).HasMaxLength(50).IsRequired();
        builder.Property(m => m.AuthorName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.AuthorRole).HasMaxLength(20).IsRequired();
        builder.HasIndex(m => m.CorrespondenceId);
        builder.HasIndex(m => m.BodyPlainText);
    }
}
