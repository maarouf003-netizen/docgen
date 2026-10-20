using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعدادات جدول التنبيهات.
///
/// **مرحلة التوسّع (PR-1 / M1):** الأعمدة الجديدة nullable للتوافق الخلفي مع الكود
/// القديم، و<code>Message</code> نص بلا سقف، و<code>IsArchived</code> بقيمة افتراضية
/// <c>false</c>. بلا قيد <c>Kind</c> وبلا فهرس فريد وبلا فهرس القائمة — تُضاف في
/// مرحلة الترحيل (PR-2) و<code>Contract</code> (PR-3) وفق الخطة §6.2/§6.3.
/// </summary>
public class HeadAlertConfiguration : IEntityTypeConfiguration<HeadAlert>
{
    public void Configure(EntityTypeBuilder<HeadAlert> builder)
    {
        builder.ToTable("HeadAlerts");
        builder.HasKey(a => a.Id);

        // النص بلا سقف عمود: «text»/«TEXT» بلا قصّ صامت (الخطة §6.2/§7.3).
        builder.Property(a => a.Message).IsRequired();

        // الأنواع الجديدة nullable في مرحلة التوسّع؛ ويفرضها «Contract» غير قابلة للفراغ.
        builder.Property(a => a.Kind).HasMaxLength(64);
        builder.Property(a => a.DedupeKey).HasMaxLength(200);
        builder.Property(a => a.LastContentChangedAt);

        // الأرشفة المنطقية: عمود NOT NULL بقيمة افتراضية false (لا يفصل الصفوف القديمة).
        builder.Property(a => a.IsArchived).HasDefaultValue(false);
        builder.Property(a => a.ArchivedAt);

        // الربط الدلالي بدائرة التنفيذ (بديل مطابقة النص). العلاقة (FK) تُضاف في
        // مرحلة الترحيل بعد تعبئة العمود، فتبقى مرحلة التوسّع بلا قيد مرجعي جديد.
        builder.Property(a => a.ExecutionCircuitId);

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
