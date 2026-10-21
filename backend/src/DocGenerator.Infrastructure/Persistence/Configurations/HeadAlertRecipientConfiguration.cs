using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعدادات جدول مستلمي التنبيه.
///
/// **مرحلة التوسّع (PR-1 / M1):** بلا تغيير — تُضاف فهارس العدّادات
/// (<c>(UserId, IsRead)</c> و<code>(HeadAlertId, IsRead)</c>) وقيد «مستلم واحد لكل
/// تنبيه» في مرحلة الترحيل (PR-2) بعد دمج الصفوف المكررة (الخطة §6.2/§6.3).
/// </summary>
public class HeadAlertRecipientConfiguration : IEntityTypeConfiguration<HeadAlertRecipient>
{
    public void Configure(EntityTypeBuilder<HeadAlertRecipient> builder)
    {
        builder.ToTable("HeadAlertRecipients");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.HeadAlertId);
        builder.HasIndex(r => r.UserId);

        builder.HasOne(r => r.HeadAlert)
            .WithMany(a => a.Recipients)
            .HasForeignKey(r => r.HeadAlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
