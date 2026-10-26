using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// إعدادات جدول التنبيهات.
///
/// **مرحلة التشديد (PR-3 / M3):** <code>Kind</code> و<code>LastContentChangedAt</code>
/// غير قابلين للفراغ في النموذج، وقيد <c>CK_HeadAlerts_Kind_NotEmpty</c> يرفض
/// النوع الفارغ/الأبيض. الفهرس الفريد الجزئي لمفتاح المنع مُعرَّف في
/// <see cref="DocGeneratorDbContext.ConfigureHeadAlertProviderSpecific"/> وفق §6.2/§6.3.
/// </summary>
public class HeadAlertConfiguration : IEntityTypeConfiguration<HeadAlert>
{
    public void Configure(EntityTypeBuilder<HeadAlert> builder)
    {
        builder.ToTable("HeadAlerts", table =>
            table.HasCheckConstraint(
                "CK_HeadAlerts_Kind_NotEmpty",
                "TRIM(\"Kind\") <> ''"));
        builder.HasKey(a => a.Id);

        // النص بلا سقف عمود: «text»/«TEXT» بلا قصّ صامت (الخطة §6.2/§7.3).
        builder.Property(a => a.Message).IsRequired();

        // بعد «Contract»: النوع وزمن آخر تغيّر إلزاميان، وقيد يرفض النوع الفارغ.
        builder.Property(a => a.Kind).HasMaxLength(64).IsRequired();
        builder.Property(a => a.DedupeKey).HasMaxLength(200);
        builder.Property(a => a.LastContentChangedAt).IsRequired();

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
