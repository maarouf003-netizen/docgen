using DocGenerator.Application.Common;
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
        // الملفات المشطوبة (وضع «منفذ عليه») مستثناة من الإحصائيات كما هي مستثناة من
        // القوائم والتصدير؛ سجلها الوحيد هو صفحة «الملفات المشطوبة».
        var q = _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff);

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
        // الملف المصرفي يضع مبلغه في AmountNumeric والعادي في InclusionAmountNumeric.
        var amounts = await q
            .Select(d => new { d.IsDraft, d.AmountNumeric, d.InclusionAmountNumeric, d.CollectedAmount })
            .ToListAsync(ct);

        var totalAmount = amounts.Where(d => !d.IsDraft)
            .Sum(d => d.AmountNumeric + d.InclusionAmountNumeric);
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
        // الملفات المشطوبة مستثناة لتوافق الشهري مع القوائم والتصدير.
        var dates = await _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new
            {
                RegDateParsed = d.RegistrationDate != null ? d.RegistrationDate.DateParsed : null,
                d.CreatedAt,
            })
            .ToListAsync(ct);

        return GroupMonths(dates.Select(x => x.RegDateParsed ?? x.CreatedAt.Date));
    }

    public async Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default)
    {
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.BranchId != null)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new { d.BranchId, d.IsDraft, d.AmountNumeric, d.InclusionAmountNumeric })
            .ToListAsync(ct);

        var grouped = rows
            .Where(d => d.BranchId != null)
            .GroupBy(d => d.BranchId!.Value)
            .Select(g => new
            {
                BranchId = g.Key,
                Total = g.Count(),
                Drafts = g.Count(d => d.IsDraft),
                Amount = g.Where(d => !d.IsDraft).Sum(d => d.AmountNumeric + d.InclusionAmountNumeric),
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
        public DateTime PeriodDate { get; set; }
    }

    public async Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new ManagerStatRow
            {
                IsDraft = d.IsDraft,
                ExecStatus = d.ExecStatus,
                ExecSubStatus = d.ExecSubStatus,
                GeneralEntitySide = d.GeneralEntitySide,
                ExecutedStatus = d.ExecutedStatus,
                ContractTypeSelector = d.ContractTypeSelector,
                AmountNumeric = d.AmountNumeric,
                Currency = d.Currency,
                Amount2Numeric = d.Amount2Numeric,
                Currency2 = d.Currency2,
                Amount3Numeric = d.Amount3Numeric,
                Currency3 = d.Currency3,
                InclusionAmountNumeric = d.InclusionAmountNumeric,
                InclusionCurrency = d.InclusionCurrency,
                InclusionAmount2Numeric = d.InclusionAmount2Numeric,
                InclusionCurrency2 = d.InclusionCurrency2,
                InclusionAmount3Numeric = d.InclusionAmount3Numeric,
                InclusionCurrency3 = d.InclusionCurrency3,
                CollectedAmount = d.CollectedAmount,
                CollectedAmount2 = d.CollectedAmount2,
                CollectedAmount3 = d.CollectedAmount3,
                CollectedCurrency = d.CollectedCurrency,
                CollectedCurrency2 = d.CollectedCurrency2,
                CollectedCurrency3 = d.CollectedCurrency3,
                ExecutedRequiredAmount = d.ExecutedRequiredAmount,
                ExecutedRequiredCurrency = d.ExecutedRequiredCurrency,
                ExecutedRequiredAmount2 = d.ExecutedRequiredAmount2,
                ExecutedRequiredCurrency2 = d.ExecutedRequiredCurrency2,
                ExecutedRequiredAmount3 = d.ExecutedRequiredAmount3,
                ExecutedRequiredCurrency3 = d.ExecutedRequiredCurrency3,
                ExecutedPaidAmount = d.ExecutedPaidAmount,
                PeriodDate = d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                        ? d.FileReceiptDate ?? d.CreatedAt
                        : d.RegistrationDate!.DateParsed ?? d.CreatedAt,
            })
            .Where(r => r.PeriodDate >= window.Start && r.PeriodDate < window.End)
            .ToListAsync(ct);

        return AggregateManagerStats(rows, period, window);
    }

    public async Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.CreatedById == userId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new ManagerStatRow
            {
                IsDraft = d.IsDraft,
                ExecStatus = d.ExecStatus,
                ExecSubStatus = d.ExecSubStatus,
                GeneralEntitySide = d.GeneralEntitySide,
                ExecutedStatus = d.ExecutedStatus,
                ContractTypeSelector = d.ContractTypeSelector,
                AmountNumeric = d.AmountNumeric,
                Currency = d.Currency,
                Amount2Numeric = d.Amount2Numeric,
                Currency2 = d.Currency2,
                Amount3Numeric = d.Amount3Numeric,
                Currency3 = d.Currency3,
                InclusionAmountNumeric = d.InclusionAmountNumeric,
                InclusionCurrency = d.InclusionCurrency,
                InclusionAmount2Numeric = d.InclusionAmount2Numeric,
                InclusionCurrency2 = d.InclusionCurrency2,
                InclusionAmount3Numeric = d.InclusionAmount3Numeric,
                InclusionCurrency3 = d.InclusionCurrency3,
                CollectedAmount = d.CollectedAmount,
                CollectedAmount2 = d.CollectedAmount2,
                CollectedAmount3 = d.CollectedAmount3,
                CollectedCurrency = d.CollectedCurrency,
                CollectedCurrency2 = d.CollectedCurrency2,
                CollectedCurrency3 = d.CollectedCurrency3,
                ExecutedRequiredAmount = d.ExecutedRequiredAmount,
                ExecutedRequiredCurrency = d.ExecutedRequiredCurrency,
                ExecutedRequiredAmount2 = d.ExecutedRequiredAmount2,
                ExecutedRequiredCurrency2 = d.ExecutedRequiredCurrency2,
                ExecutedRequiredAmount3 = d.ExecutedRequiredAmount3,
                ExecutedRequiredCurrency3 = d.ExecutedRequiredCurrency3,
                ExecutedPaidAmount = d.ExecutedPaidAmount,
                PeriodDate = d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                        ? d.FileReceiptDate ?? d.CreatedAt
                        : d.RegistrationDate!.DateParsed ?? d.CreatedAt,
            })
            .Where(r => r.PeriodDate >= window.Start && r.PeriodDate < window.End)
            .ToListAsync(ct);

        return AggregateManagerStats(rows, period, window);
    }

    /// <summary>العملات المعروفة في شاشة الإحصاءات بترتيب العرض الثابت.</summary>
    private static readonly string[] KnownCurrencies =
        { "ليرة سورية", "دولار أمريكي", "يورو" };

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "ليرة سورية" : currency.Trim();

    /// <summary>ملف «مصرفي» ما لم يُحدد «عادي» صراحة (القيمة الافتراضية للعقد مصرفي).</summary>
    private static bool IsBanking(string? contractTypeSelector) =>
        !string.Equals(contractTypeSelector?.Trim(), "عادي", StringComparison.Ordinal);

    /// <summary>
    /// يُضيف مبلغًا لسلة عملته متجاهلًا الصفر والغائب؛
    /// والعملات خارج المعروفة تُهمل من العرض (لا يُفترض حدوثها لكون النموذج مقيدًا بها).
    /// </summary>
    private static void AddAmount(Dictionary<string, decimal> buckets, string? currency, decimal? amount)
    {
        if (amount == null || amount.Value == 0)
            return;
        var key = NormalizeCurrency(currency);
        if (!KnownCurrencies.Contains(key))
            return;
        buckets[key] = buckets.TryGetValue(key, out var current) ? current + amount.Value : amount.Value;
    }

    /// <summary>السلات بالترتيب الثابت المعروف، وتستبعد العملات غير المجمّعة (صفرية).</summary>
    private static List<CurrencyAmountDto> ToCurrencyAmounts(Dictionary<string, decimal> buckets) =>
        KnownCurrencies
            .Where(buckets.ContainsKey)
            .Select(c => new CurrencyAmountDto(c, buckets[c]))
            .ToList();

    private static ManagerStatsDto AggregateManagerStats(
        List<ManagerStatRow> rows, StatsPeriod period, (DateTime Start, DateTime End) window)
    {
        var active = 0;
        var drafts = 0;
        var deferred = 0;
        var settledCount = 0;
        var forcibleCount = 0;
        var tradingAgainstCount = 0;
        var executedAgainstCount = 0;
        decimal executedAgainstAmount = 0;
        var depositTradingCount = 0;
        var depositExecutedCount = 0;
        decimal depositExecutedAmount = 0;

        var activeBanking = 0;
        var activeOrdinary = 0;
        var draftsBanking = 0;
        var draftsOrdinary = 0;
        var deferredBanking = 0;
        var deferredOrdinary = 0;

        var activeBankingBuckets = new Dictionary<string, decimal>();
        var activeOrdinaryBuckets = new Dictionary<string, decimal>();
        var draftsBankingBuckets = new Dictionary<string, decimal>();
        var draftsOrdinaryBuckets = new Dictionary<string, decimal>();
        var deferredBankingBuckets = new Dictionary<string, decimal>();
        var deferredOrdinaryBuckets = new Dictionary<string, decimal>();
        var totalBuckets = new Dictionary<string, decimal>();
        var tradingAgainstBuckets = new Dictionary<string, decimal>();
        var settledCollectedBuckets = new Dictionary<string, decimal>();
        var forcibleCollectedBuckets = new Dictionary<string, decimal>();

        foreach (var r in rows)
        {
            // ملف «الجهة العامة منفذ عليها»: يُحتسب في «متداول للضد» (المتداول فقط)
            // أو «منفذ للضد» (المنفذ فقط)، والمشطوب مستبعد من الاثنتين (فلتُر في SQL).
            if (r.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            {
                if (r.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                    continue;

                if (r.ExecutedStatus == ExecutedStatusCatalog.Executed)
                {
                    executedAgainstCount++;
                    executedAgainstAmount += r.ExecutedPaidAmount ?? 0;
                }
                else
                {
                    tradingAgainstCount++;
                    AddAmount(tradingAgainstBuckets, r.ExecutedRequiredCurrency, r.ExecutedRequiredAmount);
                    AddAmount(tradingAgainstBuckets, r.ExecutedRequiredCurrency2, r.ExecutedRequiredAmount2);
                    AddAmount(tradingAgainstBuckets, r.ExecutedRequiredCurrency3, r.ExecutedRequiredAmount3);
                }
                continue;
            }

            // ملف «عرض وايداع»: يُحتسب «للصالح» كسطر فرعي داخل بطاقتي متداول/منفذ.
            // المتداول يظهر بعدده فقط، والمنفذ بعدده ومجموع المبالغ المودعة، والمشطوب مستبعد.
            if (r.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
            {
                if (r.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                    continue;

                if (r.ExecutedStatus == ExecutedStatusCatalog.Executed)
                {
                    depositExecutedCount++;
                    depositExecutedAmount += r.ExecutedPaidAmount ?? 0;
                }
                else
                {
                    depositTradingCount++;
                }
                continue;
            }

            if (r.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement)
            {
                settledCount++;
                AddAmount(settledCollectedBuckets, r.CollectedCurrency, r.CollectedAmount);
                AddAmount(settledCollectedBuckets, r.CollectedCurrency2, r.CollectedAmount2);
                AddAmount(settledCollectedBuckets, r.CollectedCurrency3, r.CollectedAmount3);
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                && r.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)
            {
                forcibleCount++;
                AddAmount(forcibleCollectedBuckets, r.CollectedCurrency, r.CollectedAmount);
                AddAmount(forcibleCollectedBuckets, r.CollectedCurrency2, r.CollectedAmount2);
                AddAmount(forcibleCollectedBuckets, r.CollectedCurrency3, r.CollectedAmount3);
            }
            else if (r.IsDraft && string.IsNullOrEmpty(r.ExecStatus))
            {
                drafts++;
                AccumulateContract(r,
                    ref draftsBanking, ref draftsOrdinary,
                    draftsBankingBuckets, draftsOrdinaryBuckets, totalBuckets);
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.Deferred)
            {
                deferred++;
                AccumulateContract(r,
                    ref deferredBanking, ref deferredOrdinary,
                    deferredBankingBuckets, deferredOrdinaryBuckets, totalBuckets);
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                && r.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted)
            {
                active++;
                AccumulateContract(r,
                    ref activeBanking, ref activeOrdinary,
                    activeBankingBuckets, activeOrdinaryBuckets, totalBuckets);
            }
            else if (string.IsNullOrEmpty(r.ExecStatus) && !r.IsDraft)
            {
                active++;
                AccumulateContract(r,
                    ref activeBanking, ref activeOrdinary,
                    activeBankingBuckets, activeOrdinaryBuckets, totalBuckets);
            }
        }

        return new ManagerStatsDto(
            TotalFiles: active + drafts + deferred,
            Active: active,
            Drafts: drafts,
            Deferred: deferred,
            ActiveSplit: new ManagerContractSplitDto(
                activeBanking, activeOrdinary,
                ToCurrencyAmounts(activeBankingBuckets), ToCurrencyAmounts(activeOrdinaryBuckets)),
            DraftsSplit: new ManagerContractSplitDto(
                draftsBanking, draftsOrdinary,
                ToCurrencyAmounts(draftsBankingBuckets), ToCurrencyAmounts(draftsOrdinaryBuckets)),
            DeferredSplit: new ManagerContractSplitDto(
                deferredBanking, deferredOrdinary,
                ToCurrencyAmounts(deferredBankingBuckets), ToCurrencyAmounts(deferredOrdinaryBuckets)),
            TotalAmounts: ToCurrencyAmounts(totalBuckets),
            TradingAgainstAmounts: ToCurrencyAmounts(tradingAgainstBuckets),
            SettledCount: settledCount,
            SettledCollected: settledCollectedBuckets.TryGetValue("ليرة سورية", out var settledPrimary) ? settledPrimary : 0,
            SettledCollectedAmounts: ToCurrencyAmounts(settledCollectedBuckets),
            ForcibleCount: forcibleCount,
            ForcibleCollected: forcibleCollectedBuckets.TryGetValue("ليرة سورية", out var forciblePrimary) ? forciblePrimary : 0,
            ForcibleCollectedAmounts: ToCurrencyAmounts(forcibleCollectedBuckets),
            TradingAgainstCount: tradingAgainstCount,
            ExecutedAgainstCount: executedAgainstCount,
            ExecutedAgainstAmount: executedAgainstAmount,
            DepositTradingCount: depositTradingCount,
            DepositExecutedCount: depositExecutedCount,
            DepositExecutedAmount: depositExecutedAmount,
            PeriodYear: window.Start.Year,
            PeriodQuarter: period == StatsPeriod.Quarterly ? (window.Start.Month - 1) / 3 + 1 : null,
            PeriodMonth: period == StatsPeriod.Monthly ? window.Start.Month : null);
    }

    /// <summary>
    /// يوزّع المبالغ الثلاثة على سلة نوع العقد (مصرفي/عادي) بعملاتهما،
    /// مع تحديث عداد النوع وسلة الإجمالي (دون المنفذ).
    /// الملف المصرفي يحفظ مبالغه في Amount/Amount2/Amount3، والعادي في Inclusion*.
    /// </summary>
    private static void AccumulateContract(
        ManagerStatRow r,
        ref int bankingCount, ref int ordinaryCount,
        Dictionary<string, decimal> bankingBuckets, Dictionary<string, decimal> ordinaryBuckets,
        Dictionary<string, decimal> totalBuckets)
    {
        var banking = IsBanking(r.ContractTypeSelector);
        if (banking) bankingCount++;
        else ordinaryCount++;

        if (banking)
        {
            AddAmount(bankingBuckets, r.Currency, r.AmountNumeric);
            AddAmount(bankingBuckets, r.Currency2, r.Amount2Numeric);
            AddAmount(bankingBuckets, r.Currency3, r.Amount3Numeric);
            AddAmount(totalBuckets, r.Currency, r.AmountNumeric);
            AddAmount(totalBuckets, r.Currency2, r.Amount2Numeric);
            AddAmount(totalBuckets, r.Currency3, r.Amount3Numeric);
        }
        else
        {
            AddAmount(ordinaryBuckets, r.InclusionCurrency, r.InclusionAmountNumeric);
            AddAmount(ordinaryBuckets, r.InclusionCurrency2, r.InclusionAmount2Numeric);
            AddAmount(ordinaryBuckets, r.InclusionCurrency3, r.InclusionAmount3Numeric);
            AddAmount(totalBuckets, r.InclusionCurrency, r.InclusionAmountNumeric);
            AddAmount(totalBuckets, r.InclusionCurrency2, r.InclusionAmount2Numeric);
            AddAmount(totalBuckets, r.InclusionCurrency3, r.InclusionAmount3Numeric);
        }
    }

    public async Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new
            {
                d.CreatedById,
                RegDateParsed = d.RegistrationDate != null ? d.RegistrationDate.DateParsed : null,
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
            var periodDate = r.RegDateParsed ?? r.CreatedAt.Date;
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
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new
            {
                RegDateParsed = d.RegistrationDate != null ? d.RegistrationDate.DateParsed : null,
                d.CreatedAt,
            })
            .ToListAsync(ct);

        return GroupMonths(dates.Select(x => x.RegDateParsed ?? x.CreatedAt.Date));
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

    private static DateTime? TryParseActionDate(string? value) => ActionDateParser.TryParse(value);

    private static int DurationDays(string? duration) => duration switch
    {
        "3 أيام" => 3,
        "أسبوع" => 7,
        "أسبوعين" => 14,
        "شهر" => 30,
        _ => 0,
    };
}
