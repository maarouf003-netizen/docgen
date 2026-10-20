using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// صندوق الصادر (Outbox) لتسليم التنبيهات النظامية: جدول مستقل بلا علاقات ولا مفاتيح
/// أجنبية — الحمولة JSON دلالية فقط (الخطة §6.2).
/// </summary>
public class HeadAlertOutboxMessageConfiguration : IEntityTypeConfiguration<HeadAlertOutboxMessage>
{
    public void Configure(EntityTypeBuilder<HeadAlertOutboxMessage> builder)
    {
        builder.ToTable("HeadAlertOutboxMessages", table =>
        {
            table.HasCheckConstraint(
                "CK_HeadAlertOutbox_Operation",
                "\"Operation\" IN ('upsert','circuit_renamed')");
            table.HasCheckConstraint(
                "CK_HeadAlertOutbox_Status",
                "\"Status\" IN ('pending','processing','processed','skipped','failed')");
            table.HasCheckConstraint(
                "CK_HeadAlertOutbox_Kind_Operation",
                "(\"Operation\" = 'upsert' AND \"Kind\" IS NOT NULL) OR (\"Operation\" = 'circuit_renamed' AND \"Kind\" IS NULL)");
            table.HasCheckConstraint("CK_HeadAlertOutbox_PayloadVersion", "\"PayloadVersion\" > 0");
            table.HasCheckConstraint("CK_HeadAlertOutbox_Attempts", "\"Attempts\" >= 0");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();

        // الحالة والعملية تُخزَّنان كنص صغير (lowercase) — لا عدد رقمي — لتطابق قيود CK_*.
        builder.Property(m => m.Status)
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => Enum.Parse<HeadAlertOutboxStatus>(value, ignoreCase: true))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(m => m.Operation)
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => Enum.Parse<HeadAlertOutboxOperation>(value, ignoreCase: true))
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.Kind).HasMaxLength(64);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.PayloadVersion).IsRequired();
        builder.Property(m => m.OccurredAt).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.NextAttemptAt).IsRequired();
        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.LockedUntil);
        builder.Property(m => m.LockedBy).HasMaxLength(100);
        builder.Property(m => m.Attempts).IsRequired();
        builder.Property(m => m.LastError);
        builder.Property(m => m.TargetUserId);

        // التقاط مرتّب + استعادة lease المنتهي + تنظيف + إعادة توجيه التعاقب.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt, m.Id });
        builder.HasIndex(m => new { m.Status, m.LockedUntil });
        builder.HasIndex(m => new { m.Status, m.TargetUserId });
        builder.HasIndex(m => new { m.Status, m.ProcessedAt });
    }
}
