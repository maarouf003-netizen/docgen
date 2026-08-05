using System.Globalization;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تجميع العدادات في قاعدة البيانات عبر GroupBy/Count؛
/// وجمع المبالغ عميل-side بالنوع decimal للحفاظ على الدقة دون Sum(double).
/// </summary>
public class StatisticsRepository : IStatisticsRepository
{
    private readonly DocGeneratorDbContext _db;

    public StatisticsRepository(DocGeneratorDbContext db) => _db = db;

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default)
    {
        var q = _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId);

        // كل ملف له حالة واحدة محددة تُحسب مباشرة بشرطها الخاص:
        //   تحت رفع  = مسودة بلا حالة تنفيذ
        //   متداول   = مقيد بلا حالة تنفيذ، أو "منفذ جبريا" بحالة "منفذ جزئيا"
        //   منفذ     = "منفذ بالتسوية"، أو "منفذ جبريا" غير "منفذ جزئيا"
        //   تريث     = "تريث"
        var totals = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Drafts = g.Count(d => d.IsDraft && string.IsNullOrEmpty(d.ExecStatus)),
                Executed = g.Count(d => d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                    || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        && d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)),
                Deferred = g.Count(d => d.ExecStatus == ExecutionStatusCatalog.Deferred),
                Active = g.Count(d => (string.IsNullOrEmpty(d.ExecStatus) && !d.IsDraft)
                    || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        && d.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted)),
            })
            .FirstOrDefaultAsync(ct);

        // جمع المبالغ عميل-side بالنوع decimal بدل Sum(double) الذي يفقد الدقة؛
        // يعمل على SQLite وPostgreSQL معًا دون أي تعديل عند الترحيل.
        var amounts = await q
            .Select(d => new { d.IsDraft, d.AmountNumeric, d.CollectedAmount })
            .ToListAsync(ct);

        var totalAmount = amounts.Where(d => !d.IsDraft).Sum(d => d.AmountNumeric);
        var totalCollectedAmount = amounts.Sum(d => d.CollectedAmount) ?? 0;

        var borrowers = await q
            .Where(d => d.BorrowerName != null && d.BorrowerName != string.Empty)
            .Select(d => (d.BorrowerName ?? string.Empty) + "-" + (d.BorrowerFamily ?? string.Empty))
            .Distinct()
            .CountAsync(ct);

        return new DashboardStatsDto(
            TotalDocuments: totals?.Total ?? 0,
            TotalDrafts: totals?.Drafts ?? 0,
            TotalExecuted: totals?.Executed ?? 0,
            TotalDeferred: totals?.Deferred ?? 0,
            TotalActive: totals?.Active ?? 0,
            TotalBorrowers: borrowers,
            TotalAmount: totalAmount,
            TotalCollectedAmount: totalCollectedAmount);
    }

    public async Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default)
    {
        return await _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyStatDto(g.Key.Year, g.Key.Month, g.Count()))
            .ToListAsync(ct);
    }

    public async Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default)
    {
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.BranchId != null)
            .Select(d => new { d.BranchId, d.IsDraft, d.AmountNumeric })
            .ToListAsync(ct);

        var grouped = rows
            .Where(d => d.BranchId != null)
            .GroupBy(d => d.BranchId!.Value)
            .Select(g => new
            {
                BranchId = g.Key,
                Total = g.Count(),
                Drafts = g.Count(d => d.IsDraft),
                Amount = g.Where(d => !d.IsDraft).Sum(d => d.AmountNumeric),
            })
            .ToList();

        var branches = await _db.Branches.AsNoTracking()
            .Select(b => new { b.Id, b.Name })
            .ToListAsync(ct);

        return branches
            .Select(b =>
            {
                var stat = grouped.FirstOrDefault(g => g.BranchId == b.Id);
                return new BranchSummaryDto(
                    b.Id,
                    b.Name,
                    stat?.Total ?? 0,
                    stat?.Drafts ?? 0,
                    stat?.Amount ?? 0);
            })
            .ToList();
    }

    public async Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default)
    {
        var grouped = await _db.Documents.AsNoTracking()
            .GroupBy(d => d.CreatedById)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Views = g.Sum(d => d.ViewCount),
            })
            .ToListAsync(ct);

        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToListAsync(ct);

        return users
            .Select(u => new UserActivityDto(
                u.Username,
                u.FullName,
                grouped.FirstOrDefault(g => g.UserId == u.Id)?.Count ?? 0,
                grouped.FirstOrDefault(g => g.UserId == u.Id)?.Views ?? 0))
            .OrderByDescending(a => a.DocumentCount)
            .ToList();
    }

    public async Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .Select(d => new
            {
                d.IsDraft,
                d.ExecStatus,
                d.ExecSubStatus,
                d.CollectedAmount,
                RegDate = d.RegistrationDate != null ? d.RegistrationDate.Date : null,
            })
            .ToListAsync(ct);

        var active = 0;
        var drafts = 0;
        var deferred = 0;
        var settledCount = 0;
        decimal settledCollected = 0;
        var forcibleCount = 0;
        decimal forcibleCollected = 0;

        foreach (var r in rows)
        {
            var regDate = TryParseActionDate(r.RegDate);
            if (regDate is null || regDate.Value < window.Start || regDate.Value >= window.End)
                continue;

            if (r.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement)
            {
                settledCount++;
                settledCollected += r.CollectedAmount ?? 0;
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                && r.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)
            {
                forcibleCount++;
                forcibleCollected += r.CollectedAmount ?? 0;
            }
            else if (r.IsDraft && string.IsNullOrEmpty(r.ExecStatus))
            {
                drafts++;
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.Deferred)
            {
                deferred++;
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                && r.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted)
            {
                active++;
            }
            else if (string.IsNullOrEmpty(r.ExecStatus) && !r.IsDraft)
            {
                active++;
            }
        }

        return new ManagerStatsDto(
            TotalFiles: active + drafts + deferred,
            Active: active,
            Drafts: drafts,
            Deferred: deferred,
            SettledCount: settledCount,
            SettledCollected: settledCollected,
            ForcibleCount: forcibleCount,
            ForcibleCollected: forcibleCollected,
            PeriodYear: window.Start.Year,
            PeriodQuarter: period == StatsPeriod.Quarterly ? (window.Start.Month - 1) / 3 + 1 : null,
            PeriodMonth: period == StatsPeriod.Monthly ? window.Start.Month : null);
    }

    public async Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.BranchId == branchId)
            .Select(d => new
            {
                d.CreatedById,
                RegDate = d.RegistrationDate != null ? d.RegistrationDate.Date : null,
            })
            .ToListAsync(ct);

        var lawyers = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Lawyer && u.BranchId == branchId && u.IsActive)
            .Select(u => new { u.Id, u.FullName, u.Username })
            .ToListAsync(ct);

        var counts = new Dictionary<int, Dictionary<(int Year, int Month), int>>();
        foreach (var r in rows)
        {
            var regDate = TryParseActionDate(r.RegDate);
            if (regDate is null || regDate.Value < window.Start || regDate.Value >= window.End)
                continue;

            var key = (regDate.Value.Year, regDate.Value.Month);
            if (!counts.TryGetValue(r.CreatedById, out var months))
            {
                months = new Dictionary<(int, int), int>();
                counts[r.CreatedById] = months;
            }
            months[key] = months.TryGetValue(key, out var existing) ? existing + 1 : 1;
        }

        var points = new List<(int Year, int Month)>();
        for (var cursor = window.Start; cursor < window.End; cursor = cursor.AddMonths(1))
            points.Add((cursor.Year, cursor.Month));

        return lawyers
            .Select(l =>
            {
                var months = counts.TryGetValue(l.Id, out var m) ? m : new Dictionary<(int, int), int>();
                var lawyerPoints = points
                    .Select(p => new ManagerPeriodPointDto(
                        p.Year,
                        p.Month,
                        months.TryGetValue((p.Year, p.Month), out var c) ? c : 0))
                    .ToList();
                return new ManagerLawyerStatDto(
                    l.Id,
                    string.IsNullOrWhiteSpace(l.FullName) ? l.Username : l.FullName,
                    lawyerPoints.Sum(p => p.Count),
                    lawyerPoints);
            })
            .OrderByDescending(l => l.TotalCount)
            .ThenBy(l => l.LawyerName)
            .ToList();
    }

    /// <summary>
    /// نطاق الفترة الحالية: شهري = الشهر الحالي، ربعي = الربع الحالي، عام = السنة الحالية.
    /// النطاق نصف مفتوح [Start, End).
    /// </summary>
    private static (DateTime Start, DateTime End) GetPeriodWindow(StatsPeriod period)
    {
        var now = DateTime.Now;
        var months = period switch
        {
            StatsPeriod.Monthly => 1,
            StatsPeriod.Quarterly => 3,
            _ => 12,
        };
        var start = period switch
        {
            StatsPeriod.Monthly => new DateTime(now.Year, now.Month, 1),
            StatsPeriod.Quarterly => new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1),
            _ => new DateTime(now.Year, 1, 1),
        };
        return (start, start.AddMonths(months));
    }

    public async Task<List<ReminderDto>> GetRemindersAsync(int? branchId, int? userId, CancellationToken ct = default)
    {
        var rows = await _db.ExecutionActions.AsNoTracking()
            .Where(a => a.ReminderDuration != null || a.ReminderColor != null)
            .Where(a => branchId == null || a.Document.BranchId == branchId)
            .Where(a => userId == null || a.Document.CreatedById == userId)
            .Select(a => new
            {
                a.Id,
                a.DocumentId,
                a.Document.DocumentType,
                a.Document.BorrowerName,
                a.Document.BorrowerFather,
                a.Document.BorrowerFamily,
                a.Text,
                a.ActionDate,
                a.ReminderDuration,
                a.ReminderColor,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ReminderDto(
                r.Id,
                r.DocumentId,
                r.DocumentType,
                r.BorrowerName,
                r.BorrowerFather,
                r.BorrowerFamily,
                r.Text,
                r.ActionDate,
                r.ReminderDuration,
                r.ReminderColor,
                ComputeDueDate(r.ActionDate, r.ReminderDuration, r.CreatedAt)))
            .OrderBy(r => r.DueDate)
            .ThenBy(r => r.DocumentId)
            .ToList();
    }

    /// <summary>
    /// تاريخ الاستحقاق = تاريخ الإجراء + مدة التذكير،
    /// وإن غاب تاريخ الإجراء فتاريخ الإنشاء + مدة التذكير.
    /// </summary>
    private static DateTime ComputeDueDate(string? actionDate, string? duration, DateTime createdAt)
    {
        var baseDate = TryParseActionDate(actionDate) ?? createdAt;
        return baseDate.Date.AddDays(DurationDays(duration));
    }

    private static DateTime? TryParseActionDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var formats = new[]
        {
            "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy",
            "yyyy-MM-dd", "d/M/yy", "dd/MM/yy",
        };
        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return parsed.Date;

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var loose))
            return loose.Date;

        return null;
    }

    private static int DurationDays(string? duration) => duration switch
    {
        "3 أيام" => 3,
        "أسبوع" => 7,
        "أسبوعين" => 14,
        "شهر" => 30,
        _ => 0,
    };
}
