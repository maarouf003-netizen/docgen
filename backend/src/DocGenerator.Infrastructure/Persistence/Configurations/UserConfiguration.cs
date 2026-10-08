using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocGenerator.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // `PB-002`: المحامي ورئيس القسم ورئيس الشعبة فرعيون بالتصميم — قيد قاعدة
        // البيانات يمنع الصف الشاذ (خدمة الإدارة وحدها لا تغطي الكتابة المباشرة
        // أو الاستعادات). الدور مخزّن نصًا (HasConversion<string>) فتعمل الصيغة
        // نفسها على SQLite وPostgres معًا؛ الأدوار بلا فرع (مشرف/مدير/مندوب)
        // غير مشمولة.
        builder.ToTable("Users", table =>
        {
            table.HasCheckConstraint("CK_Users_BranchRequiredForBranchRoles",
                "\"BranchId\" IS NOT NULL OR \"Role\" NOT IN ('Lawyer', 'Head', 'SubHead')");
        });
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        // الاسم الثلاثي فريد ضمن الفرع؛ المستخدمون بلا فرع (مشرف/مدير) يتفردون فيما بينهم منطقياً.
        builder.HasIndex(u => new { u.Username, u.BranchId }).IsUnique();
        // القيد المنطقي المفقود: فهرس فريد جزئي يمنع تكرار اسم الثلاثي بين المستخدمين بلا فرع
        // (BranchId IS NULL) — SQLite وPostgres يدعمان الفهارس الجزئية بهذه الصيغة.
        builder.HasIndex(u => u.Username)
            .HasFilter("\"BranchId\" IS NULL")
            .IsUnique();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(150);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.LockoutEndUtc);

        // نطاق بوابة مندوب الجهة: يُفكّ الارتباط بحذف القيد/الهوية (SetNull) مع فهارس للتصفية.
        builder.HasIndex(u => u.PortalGroupId);
        builder.HasIndex(u => u.PortalEntryId);
        builder.HasOne(u => u.PortalGroup)
            .WithMany()
            .HasForeignKey(u => u.PortalGroupId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(u => u.PortalEntry)
            .WithMany()
            .HasForeignKey(u => u.PortalEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        // شعبة الحساب (إلزامية لرئيس الشعبة خدميًا — مرحلة الحسابات §9) ومنشئه
        // (لمباشرة محامي الصفر ملفات؛ SetNull عند حذف المنشئ فيُعامل كقديم).
        // ملاحظة: لا فهرس عادي على `SectionId` — الفريد الجزئي أدناه يغني عنه
        // في البحث (الفهرس الفريد يخدم الاستعلامات غير الفريدة أيضًا).
        builder.HasIndex(u => u.CreatedById);
        builder.HasOne(u => u.Section)
            .WithMany(s => s.Users)
            .HasForeignKey(u => u.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(u => u.CreatedBy)
            .WithMany()
            .HasForeignKey(u => u.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        // واحد لواحد على المفعّلين فقط (قرار §2.3/§2.26): شعبة واحدة = رئيس
        // شعبة مفعّل واحد، وفرع واحد = رئيس قسم مفعّل واحد. الصيغة المقتبسة
        // المزدوجة تعمل على SQLite وPostgres معًا (سابقة `IS NULL` الجزئية).
        builder.HasIndex(u => u.SectionId)
            .HasFilter("\"Role\" = 'SubHead' AND \"IsActive\"")
            .IsUnique();
        builder.HasIndex(u => u.BranchId)
            .HasFilter("\"Role\" = 'Head' AND \"IsActive\"")
            .IsUnique();
    }
}
