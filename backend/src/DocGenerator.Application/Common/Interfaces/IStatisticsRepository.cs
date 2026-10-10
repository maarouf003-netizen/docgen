using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات الإحصائيات المنفّذة على مستوى قاعدة البيانات
/// (تجميع في DB بدل تحميل كل المستندات في الذاكرة).
/// </summary>
public interface IStatisticsRepository
{
    /// <summary>
    /// بطاقة اللوحة الإجمالية — بالنطاق (§12/قرار 27): `fullAccess` للفرع ككل،
    /// وإلا دوائر القسم وبلا دائرة (قسم) أو دوائر الشعبة.
    /// </summary>
    Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);

    /// <summary>
    /// السلسلة الشهرية — بالنطاق نفسه (§12/قرار 27).
    /// </summary>
    Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);
    Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default);
    Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default);
    Task<List<ReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// بطاقات إحصاءات المدير على الملفات ضمن نطاق الفترة.
    /// فترة الملف هي تاريخ قيده (RegistrationDate)؛ وإن لم يُقيد بعد (تحت رفع) فشهر إدخاله (CreatedAt).
    /// year/month/quarter اختيارية لاختيار فترة سابقة؛ إن غابت تُستخدم الفترة الحالية.
    /// نطاق المالك (§12/قرار 27): `fullAccess` للفرع ككل (إدارة)، وإلا دوائر
    /// القسم وبلا دائرة (`ownerSectionId` فارغ) أو دوائر الشعبة.
    /// </summary>
    Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);

    /// <summary>
    /// جدول محامي فرع محدد بتوزيعهم الشهري داخل نطاق الفترة — العدّ ضمن نطاق
    /// المالك (§12): ملف المحامي في نطاقين يُحتسب حيث دائرته فقط.
    /// </summary>
    Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);

    /// <summary>بطاقات إحصاءات الفترة لملفات مستخدم محدد (المحامي على ملفاته هو).</summary>
    Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default);

    /// <summary>
    /// الأشهر المتاحة (تاريخ القيد، وإن لم يُقيد بعد فشهر الإدخال)، ضمن نطاق
    /// الفرع/المستخدم/المالك (§12).
    /// </summary>
    Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(int? branchId, int? userId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);
}
