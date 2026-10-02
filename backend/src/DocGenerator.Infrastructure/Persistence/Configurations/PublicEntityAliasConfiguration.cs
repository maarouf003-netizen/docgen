using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// أسماء كتابية بديلة لقيد الجهة: تُفهرس للبحث وتُحذف تبعًا لقيدِها.
/// </summary>
public class PublicEntityAliasConfiguration : IEntityTypeConfiguration<PublicEntityAlias>
{
    public void Configure(EntityTypeBuilder<PublicEntityAlias> builder)
    {
        builder.ToTable("PublicEntityAliases");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AliasText).HasMaxLength(500).IsRequired();

        builder.HasIndex(a => a.AliasText);
        builder.HasIndex(a => a.PublicEntityId);

        builder.HasOne(a => a.PublicEntity)
            .WithMany(e => e.Aliases)
            .HasForeignKey(a => a.PublicEntityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
