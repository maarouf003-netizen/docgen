using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعدادات جدول التنبيهات.
///
/// **مرحلة التوسّع (PR-1 / M1):** الأعمدة الجديدة nullable للتوافق الخلفي مع الكود
/// القديم، و<code>Message</code> نص بلا سقف، و<code>IsArchived</code> بقيمة افتراضية
/// <c>false</c>. بلا قيد <c>Kind</c>؛ والفهرس الفريد الجزئي لمفتاح المنع مُعرَّف
/// في النموذج والهجرة (M2) وفق الخطة §6.2/§6.3.
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

        builder.HasIndex(a => new { a.IsArchived, a.LastContentChangedAt, a.Id })
            .HasDatabaseName("IX_HeadAlerts_IsArchived_LastContentChangedAt_Id");
        builder.HasIndex(a => new { a.Kind, a.IsArchived, a.LastContentChangedAt, a.Id })
            .HasDatabaseName("IX_HeadAlerts_Kind_Active_LastChanged");
        builder.HasIndex(a => new { a.TargetLawyerId, a.Kind, a.IsArchived })
            .HasDatabaseName("IX_HeadAlerts_TargetLawyerId_Kind_IsArchived");
        builder.HasIndex(a => new { a.ExecutionCircuitId, a.Kind, a.IsArchived })
            .HasDatabaseName("IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived");
        builder.HasIndex(a => new { a.BranchId, a.IsArchived, a.LastContentChangedAt, a.Id })
            .HasDatabaseName("IX_HeadAlerts_BranchId_IsArchived_LastContentChangedAt_Id");
        builder.HasIndex(a => new { a.BranchId, a.CreatedById, a.IsArchived, a.LastContentChangedAt, a.Id })
            .HasDatabaseName("IX_HeadAlerts_BranchId_CreatedById_Active_List");

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
        // بعد حذف الجهة (SetNull) بدل فقدانها.
        builder.HasOne(a => a.PublicEntity)
            .WithMany()
            .HasForeignKey(a => a.PublicEntityId)
            .OnDelete(DeleteBehavior.SetNull);

        // دائرة التنفيذ المرتبطة: أرشفة التنبيهات تسبق حذف الدائرة في الخدمة، ويبقى
        // SetNull شبكة أمان تمنع فقدان التنبيه عند حذف الدائرة من مسار آخر.
        builder.HasOne(a => a.ExecutionCircuit)
            .WithMany()
            .HasForeignKey(a => a.ExecutionCircuitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
