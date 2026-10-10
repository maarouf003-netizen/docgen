using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ReviewLetterConfiguration : IEntityTypeConfiguration<ReviewLetter>
{
    public void Configure(EntityTypeBuilder<ReviewLetter> builder)
    {
        builder.ToTable("ReviewLetters");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LetterNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(l => l.LetterNumber).IsUnique();

        builder.Property(l => l.LetterDate).HasColumnType("datetime2");

        builder.HasIndex(l => l.BranchId);
        builder.HasIndex(l => l.DocumentId);
        builder.HasIndex(l => l.CreatedById);
        builder.HasIndex(l => l.RecipientSectionId);
        builder.HasIndex(l => l.IsAnswered);
        builder.HasIndex(l => l.UpdatedAt);

        builder.HasOne(l => l.Branch)
            .WithMany()
            .HasForeignKey(l => l.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.CreatedBy)
            .WithMany()
            .HasForeignKey(l => l.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // الشعبة المستلمة (لبلا ملف) — مرآة قرار التوجيه وقت الإنشاء.
        builder.HasOne(l => l.RecipientSection)
            .WithMany()
            .HasForeignKey(l => l.RecipientSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // الملف المرتبط اختياري (كتاب عام عندما يكون null)؛ الكتاب وثيقة رسمية
        // لا تُحذف بحذف الملف، لذا يُفكّ الرابط فقط (SetNull).
        builder.HasOne(l => l.Document)
            .WithMany()
            .HasForeignKey(l => l.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(l => l.Messages)
            .WithOne(m => m.ReviewLetter)
            .HasForeignKey(m => m.ReviewLetterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
