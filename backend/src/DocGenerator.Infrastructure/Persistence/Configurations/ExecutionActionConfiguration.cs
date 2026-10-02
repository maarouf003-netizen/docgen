using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ExecutionActionConfiguration : IEntityTypeConfiguration<ExecutionAction>
{
    public void Configure(EntityTypeBuilder<ExecutionAction> builder)
    {
        builder.ToTable("ExecutionActions");
        builder.HasKey(a => a.Id);
        // عامل مطابق لقفل الحذف المنطقي للمستند الأب
        builder.HasQueryFilter(a => a.Document == null || !a.Document.IsDeleted);
        builder.Property(a => a.Type).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Text).IsRequired();
        builder.Property(a => a.ActionDate).HasMaxLength(50);
        builder.Property(a => a.ReminderDuration).HasMaxLength(20);
        builder.Property(a => a.ReminderColor).HasMaxLength(20);
        builder.HasIndex(a => a.DocumentId);
        builder.HasIndex(a => a.CreatedAt);

        builder.HasOne(a => a.Document)
            .WithMany(d => d.ExecutionActions)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
