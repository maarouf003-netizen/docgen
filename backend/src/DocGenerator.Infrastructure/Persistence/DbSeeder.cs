using System.Security.Cryptography;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// بذر الفروع والمستخدمين لبيئة التطوير فقط؛ أما الإنتاج فلا يُنشئ حسابات افتراضية بكلمة معروفة.
/// كلمة مرور التطوير عشوائية عند كل بذر (تُطبَع على الكونسول المحلي لمرة واحدة) ما لم تُمرَّر
/// صراحة عبر <c>Bootstrap:DevSeedPassword</c> (تجاوز للاختبارات المعزولة فقط) — RF-006 (SEC-004).
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// يبذر الفروع وحسابات التطوير الأربعة، ويعيد كلمة المرور المستخدمة (للطباعة/الاختبار).
    /// لا تُسجَّل الكلمة في ملفات السجلات أبدًا — الكونسول المحلي فقط.
    /// </summary>
    public static async Task<string> SeedAsync(
        DocGeneratorDbContext db,
        IPasswordHasher hasher,
        CancellationToken ct = default,
        string? devPassword = null)
    {
        var password = string.IsNullOrWhiteSpace(devPassword) ? GenerateDevPassword() : devPassword;
        if (string.IsNullOrWhiteSpace(devPassword))
        {
            // كونسول محلي فقط (لا Serilog ولا ملفات): بيئة التطوير وحدها تصل هنا.
            Console.WriteLine($"[DbSeeder] Development accounts seeded. One-time password: {password}");
        }
        if (!db.Branches.Any())
        {
            db.Branches.AddRange(
                new Branch { Name = "الفرع الرئيسي - دمشق", Code = "DAM", Address = "دمشق", Governorate = "دمشق" },
                new Branch { Name = "فرع حلب", Code = "ALP", Address = "حلب", Governorate = "حلب" },
                new Branch { Name = "فرع حمص", Code = "HMS", Address = "حمص", Governorate = "حمص" },
                new Branch { Name = "فرع اللاذقية", Code = "LAT", Address = "اللاذقية", Governorate = "اللاذقية" },
                new Branch { Name = "فرع طرطوس", Code = "TAR", Address = "طرطوس", Governorate = "طرطوس" });
            await db.SaveChangesAsync(ct);
        }

        if (!db.Users.Any())
        {
            var damascus = db.Branches.FirstOrDefault(b => b.Code == "DAM");
            db.Users.AddRange(
                new User { Username = "admin", FullName = "مشرف النظام", Role = UserRole.Admin, PasswordHash = hasher.Hash(password) },
                new User { Username = "manager", FullName = "مدير النظام", Role = UserRole.Manager, PasswordHash = hasher.Hash(password) },
                new User { Username = "head1", FullName = "رئيس قسم دمشق", Role = UserRole.Head, BranchId = damascus?.Id, PasswordHash = hasher.Hash(password) },
                new User { Username = "lawyer1", FullName = "محامي دمشق", Role = UserRole.Lawyer, BranchId = damascus?.Id, PasswordHash = hasher.Hash(password) });
            await db.SaveChangesAsync(ct);
        }

        return password;
    }

    /// <summary>
    /// كلمة عشوائية 12 خانة من أبجدية بلا التباس (تُستخدَم عند غياب التجاوز الصريح فقط).
    /// </summary>
    private static string GenerateDevPassword()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";
        return string.Create(12, alphabet, static (span, chars) =>
        {
            var bytes = RandomNumberGenerator.GetBytes(span.Length);
            for (var i = 0; i < span.Length; i++)
                span[i] = chars[bytes[i] % chars.Length];
        });
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
