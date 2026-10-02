using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ReviewLetterMessageConfiguration : IEntityTypeConfiguration<ReviewLetterMessage>
{
    public void Configure(EntityTypeBuilder<ReviewLetterMessage> builder)
    {
        builder.ToTable("ReviewLetterMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Kind).HasMaxLength(20).IsRequired();
        builder.Property(m => m.MessageNumber).HasMaxLength(50).IsRequired();
        builder.Property(m => m.AuthorName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.AuthorRole).HasMaxLength(20).IsRequired();
        builder.HasIndex(m => m.ReviewLetterId);
        builder.HasIndex(m => m.BodyPlainText);
    }
}
