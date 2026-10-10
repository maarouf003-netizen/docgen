using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class CorrespondenceConfiguration : IEntityTypeConfiguration<Correspondence>
{
    public void Configure(EntityTypeBuilder<Correspondence> builder)
    {
        builder.ToTable("Correspondences");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CorrespondenceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.CorrespondenceNumber).IsUnique();

        builder.Property(c => c.CorrespondenceDate).HasColumnType("datetime2");
        builder.Property(c => c.Governorate).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Importance).HasMaxLength(20).IsRequired();

        builder.HasIndex(c => c.BranchId);
        builder.HasIndex(c => c.Governorate);
        builder.HasIndex(c => c.CreatedById);
        builder.HasIndex(c => c.TargetUserId);
        builder.HasIndex(c => c.RecipientSectionId);
        builder.HasIndex(c => c.DocumentId);
        builder.HasIndex(c => c.Importance);
        builder.HasIndex(c => c.UpdatedAt);

        // العامة من مندوب بلا فرع (SetNull عند حذف الفرع)؛ المراسلة وثيقة رسمية
        // لا تُحذف بحذف الفرع.
        builder.HasOne(c => c.Branch)
            .WithMany()
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.TargetUser)
            .WithMany()
            .HasForeignKey(c => c.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // الشعبة المستلمة (لبلا ملف) — مرآة قرار التوجيه وقت الإنشاء.
        builder.HasOne(c => c.RecipientSection)
            .WithMany()
            .HasForeignKey(c => c.RecipientSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // الملف المرتبط اختياري (عامة عندما يكون null)؛ تُفكّ الرابط فقط (SetNull)
        // لأن المراسلة وثيقة رسمية لا تُحذف بحذف الملف.
        builder.HasOne(c => c.Document)
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Correspondence)
            .HasForeignKey(m => m.CorrespondenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Receipts)
            .WithOne(r => r.Correspondence)
            .HasForeignKey(r => r.CorrespondenceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
