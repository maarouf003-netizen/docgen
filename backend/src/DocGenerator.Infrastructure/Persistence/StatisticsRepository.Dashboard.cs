using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public partial class StatisticsRepository
{
    public async Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
    {
        // الملفات المشطوبة (وضع «منفذ عليه») مستثناة من الإحصائيات كما هي مستثناة من
        // القوائم والتصدير؛ سجلها الوحيد هو صفحة «الملفات المشطوبة».
        var q = InOwnerScope(_db.Documents.AsNoTracking(), ownerSectionId, fullAccess)
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff);

        // كل ملف له حالة واحدة محددة تُحسب مباشرة بشرطها الخاص:
        //   تحت رفع  = مسودة بلا حالة تنفيذ
        //   متداول   = مقيد بلا حالة تنفيذ، أو "منفذ جبريا" بحالة "منفذ جزئيا"
        //   منفذ     = "منفذ بالتسوية"، أو "منفذ جبريا" غير "منفذ جزئيا"، أو "منفذ إنابة" (المناب)
        //   تريث     = "تريث"
        var totals = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Drafts = g.Count(d => d.IsDraft && string.IsNullOrEmpty(d.ExecStatus)),
                Executed = g.Count(d => d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                    || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                    || d.ExecStatus == ExecutionStatusCatalog.Recovered
                    || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        // FR-004: صيغة صريحة آمنة لـ NULL (مرآة القوائم H4) — الجزئي مطابقة
                        // تامة فقط، وNULL منفذ كامل؛ بلا اعتماد على منطق SQL الثلاثي المتفاوت بين المزودين.
                        && (d.ExecSubStatus == null
                            || d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted))),
                Deferred = g.Count(d => d.ExecStatus == ExecutionStatusCatalog.Deferred),
                Active = g.Count(d => (string.IsNullOrEmpty(d.ExecStatus) && !d.IsDraft)
                    || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        && d.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted)),
            })
            .FirstOrDefaultAsync(ct);

        // جمع المبالغ عميل-side بالنوع decimal بدل Sum(double) الذي يفقد الدقة؛
        // يعمل على SQLite وPostgreSQL معًا دون أي تعديل عند الترحيل.
        // الملف المصرفي يضع مبلغه في AmountNumeric والعادي في InclusionAmountNumeric.
        // ب6: الملفات المنابة مستثناة من مبالغ اللوحة — مبالغها نسخ للمنيب ولا تُحسب مرتين.
        var amounts = await q
            .Select(d => new { d.IsDraft, d.ContractTypeSelector, d.AmountNumeric, d.InclusionAmountNumeric, d.CollectedAmount, d.SourceDelegationId })
            .ToListAsync(ct);

        // مبلغ الملف يُقرأ من مجموعة واحدة حسب نوع السند: المصرفي من Amount*،
        // والعادي من InclusionAmount* — كما في AccumulateContract، لا جمعًا للمجموعتين.
        var totalAmount = amounts.Where(d => !d.IsDraft && d.SourceDelegationId == null)
            .Sum(d => ContractTypeCatalog.IsBanking(d.ContractTypeSelector) ? d.AmountNumeric : d.InclusionAmountNumeric);
        // ب6: محصّلات المناب نسخةٌ للمنيب — تُستثنى من إجمالي المحصّل كما مبالغ الأساس.
        var totalCollectedAmount = amounts.Where(d => d.SourceDelegationId == null).Sum(d => d.CollectedAmount) ?? 0;

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

    public async Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
    {
        // شهر الملف هو تاريخ قيده؛ وإن لم يُقيد بعد (تحت رفع) فيُحسب بشهر إدخاله.
        // الملفات المشطوبة مستثناة لتوافق الشهري مع القوائم والتصدير.
        var dates = await InOwnerScope(_db.Documents.AsNoTracking(), ownerSectionId, fullAccess)
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new
            {
                RegDateParsed = d.RegistrationDate != null ? d.RegistrationDate.DateParsed : null,
                d.CreatedAt,
            })
            .ToListAsync(ct);

        return GroupMonths(dates.Select(x => x.RegDateParsed ?? LocalDateOf(x.CreatedAt)));
    }

    public async Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default)
    {
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.BranchId != null)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new { d.BranchId, d.IsDraft, d.ContractTypeSelector, d.AmountNumeric, d.InclusionAmountNumeric, d.SourceDelegationId })
            .ToListAsync(ct);

        var grouped = rows
            .Where(d => d.BranchId != null)
            .GroupBy(d => d.BranchId!.Value)
            .Select(g => new
            {
                BranchId = g.Key,
                Total = g.Count(),
                Drafts = g.Count(d => d.IsDraft),
                // ب6: المبالغ الحقيقية للمنيب فقط؛ المناب عدد بلا مبالغ (لا تُحسب نسخه مرتين).
                Amount = g.Where(d => !d.IsDraft && d.SourceDelegationId == null)
                    .Sum(d => ContractTypeCatalog.IsBanking(d.ContractTypeSelector) ? d.AmountNumeric : d.InclusionAmountNumeric),
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
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .GroupBy(d => d.CreatedById)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
            })
            .ToListAsync(ct);

        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToListAsync(ct);

        return users
            .Select(u => new UserActivityDto(
                u.Username,
                u.FullName,
                grouped.FirstOrDefault(g => g.UserId == u.Id)?.Count ?? 0))
            .OrderByDescending(a => a.DocumentCount)
            .ToList();
    }

    /// <summary>
    /// صف خام لإحصاءات المدير/المحامي. فترة الملف تُحسب في SQL عبر PeriodDate:
    /// تاريخ القيد المحلول (DateParsed) أو تاريخ الإدخال عند غيابه،
    /// ولعائلة وضع «الجهة العامة منفذ عليها» (Executed + Deposit) من تاريخ ورود الاخطار.
    /// </summary>
    private sealed class ManagerStatRow
    {
        public bool IsDraft { get; set; }
        public string? ExecStatus { get; set; }
        public string? ExecSubStatus { get; set; }
        public string? GeneralEntitySide { get; set; }
        public string? ExecutedStatus { get; set; }
        public bool WasDepositExecuted { get; set; }
        public string? ContractTypeSelector { get; set; }
        public decimal AmountNumeric { get; set; }
        public string? Currency { get; set; }
        public decimal Amount2Numeric { get; set; }
        public string? Currency2 { get; set; }
        public decimal Amount3Numeric { get; set; }
        public string? Currency3 { get; set; }
        public decimal InclusionAmountNumeric { get; set; }
        public string? InclusionCurrency { get; set; }
        public decimal InclusionAmount2Numeric { get; set; }
        public string? InclusionCurrency2 { get; set; }
        public decimal InclusionAmount3Numeric { get; set; }
        public string? InclusionCurrency3 { get; set; }
        public decimal? CollectedAmount { get; set; }
        public decimal? CollectedAmount2 { get; set; }
        public decimal? CollectedAmount3 { get; set; }
        public string? CollectedCurrency { get; set; }
        public string? CollectedCurrency2 { get; set; }
        public string? CollectedCurrency3 { get; set; }
        public decimal? ExecutedRequiredAmount { get; set; }
        public string? ExecutedRequiredCurrency { get; set; }
        public decimal? ExecutedRequiredAmount2 { get; set; }
        public string? ExecutedRequiredCurrency2 { get; set; }
        public decimal? ExecutedRequiredAmount3 { get; set; }
        public string? ExecutedRequiredCurrency3 { get; set; }
        public decimal? ExecutedPaidAmount { get; set; }
        public string? ExecutedPaidCurrency { get; set; }
        public decimal? ExecutedPaidAmount2 { get; set; }
        public string? ExecutedPaidCurrency2 { get; set; }
        public decimal? ExecutedPaidAmount3 { get; set; }
        public string? ExecutedPaidCurrency3 { get; set; }
        /// <summary>المناب: يُعدّ في العدادات بلا مبالغ (نسخة من المنيب لا تُحسب مرتين).</summary>
        public int? SourceDelegationId { get; set; }
        /// <summary>مجموع بدل المبيع لإنابات الملف المنفذة (تُضاف لسلة «منفذ جبريا» عند اعتباره منفذًا).</summary>
        public decimal? DelegationSalesAmount { get; set; }
        public DateTime PeriodDate { get; set; }
        /// <summary>حُسبت فترة الملف بتاريخ إدخاله لسقوط تاريخه المعتمد (وسم المصدر).</summary>
        public bool PeriodDateFromCreatedAt { get; set; }
        /// <summary>
        /// سبب الاحتياط لجهتي «منفذ عليها/إيداع»: غياب تاريخ ورود الإخطار (لا تعذّر تحليل تاريخ القيد —
        /// تاريخ القيد ليس التاريخ المعتمد لهاتين الجهتين أصلًا). يُستخدم لفصل العدّاد والتصريح الدقيق.
        /// </summary>
        public bool PeriodDateReceiptMissing { get; set; }
    }

}
