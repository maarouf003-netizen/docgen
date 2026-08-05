using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// بذر الفروع والمستخدمين نفس بيانات تطبيق Flask المرجعي (كلمة سر: 123456).
/// يُستدعى في بيئة التطوير فقط؛ أما الإنتاج فلا يُنشئ حسابات افتراضية بكلمة معروفة.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(DocGeneratorDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (!db.Branches.Any())
        {
            db.Branches.AddRange(
                new Branch { Name = "الفرع الرئيسي - دمشق", Code = "DAM", Address = "دمشق" },
                new Branch { Name = "فرع حلب", Code = "ALP", Address = "حلب" },
                new Branch { Name = "فرع حمص", Code = "HMS", Address = "حمص" },
                new Branch { Name = "فرع اللاذقية", Code = "LAT", Address = "اللاذقية" },
                new Branch { Name = "فرع طرطوس", Code = "TAR", Address = "طرطوس" });
            await db.SaveChangesAsync(ct);
        }

        if (!db.Users.Any())
        {
            var damascus = db.Branches.FirstOrDefault(b => b.Code == "DAM");
            db.Users.AddRange(
                new User { Username = "admin", FullName = "مشرف النظام", Role = UserRole.Admin, PasswordHash = hasher.Hash("123456") },
                new User { Username = "manager", FullName = "مدير النظام", Role = UserRole.Manager, PasswordHash = hasher.Hash("123456") },
                new User { Username = "head1", FullName = "رئيس قسم دمشق", Role = UserRole.Head, BranchId = damascus?.Id, PasswordHash = hasher.Hash("123456") },
                new User { Username = "lawyer1", FullName = "محامي دمشق", Role = UserRole.Lawyer, BranchId = damascus?.Id, PasswordHash = hasher.Hash("123456") });
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// تهيئة أول تشغيل لبيئات الإنتاج/التجريبية: عند غياب أي مستخدم يُنشأ مدير أول فقط
    /// بكلمة مرور تُحقن من الإعدادات (متغير البيئة Bootstrap__AdminPassword)،
    /// وإلا يُرمى خطأ صريح يمنع الإقلاع بحسابات افتراضية أو بنظام بلا مستخدمين.
    /// </summary>
    public static async Task BootstrapAsync(DocGeneratorDbContext db, IPasswordHasher hasher, string? adminPassword, CancellationToken ct = default)
    {
        if (db.Users.Any())
            return;

        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException(
                "لا يوجد مستخدمون في قاعدة البيانات، ولم يُضبط Bootstrap__AdminPassword. " +
                "عيّن كلمة مرور قوية لمدير النظام الأول عبر متغير البيئة ثم أعد التشغيل.");

        db.Users.Add(new User
        {
            Username = "admin",
            FullName = "مشرف النظام",
            Role = UserRole.Admin,
            PasswordHash = hasher.Hash(adminPassword),
        });
        await db.SaveChangesAsync(ct);
    }
}
