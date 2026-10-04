using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// الهوية الأم للجهة العامة في السجل المرجعي المركزي: الاسم المعتمد فريد،
/// والقيود (المحافظة + الفرع) تحته تُحذف تبعًا له.
/// </summary>
public class PublicEntityGroupConfiguration : IEntityTypeConfiguration<PublicEntityGroup>
{
    public void Configure(EntityTypeBuilder<PublicEntityGroup> builder)
    {
        builder.ToTable("PublicEntityGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.CanonicalName).HasMaxLength(200).IsRequired();
        builder.HasIndex(g => g.CanonicalName).IsUnique();
        // `PB-001`: وحدانية الاسم المعياري (المتغيرات الإملائية هوية واحدة).
        builder.Property(g => g.CanonicalNameNorm).HasMaxLength(200);
        builder.HasIndex(g => g.CanonicalNameNorm).IsUnique();
        builder.Property(g => g.EntityType).HasMaxLength(30).IsRequired();
        builder.HasIndex(g => g.EntityType);
    }
}
