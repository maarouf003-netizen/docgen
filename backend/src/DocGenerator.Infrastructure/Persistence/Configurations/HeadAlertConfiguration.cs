using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class HeadAlertConfiguration : IEntityTypeConfiguration<HeadAlert>
{
    public void Configure(EntityTypeBuilder<HeadAlert> builder)
    {
        builder.ToTable("HeadAlerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Message).HasMaxLength(2000).IsRequired();
        builder.HasIndex(a => a.BranchId);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.DelegationId);
        builder.HasIndex(a => a.AppealId);
        builder.HasIndex(a => a.ReviewLetterId);

        builder.HasOne(a => a.Branch)
            .WithMany()
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedBy)
            .WithMany()
            .HasForeignKey(a => a.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.TargetLawyer)
            .WithMany()
            .HasForeignKey(a => a.TargetLawyerId)
            .OnDelete(DeleteBehavior.Restrict);

        // الاستئناف المرتبط بالتنبيه: عند حذفه يُبقى التنبيه ويُفكّ رابطه (SetNull)
        // حتى تبقى سجلّات التنبيهات التاريخية سليمة.
        builder.HasOne(a => a.Appeal)
            .WithMany()
            .HasForeignKey(a => a.AppealId)
            .OnDelete(DeleteBehavior.SetNull);

// كتاب المطالعة المرتبط (تنبيه الرد): الكتاب سجل رسمي لا يُحذف،
        // ويبقى السلوك نفسه بحكم الحماية عند أي تطور مستقبلي.
        builder.HasOne(a => a.ReviewLetter)
            .WithMany()
            .HasForeignKey(a => a.ReviewLetterId)
            .OnDelete(DeleteBehavior.SetNull);

        // الجهة العامة المرتبطة (تنبيه اقتراح التعديل/المراجعة): تبقى سجلات التنبيهات
        // التاريخية سليمة عند حذف القيد فيُفكّ الرابط (SetNull) دون حذف التنبيه.
        builder.HasOne(a => a.PublicEntity)
            .WithMany()
            .HasForeignKey(a => a.PublicEntityId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
