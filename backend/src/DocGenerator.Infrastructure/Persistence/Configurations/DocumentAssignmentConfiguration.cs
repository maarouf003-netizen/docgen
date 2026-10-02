using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class DocumentAssignmentConfiguration : IEntityTypeConfiguration<DocumentAssignment>
{
    public void Configure(EntityTypeBuilder<DocumentAssignment> builder)
    {
        builder.ToTable("DocumentAssignments");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Kind).HasMaxLength(20);
        builder.Property(a => a.LawyerName).HasMaxLength(200);
        builder.Property(a => a.AssignedByName).HasMaxLength(200);
        builder.Property(a => a.AssignedAt).HasColumnType("datetime2");
        builder.HasIndex(a => a.DocumentId);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.Assignments)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
