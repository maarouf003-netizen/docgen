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
        // شهر الملف هو تاريخ قيده؛ وإن لم يُقيد بعد (تحت رفع) فيُحسب بشهر إدخاله.
        var dates = await _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .Select(d => new
            {
                RegDate = d.RegistrationDate != null ? d.RegistrationDate.Date : null,
                d.CreatedAt,
            })
            .ToListAsync(ct);

        return GroupMonths(dates.Select(x => TryParseActionDate(x.RegDate) ?? x.CreatedAt.Date));
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

    /// <summary>صف خام لإحصاءات المدير/المحامي؛ تاريخ القيد نصّي في DB لذا RegDate من نوع string.</summary>
    private sealed class ManagerStatRow
    {
        public bool IsDraft { get; set; }
        public string? ExecStatus { get; set; }
        public string? ExecSubStatus { get; set; }
        public string? GeneralEntitySide { get; set; }
        public string? ExecutedStatus { get; set; }
        public decimal AmountNumeric { get; set; }
        public decimal Amount2Numeric { get; set; }
        public decimal? CollectedAmount { get; set; }
        public decimal? ExecutedRequiredAmount { get; set; }
        public decimal? ExecutedPaidAmount { get; set; }
        public DateTime? FileReceiptDate { get; set; }
        public string? RegDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public async Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .Select(d => new ManagerStatRow
            {
                IsDraft = d.IsDraft,
                ExecStatus = d.ExecStatus,
                ExecSubStatus = d.ExecSubStatus,
                GeneralEntitySide = d.GeneralEntitySide,
                ExecutedStatus = d.ExecutedStatus,
                AmountNumeric = d.AmountNumeric,
                Amount2Numeric = d.Amount2Numeric,
                CollectedAmount = d.CollectedAmount,
                ExecutedRequiredAmount = d.ExecutedRequiredAmount,
                ExecutedPaidAmount = d.ExecutedPaidAmount,
                FileReceiptDate = d.FileReceiptDate,
                RegDate = d.RegistrationDate != null ? d.RegistrationDate.Date : null,
                CreatedAt = d.CreatedAt,
            })
            .ToListAsync(ct);

        return AggregateManagerStats(rows, period, window);
    }

    public async Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.CreatedById == userId)
            .Select(d => new ManagerStatRow
            {
                IsDraft = d.IsDraft,
                ExecStatus = d.ExecStatus,
                ExecSubStatus = d.ExecSubStatus,
                GeneralEntitySide = d.GeneralEntitySide,
                ExecutedStatus = d.ExecutedStatus,
                AmountNumeric = d.AmountNumeric,
                Amount2Numeric = d.Amount2Numeric,
                CollectedAmount = d.CollectedAmount,
                ExecutedRequiredAmount = d.ExecutedRequiredAmount,
                ExecutedPaidAmount = d.ExecutedPaidAmount,
                FileReceiptDate = d.FileReceiptDate,
                RegDate = d.RegistrationDate != null ? d.RegistrationDate.Date : null,
                CreatedAt = d.CreatedAt,
            })
            .ToListAsync(ct);

        return AggregateManagerStats(rows, period, window);
    }

    private static ManagerStatsDto AggregateManagerStats(
        List<ManagerStatRow> rows, StatsPeriod period, (DateTime Start, DateTime End) window)
    {
        var active = 0;
        var drafts = 0;
        var deferred = 0;
        var settledCount = 0;
        decimal settledCollected = 0;
        var forcibleCount = 0;
        decimal forcibleCollected = 0;
        var tradingAgainstCount = 0;
        decimal tradingAgainstAmount = 0;
        var executedAgainstCount = 0;
        decimal executedAgainstAmount = 0;
        decimal activeAmount = 0;
        decimal draftsAmount = 0;
        decimal deferredAmount = 0;
        decimal activeAmount2 = 0;
        decimal draftsAmount2 = 0;
        decimal deferredAmount2 = 0;

        foreach (var r in rows)
        {
            // ملف «الجهة العامة منفذ عليها»: يُحتسب في بطاقة «متداول للضد» (المتداول فقط)
            // أو «منفذ للضد» (المنفذ فقط)، والمشطوب مستبعد من الاثنتين، وفترة الملف
            // من تاريخ وروده لا من تاريخ قيده (المقيد من الخصم لا من محامي الدولة).
            if (r.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            {
                if (r.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                    continue;

                var executedPeriodDate = r.FileReceiptDate?.Date ?? r.CreatedAt.Date;
                if (executedPeriodDate < window.Start || executedPeriodDate >= window.End)
                    continue;

                if (r.ExecutedStatus == ExecutedStatusCatalog.Executed)
                {
                    executedAgainstCount++;
                    executedAgainstAmount += r.ExecutedPaidAmount ?? 0;
                }
                else
                {
                    tradingAgainstCount++;
                    tradingAgainstAmount += r.ExecutedRequiredAmount ?? 0;
                }
                continue;
            }

            // فترة الملف: تاريخ قيده، وإن لم يُقيد بعد (تحت رفع) فشهر إدخاله.
            var periodDate = TryParseActionDate(r.RegDate) ?? r.CreatedAt.Date;
            if (periodDate < window.Start || periodDate >= window.End)
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
                draftsAmount += r.AmountNumeric;
                draftsAmount2 += r.Amount2Numeric;
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.Deferred)
            {
                deferred++;
                deferredAmount += r.AmountNumeric;
                deferredAmount2 += r.Amount2Numeric;
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                && r.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted)
            {
                active++;
                activeAmount += r.AmountNumeric;
                activeAmount2 += r.Amount2Numeric;
            }
            else if (string.IsNullOrEmpty(r.ExecStatus) && !r.IsDraft)
            {
                active++;
                activeAmount += r.AmountNumeric;
                activeAmount2 += r.Amount2Numeric;
            }
        }

        return new ManagerStatsDto(
            TotalFiles: active + drafts + deferred,
            Active: active,
            Drafts: drafts,
            Deferred: deferred,
            TotalAmount: activeAmount + draftsAmount + deferredAmount,
            ActiveAmount: activeAmount,
            DraftsAmount: draftsAmount,
            DeferredAmount: deferredAmount,
            TotalAmount2: activeAmount2 + draftsAmount2 + deferredAmount2,
            ActiveAmount2: activeAmount2,
            DraftsAmount2: draftsAmount2,
            DeferredAmount2: deferredAmount2,
            SettledCount: settledCount,
            SettledCollected: settledCollected,
            ForcibleCount: forcibleCount,
            ForcibleCollected: forcibleCollected,
            TradingAgainstCount: tradingAgainstCount,
            TradingAgainstAmount: tradingAgainstAmount,
            ExecutedAgainstCount: executedAgainstCount,
            ExecutedAgainstAmount: executedAgainstAmount,
            PeriodYear: window.Start.Year,
            PeriodQuarter: period == StatsPeriod.Quarterly ? (window.Start.Month - 1) / 3 + 1 : null,
            PeriodMonth: period == StatsPeriod.Monthly ? window.Start.Month : null);
    }

    public async Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.BranchId == branchId)
            .Select(d => new
            {
                d.CreatedById,
                RegDate = d.RegistrationDate != null ? d.RegistrationDate.Date : null,
                CreatedAt = d.CreatedAt,
            })
            .ToListAsync(ct);

        var lawyers = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Lawyer && u.BranchId == branchId && u.IsActive)
            .Select(u => new { u.Id, u.FullName, u.Username })
            .ToListAsync(ct);

        var counts = new Dictionary<int, Dictionary<(int Year, int Month), int>>();
        foreach (var r in rows)
        {
            // فترة الملف: تاريخ قيده، وإن لم يُقيد بعد (تحت رفع) فشهر إدخاله.
            var periodDate = TryParseActionDate(r.RegDate) ?? r.CreatedAt.Date;
            if (periodDate < window.Start || periodDate >= window.End)
                continue;

            var key = (periodDate.Year, periodDate.Month);
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
    /// الأشهر المتاحة (تاريخ القيد، وإن لم يُقيد بعد فشهر الإدخال) ضمن نطاق الفرع/المستخدم،
    /// لتغذية منتقي الفترة المحددة في الواجهة.
    /// </summary>
    public async Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(int? branchId, int? userId, CancellationToken ct = default)
    {
        var dates = await _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => userId == null || d.CreatedById == userId)
            .Select(d => new
            {
                RegDate = d.RegistrationDate != null ? d.RegistrationDate.Date : null,
                d.CreatedAt,
            })
            .ToListAsync(ct);

        return GroupMonths(dates.Select(x => TryParseActionDate(x.RegDate) ?? x.CreatedAt.Date));
    }

    private static List<MonthlyStatDto> GroupMonths(IEnumerable<DateTime> dates)
    {
        var counts = new Dictionary<(int Year, int Month), int>();
        foreach (var date in dates)
        {
            var key = (date.Year, date.Month);
            counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
        }

        return counts
            .OrderBy(k => k.Key.Year)
            .ThenBy(k => k.Key.Month)
            .Select(k => new MonthlyStatDto(k.Key.Year, k.Key.Month, k.Value))
            .ToList();
    }

    /// <summary>
    /// نطاق الفترة: شهري = شهر محدد (افتراضيًا الحالي)، ربعي = ربع محدد (افتراضيًا الحالي)،
    /// عام = سنة محددة (افتراضيًا الحالية). النطاق نصف مفتوح [Start, End).
    /// year/month/quarter تُتحقق من صحة قيمها في المتحكم قبل الوصول إلى هنا.
    /// </summary>
    private static (DateTime Start, DateTime End) GetPeriodWindow(StatsPeriod period,
        int? year = null, int? month = null, int? quarter = null)
    {
        var now = DateTime.Now;
        var months = period switch
        {
            StatsPeriod.Monthly => 1,
            StatsPeriod.Quarterly => 3,
            _ => 12,
        };
        var startYear = year ?? now.Year;
        var startMonth = period switch
        {
            StatsPeriod.Monthly => month ?? now.Month,
            StatsPeriod.Quarterly when quarter is >= 1 and <= 4 => (quarter!.Value - 1) * 3 + 1,
            StatsPeriod.Quarterly => ((now.Month - 1) / 3) * 3 + 1,
            _ => 1,
        };
        var start = new DateTime(startYear, startMonth, 1);
        return (start, start.AddMonths(months));
    }

    public async Task<List<ReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _db.ExecutionActions.AsNoTracking()
            .Where(a => a.ReminderDuration != null || a.ReminderColor != null)
            .Where(a => a.Document.CreatedById == userId)
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
