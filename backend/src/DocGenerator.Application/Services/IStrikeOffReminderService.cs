using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

/// <summary>
/// بطاقة «تذكيرات الشطب»: ملفات «طالبة تنفيذ» النشطة التي مضى على آخر إجراء مسجّل فيها
/// عتبة الأشهر (قبل مهلة الشطب القانونية) — تُحسب عند القراءة (لا جدول تذكيرات ولا مهمة خلفية).
/// القاعدة الكاملة في <c>docs/STRIKE_OFF_REMINDERS_PLAN.md</c> §1.
/// </summary>
public interface IStrikeOffReminderService
{
    /// <summary>صفحة من الملفات المستحقة (الأقدم ركودًا أولًا) بنطاق المستخدم.</summary>
    Task<PagedResult<DocumentResponse>> GetAsync(
        int page, int perPage, int? visibleBranchId, int? visibleUserId, int? ownerSectionId,
        CancellationToken ct = default);

    /// <summary>عدد الملفات المستحقة (لشارة اللوحة).</summary>
    Task<int> GetCountAsync(
        int? visibleBranchId, int? visibleUserId, int? ownerSectionId, CancellationToken ct = default);

    /// <summary>
    /// إلغاء تذكير ملف (مالكه فقط): يكتب لحظة الإلغاء UTC + تدقيق. يعيد `false` إن لم يوجد
    /// الملف أو لم يكن للمستخدم. الإلغاء يُبطَل منطقيًا عند تسجيل إجراء أحدث منه.
    /// </summary>
    Task<bool> DismissAsync(int documentId, int ownerId, string? actorName, CancellationToken ct = default);
}
