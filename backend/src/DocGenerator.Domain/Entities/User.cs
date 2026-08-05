using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Lawyer;
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// نسخة التوكن (Security Stamp): تُرفع عند تغيير كلمة المرور أو تعطيل الحساب
    /// لإبطال كل الرموز الصادرة سابقًا.
    /// </summary>
    public int TokenVersion { get; set; }
    /// <summary>
    /// عدد محاولات الدخول الفاشلة المتتالية على مستوى الحساب،
    /// يُصفَّر عند نجاح الدخول أو عند انتهاء مدة القفل.
    /// </summary>
    public int FailedLoginCount { get; set; }
    /// <summary>
    /// نهاية فترة قفل الحساب (UTC). الحساب يُرفض دخوله عندما تكون أكبر من الآن.
    /// </summary>
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }

    public Branch? Branch { get; set; }
    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public bool HasFullAccess =>
        Role is UserRole.Manager or UserRole.Admin;

    /// <summary>
    /// الحذف والاستعادة المنطقيان للملفات من اختصاص المحامي صاحب الملف.
    /// </summary>
    public bool CanDeleteDocuments =>
        Role == UserRole.Lawyer;
}
