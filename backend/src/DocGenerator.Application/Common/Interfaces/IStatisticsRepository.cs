using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات الإحصائيات المنفّذة على مستوى قاعدة البيانات
/// (تجميع في DB بدل تحميل كل المستندات في الذاكرة).
/// </summary>
public interface IStatisticsRepository
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default);
    Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default);
    Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default);
    Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default);
    Task<List<ReminderDto>> GetRemindersAsync(int? branchId, int? userId, CancellationToken ct = default);

    /// <summary>بطاقات إحصاءات المدير على الملفات المقيَّدة في نطاق الفترة.</summary>
    Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId, CancellationToken ct = default);

    /// <summary>جدول محامي فرع محدد بتوزيعهم الشهري داخل نطاق الفترة.</summary>
    Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId, CancellationToken ct = default);
}
