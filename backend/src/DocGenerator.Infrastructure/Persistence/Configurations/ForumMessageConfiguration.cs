using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ForumMessageConfiguration : IEntityTypeConfiguration<ForumMessage>
{
    public void Configure(EntityTypeBuilder<ForumMessage> builder)
    {
        builder.ToTable("ForumMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).HasMaxLength(2000).IsRequired();
        builder.Property(m => m.AuthorName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.AuthorRole).HasMaxLength(20).IsRequired();
        builder.Property(m => m.AuthorLocation).HasMaxLength(200).IsRequired();
        builder.Property(m => m.AuthorSection).HasMaxLength(200);
        builder.Property(m => m.QuotedAuthorName).HasMaxLength(150);
        builder.Property(m => m.QuotedExcerpt).HasMaxLength(200);

        // وحدانية التثبيت: رسالة واحدة مثبتة في اللحظة — الشرط `"IsPinned"`
        // وحده صالح لـ SQLite (عدد صحيح) وPostgres (منطقي) معًا.
        builder.HasIndex(m => m.IsPinned)
            .HasFilter("\"IsPinned\"")
            .IsUnique();
        builder.HasIndex(m => m.CreatedAt);

        // حذف المستخدم لا يمحو ذاكرة المنتدى: الرسائل لقطات بأسماء أصحابها.
        builder.HasOne(m => m.Author)
            .WithMany()
            .HasForeignKey(m => m.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
        // حذف المقتبَس يُصفّر المرجع وتبقى اللقطة (ظل واتساب).
        builder.HasOne(m => m.QuotedMessage)
            .WithMany()
            .HasForeignKey(m => m.QuotedMessageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
