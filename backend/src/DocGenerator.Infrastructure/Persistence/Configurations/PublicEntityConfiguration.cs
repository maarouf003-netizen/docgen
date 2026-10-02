using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// قيد الجهة بمستوى المحافظة + الفرع: فهرس فريد مركب يمنع تكرار القيد نفسه
/// تحت الهوية الأم، وفهرسة الحالة والمحافظة لتصفيات السجل والبوابة.
/// </summary>
public class PublicEntityConfiguration : IEntityTypeConfiguration<PublicEntity>
{
    public void Configure(EntityTypeBuilder<PublicEntity> builder)
    {
        builder.ToTable("PublicEntities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Governorate).HasMaxLength(100).IsRequired();
        builder.Property(e => e.BranchName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CitationFormula).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CoverageLabel).HasMaxLength(150);
        builder.Property(e => e.ReviewedAtUtc);
        builder.Property(e => e.IsParentEntity);

        builder.HasIndex(e => new { e.GroupId, e.Governorate, e.BranchName }).IsUnique();
        builder.HasIndex(e => e.Governorate);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.GroupId);
        builder.HasIndex(e => e.NeedsReview);
        builder.HasIndex(e => e.ReviewedById).IsUnique(false);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.IsParentEntity);

        builder.HasOne(e => e.Group)
            .WithMany(g => g.Entries)
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // مُراجِع القيد: يُفكّ الارتباط بحذف حسابه دون المساس بالقيد.
        builder.HasOne(e => e.ReviewedBy)
            .WithMany()
            .HasForeignKey(e => e.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
