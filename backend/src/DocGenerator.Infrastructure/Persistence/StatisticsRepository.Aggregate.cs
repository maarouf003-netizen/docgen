using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public partial class StatisticsRepository
{
    private static ManagerStatsDto AggregateManagerStats(
        List<ManagerStatRow> rows, StatsPeriod period, (DateTime Start, DateTime End) window)
    {
        var active = 0;
        var drafts = 0;
        var deferred = 0;
        var referredToStart = 0;
        var settledCount = 0;
        var forcibleCount = 0;
        var tradingAgainstCount = 0;
        var executedAgainstCount = 0;
        var depositTradingCount = 0;
        var depositExecutedCount = 0;
        // وسم مصدر الفترة: ملفات النطاق المحسوبة بتاريخ إدخالها (تُجمَع هنا وتُعرض معلنة) —
        // مفصولة السبب: تعذّر تاريخ القيد (`periodFallbackCount`) وغياب إخطار الورود (`periodReceiptCount`).
        var periodFallbackCount = 0;
        var periodReceiptCount = 0;

        var activeBanking = 0;
        var activeOrdinary = 0;
        var draftsBanking = 0;
        var draftsOrdinary = 0;
        var deferredBanking = 0;
        var deferredOrdinary = 0;
        var referredBanking = 0;
        var referredOrdinary = 0;

        var activeBankingBuckets = new Dictionary<string, decimal>();
        var activeOrdinaryBuckets = new Dictionary<string, decimal>();
        var draftsBankingBuckets = new Dictionary<string, decimal>();
        var draftsOrdinaryBuckets = new Dictionary<string, decimal>();
        var deferredBankingBuckets = new Dictionary<string, decimal>();
        var deferredOrdinaryBuckets = new Dictionary<string, decimal>();
        var referredBankingBuckets = new Dictionary<string, decimal>();
        var referredOrdinaryBuckets = new Dictionary<string, decimal>();
        var totalBuckets = new Dictionary<string, decimal>();
        var tradingAgainstBuckets = new Dictionary<string, decimal>();
        var executedAgainstBuckets = new Dictionary<string, decimal>();
        var depositExecutedBuckets = new Dictionary<string, decimal>();
        var settledCollectedBuckets = new Dictionary<string, decimal>();
        var forcibleCollectedBuckets = new Dictionary<string, decimal>();

        foreach (var r in rows)
        {
            // فصل السبب: غياب الإخطار لجهتي «منفذ عليها/إيداع» لا يُنسب إلى «تعذّر تحليل تاريخ القيد».
            if (r.PeriodDateReceiptMissing)
                periodReceiptCount++;
            else if (r.PeriodDateFromCreatedAt)
                periodFallbackCount++;

            // ملف «الجهة العامة منفذ عليها»: يُحتسب في «متداول للضد» (المتداول فقط)
            // أو «منفذ للضد» (المنفذ فقط)، والمشطوب مستبعد من الاثنتين (فلتُر في SQL).
            if (r.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            {
                if (r.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                    continue;

                if (r.ExecutedStatus == ExecutedStatusCatalog.Executed)
                {
                    executedAgainstCount++;
                    AddAmount(executedAgainstBuckets, r.ExecutedPaidCurrency, r.ExecutedPaidAmount);
                    AddAmount(executedAgainstBuckets, r.ExecutedPaidCurrency2, r.ExecutedPaidAmount2);
                    AddAmount(executedAgainstBuckets, r.ExecutedPaidCurrency3, r.ExecutedPaidAmount3);
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
            // الملف الذي سبق تنفيذه يبقى محسوبًا «منفذًا» عددًا ومبلغًا حتى بعد عودته إلى
            // المتداول بكتاب السير بالملف (WasDepositExecuted) — فلا يخرج مبلغه من الإحصاء.
            if (r.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
            {
                if (r.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
                    continue;

                if (r.ExecutedStatus == ExecutedStatusCatalog.Executed || r.WasDepositExecuted)
                {
                    depositExecutedCount++;
                    AddAmount(depositExecutedBuckets, r.ExecutedPaidCurrency, r.ExecutedPaidAmount);
                    AddAmount(depositExecutedBuckets, r.ExecutedPaidCurrency2, r.ExecutedPaidAmount2);
                    AddAmount(depositExecutedBuckets, r.ExecutedPaidCurrency3, r.ExecutedPaidAmount3);
                }
                else
                {
                    depositTradingCount++;
                }
                continue;
            }

            // «منفذ بالتسوية» (الملف المنيب): يُحتسب عددًا ومبالغ محصّلة. ب6: المنفذ-الإنابة
            // «DelegationExecuted» والمسترد «Recovered» (منابان دائمًا) يُطويان عددًا فقط
            // في settledCount دون مبالغ — مبالغ البيع تُعرض في بطاقة المنيب.
            if (r.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement)
            {
                settledCount++;
                // ب6: المناب يُعدّ بلا مبالغ — محصّلات مناب مسوّى (صفوف قديمة) لا تدخل السلة.
                if (r.SourceDelegationId == null)
                {
                    AddAmount(settledCollectedBuckets, r.CollectedCurrency, r.CollectedAmount);
                    AddAmount(settledCollectedBuckets, r.CollectedCurrency2, r.CollectedAmount2);
                    AddAmount(settledCollectedBuckets, r.CollectedCurrency3, r.CollectedAmount3);
                }
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                || r.ExecStatus == ExecutionStatusCatalog.Recovered)
            {
                settledCount++;
            }
            else if (r.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                && r.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)
            {
                forcibleCount++;
                AddAmount(forcibleCollectedBuckets, r.CollectedCurrency, r.CollectedAmount);
                AddAmount(forcibleCollectedBuckets, r.CollectedCurrency2, r.CollectedAmount2);
                AddAmount(forcibleCollectedBuckets, r.CollectedCurrency3, r.CollectedAmount3);
                // مجموع بدل المبيع لإنابات الملف المنفذة (منفذ إنابة): يُضاف لسلة «منفذ جبريا»
                // (ليرة سورية) عند اعتبار المنيب منفذًا جبريًا — المبالغ المحصّلة من بيع الأموال
                // موضوع الإنابة بالمزاد العلني تتمّ بالليرة السورية دائمًا.
                AddAmount(forcibleCollectedBuckets, "ليرة سورية", r.DelegationSalesAmount);
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
            else if (r.ExecStatus == ExecutionStatusCatalog.ReferredToStart)
            {
                // ب6 (خطة RTS): ملف «محال الى البداية» يُعدّ في الفرع الجديد بعدّه وعدّادَي
                // نوع العقد ومالهما، ومبالغه في سلة الإجمالي — بما فيها من دخل من «منفذ جزئيا»
                // (يُفصَل عن «متداول / منفذ جزئيا» بقسم bran الخاص في آلة الحالات بهذه الحالة).
                referredToStart++;
                AccumulateContract(r,
                    ref referredBanking, ref referredOrdinary,
                    referredBankingBuckets, referredOrdinaryBuckets, totalBuckets);
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
            TotalFiles: active + drafts + deferred + referredToStart,
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
            ExecutedAgainstAmount: executedAgainstBuckets.TryGetValue("ليرة سورية", out var executedPrimary) ? executedPrimary : 0,
            ExecutedAgainstAmounts: ToCurrencyAmounts(executedAgainstBuckets),
            DepositTradingCount: depositTradingCount,
            DepositExecutedCount: depositExecutedCount,
            DepositExecutedAmount: depositExecutedBuckets.TryGetValue("ليرة سورية", out var depositPrimary) ? depositPrimary : 0,
            DepositExecutedAmounts: ToCurrencyAmounts(depositExecutedBuckets),
            ReferredToStartCount: referredToStart,
            ReferredSplit: new ManagerContractSplitDto(
                referredBanking, referredOrdinary,
                ToCurrencyAmounts(referredBankingBuckets), ToCurrencyAmounts(referredOrdinaryBuckets)),
            PeriodYear: window.Start.Year,
            PeriodQuarter: period == StatsPeriod.Quarterly ? (window.Start.Month - 1) / 3 + 1 : null,
            PeriodMonth: period == StatsPeriod.Monthly ? window.Start.Month : null,
            PeriodDateFallbackCount: periodFallbackCount,
            PeriodDateFromReceiptCount: periodReceiptCount);
    }

    /// <summary>
    /// يوزّع المبالغ الثلاثة على سلة نوع العقد (مصرفي/عادي) بعملاتهما،
    /// مع تحديث عداد النوع وسلة الإجمالي (دون المنفذ).
    /// الملف المصرفي يحفظ مبالغه في Amount/Amount2/Amount3، والعادي في Inclusion*.
    /// ب6: الملف المناب يُعدّ في عدادي النوع فقط بلا أي مبالغ — نسخته للمنيب لا تُحسب مرتين.
    /// </summary>
    private static void AccumulateContract(
        ManagerStatRow r,
        ref int bankingCount, ref int ordinaryCount,
        Dictionary<string, decimal> bankingBuckets, Dictionary<string, decimal> ordinaryBuckets,
        Dictionary<string, decimal> totalBuckets)
    {
        var banking = ContractTypeCatalog.IsBanking(r.ContractTypeSelector);
        if (banking) bankingCount++;
        else ordinaryCount++;

        if (r.SourceDelegationId != null)
            return;

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
}
