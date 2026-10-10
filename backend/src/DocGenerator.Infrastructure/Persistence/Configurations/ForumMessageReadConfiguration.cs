using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class ForumMessageReadConfiguration : IEntityTypeConfiguration<ForumMessageRead>
{
    public void Configure(EntityTypeBuilder<ForumMessageRead> builder)
    {
        builder.ToTable("ForumMessageReads");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserName).HasMaxLength(200).IsRequired();

        // توثيق واحد لكل مطّلع على الرسالة.
        builder.HasIndex(r => new { r.MessageId, r.UserId }).IsUnique();
        // العدّاد يُشتق من `MAX(MessageId)` لكل مستخدم — الفهرس يمنع المسح الكامل.
        builder.HasIndex(r => new { r.UserId, r.MessageId });

        // الحذف الصلب للرسالة يمحو إيصالاتها معها (احتياط خلف الحذف الصريح المرتب).
        builder.HasOne(r => r.Message)
            .WithMany()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
