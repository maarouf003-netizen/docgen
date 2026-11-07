using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentAnnexConfiguration : IEntityTypeConfiguration<DocumentAnnex>
{
    public void Configure(EntityTypeBuilder<DocumentAnnex> builder)
    {
        builder.ToTable("DocumentAnnexes");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب.
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);

        builder.Property(a => a.AnnexType).HasMaxLength(100);
        builder.Property(a => a.AnnexNumber).HasMaxLength(100);
        builder.Property(a => a.AnnexDate).HasMaxLength(50);

        builder.HasIndex(a => new { a.DocumentId, a.Sequence });
        builder.HasOne(a => a.Document)
            .WithMany(d => d.Annexes)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
