import { useEffect, useState } from 'react';
import type {
  AppealsStatsDto,
  ManagerStatsDto,
  MonthlyStatDto,
  StatsPeriod,
} from '../../types';
import { EXEC_STATUS_DEFERRED, STATE_DRAFT } from '../../utils/documentStatus';
import { ContractSplit, CurrencyAmountList } from './CurrencyAmountList';
import {
  PERIODS,
  currencyLabel,
  deltaPercent,
  formatNumber,
  periodLabel,
  periodOptions,
  selectionValue,
  zeroFilledMonthlyCounts,
} from './dashboardFormat';
import type { PeriodSelection } from './dashboardTypes';
import { LawyerStatCard } from './LawyerStatCard';
import { Sparkline } from './Sparkline';

/**
 * قسم إحصائيات المحامي المعاد هيكلته (للمحامي فقط — `ManagerStatsSection` يبقى للمدير/الرئيس):
 * بطاقة بطل «متداولة ضمن الفترة» (ليست لقطة العمل الجاري — الفترة معلنة دائمًا) +
 * 4 مؤشرات بدلتا عن الفترة السابقة + `sparkline` واحد للمسجَّلة شهريًا +
 * تفاصيل مصرفي/عادي والمبالغ خلف توسيع + روابط تعمّق لقوائم الملفات.
 */
export function LawyerStatsSection({
  period,
  onPeriodChange,
  availablePeriods,
  selection,
  onSelectionChange,
  stats,
  prevStats,
  appealsStats,
  error,
}: {
  period: StatsPeriod;
  onPeriodChange: (p: StatsPeriod) => void;
  availablePeriods: MonthlyStatDto[];
  selection: PeriodSelection | null;
  onSelectionChange: (s: PeriodSelection) => void;
  stats: ManagerStatsDto | null;
  prevStats: ManagerStatsDto | null;
  appealsStats: AppealsStatsDto | null;
  error: string;
}) {
  const [noteOpen, setNoteOpen] = useState(false);
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);

  useEffect(() => {
    if (stats) setUpdatedAt(new Date());
  }, [stats]);

  if (error) return <div className="text-red-600">{error}</div>;
  if (!stats) return <div className="text-gray-500">جارِ التحميل...</div>;

  const options = periodOptions(availablePeriods, period);
  const selectedValue = selectionValue(selection);
  const periodName = periodLabel(stats);

  const heroValue = (stats.active ?? 0) + (stats.tradingAgainstCount ?? 0) + (stats.depositTradingCount ?? 0);
  const prevHero = prevStats
    ? (prevStats.active ?? 0) + (prevStats.tradingAgainstCount ?? 0) + (prevStats.depositTradingCount ?? 0)
    : null;
  const executedValue =
    stats.settledCount + stats.forcibleCount + Number(stats.executedAgainstCount ?? 0) + Number(stats.depositExecutedCount ?? 0);
  const prevExecuted = prevStats
    ? prevStats.settledCount +
      prevStats.forcibleCount +
      Number(prevStats.executedAgainstCount ?? 0) +
      Number(prevStats.depositExecutedCount ?? 0)
    : null;
  const appealsValue = appealsStats
    ? appealsStats.pendingCount + appealsStats.decidedInFavor + appealsStats.decidedAgainst
    : 0;
  const prevAppealsValue =
    prevStats?.appeals != null
      ? prevStats.appeals.pendingCount + prevStats.appeals.decidedInFavor + prevStats.appeals.decidedAgainst
      : null;

  const trendPoints = zeroFilledMonthlyCounts(availablePeriods);
  const trendDirection =
    trendPoints.length > 1
      ? trendPoints[trendPoints.length - 1].count > trendPoints[0].count
        ? 'ارتفاع'
        : trendPoints[trendPoints.length - 1].count < trendPoints[0].count
          ? 'انخفاض'
          : 'ثبات'
      : 'نقطة واحدة';

  const fallbackCount = stats.periodDateFallbackCount ?? 0;
  const receiptCount = stats.periodDateFromReceiptCount ?? 0;

  return (
    <section aria-label="إحصائيات المحامي" id="lawyer-stats" className="scroll-mt-4">
      <div className="sticky top-0 z-10 bg-gray-100/95 backdrop-blur py-2 mb-4">
        <div className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
          <div
            role="group"
            aria-label="نطاق الفترة"
            className="inline-flex self-start rounded-xl border border-gray-200 bg-white p-1"
          >
            {PERIODS.map(({ key, label }) => (
              <button
                key={key}
                type="button"
                onClick={() => onPeriodChange(key)}
                className={`min-h-11 px-4 rounded-lg text-sm font-medium transition-colors ${
                  period === key ? 'bg-emerald-600 text-white' : 'text-gray-600 hover:bg-gray-50'
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            {updatedAt ? (
              <span className="text-xs text-gray-400 tabular-nums">
                حُدّث الآن{' '}
                {updatedAt.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}
              </span>
            ) : null}
            <label className="flex items-center gap-2 text-sm text-gray-600">
              الفترة
              <select
                value={selectedValue}
                onChange={(e) => {
                  const opt = options.find((o) => o.value === e.target.value);
                  if (opt) {
                    onSelectionChange({ year: opt.year, month: opt.month, quarter: opt.quarter });
                  }
                }}
                className="min-h-11 rounded-xl border border-gray-200 bg-white px-3 text-sm"
              >
                {options.length === 0 ? (
                  <option value="">لا توجد فترات مسجلة</option>
                ) : (
                  options.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))
                )}
              </select>
            </label>
          </div>
        </div>
      </div>

      {fallbackCount > 0 || receiptCount > 0 ? (
        <div className="mb-4">
          <button
            type="button"
            onClick={() => setNoteOpen((v) => !v)}
            aria-expanded={noteOpen}
            className="min-h-11 text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 hover:bg-amber-100 focus-visible:ring-2 focus-visible:ring-amber-500 font-medium"
          >
            {noteOpen ? 'إخفاء ملاحظة مصدر التواريخ' : 'ملاحظة حول مصدر تواريخ الفترة…'}
          </button>
          {noteOpen ? (
            <div className="mt-2 space-y-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
              {fallbackCount > 0 ? (
                <p>
                  تشمل هذه الفترة{' '}
                  <span className="font-bold tabular-nums" dir="ltr">
                    ({fallbackCount})
                  </span>{' '}
                  ملفًا حُسبت بتاريخ إدخالها لغياب تاريخ قيدها أو تعذّر تحليله
                </p>
              ) : null}
              {receiptCount > 0 ? (
                <p>
                  وتشمل أيضًا{' '}
                  <span className="font-bold tabular-nums" dir="ltr">
                    ({receiptCount})
                  </span>{' '}
                  ملفًا لجهات «منفذ عليها» أو «عرض وايداع» حُسبت بتاريخ إدخالها لغياب تاريخ ورود الإخطار
                </p>
              ) : null}
            </div>
          ) : null}
        </div>
      ) : null}

      <LawyerStatCard
        hero
        title={`متداولة ضمن ${periodName}`}
        value={heroValue}
        delta={deltaPercent(heroValue, prevHero)}
        // الرقم يجمع جانبين (صالح + ضد/إيداع) فلا يمثّله فلتر واحد — التعمّق للقائمة الكاملة.
        drillTo="/documents"
        aside={
          <Sparkline points={trendPoints} description={`الملفات المسجَّلة شهريًا: ${trendDirection}`} />
        }
      >
        <div className="space-y-2">
          <div>
            <span className="font-bold text-gray-800">متداول للصالح</span>
            <span className="text-gray-500 tabular-nums" dir="ltr">
              {' '}
              ({stats.active})
            </span>
            <ContractSplit split={stats.activeSplit} />
          </div>
          <div>
            <span className="font-bold text-gray-800">عرض وايداع</span>
            <span className="text-gray-500 tabular-nums" dir="ltr">
              {' '}
              ({stats.depositTradingCount ?? 0})
            </span>
          </div>
          <div>
            <span className="font-bold text-gray-800">متداول للضد</span>
            <span className="text-gray-500 tabular-nums" dir="ltr">
              {' '}
              ({stats.tradingAgainstCount ?? 0})
            </span>
            <CurrencyAmountList amounts={stats.tradingAgainstAmounts} />
          </div>
        </div>
      </LawyerStatCard>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 sm:gap-4 mt-3 sm:mt-4">
        <LawyerStatCard
          title="إجمالي الملفات"
          value={stats.totalFiles}
          delta={deltaPercent(stats.totalFiles, prevStats?.totalFiles)}
          drillTo="/documents"
        >
          <CurrencyAmountList amounts={stats.totalAmounts} />
        </LawyerStatCard>

        <LawyerStatCard
          title="تحت رفع"
          value={stats.drafts}
          delta={deltaPercent(stats.drafts, prevStats?.drafts)}
          drillTo={`/documents?status=${encodeURIComponent(STATE_DRAFT)}`}
        >
          <ContractSplit split={stats.draftsSplit} />
        </LawyerStatCard>

        <LawyerStatCard
          title="منفذ"
          value={executedValue}
          delta={deltaPercent(executedValue, prevExecuted)}
          drillTo="/documents/executed"
        >
          <div className="space-y-2">
            <div>
              <span className="font-bold text-gray-800">منفذ للصالح</span>
              <span className="text-gray-500 tabular-nums" dir="ltr">
                {' '}
                ({stats.settledCount + stats.forcibleCount})
              </span>
              <div className="mt-1.5 space-y-1.5">
                <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-600 shrink-0" aria-hidden="true" />
                  <span className="text-gray-700">منفذ بالتسوية</span>
                  <span className="tabular-nums text-gray-500" dir="ltr">
                    ({stats.settledCount})
                  </span>
                  <span className="text-emerald-700 tabular-nums whitespace-nowrap" dir="ltr">
                    {stats.settledCollectedAmounts?.length
                      ? stats.settledCollectedAmounts
                          .map((a) => `${formatNumber(Number(a.amount))} ${currencyLabel(a.currency)}`)
                          .join(' + ')
                      : `${formatNumber(stats.settledCollected)} ${currencyLabel('ليرة سورية')}`}
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-red-600 shrink-0" aria-hidden="true" />
                  <span className="text-gray-700">منفذ جبريا</span>
                  <span className="tabular-nums text-gray-500" dir="ltr">
                    ({stats.forcibleCount})
                  </span>
                  <span className="text-red-700 tabular-nums whitespace-nowrap" dir="ltr">
                    {stats.forcibleCollectedAmounts?.length
                      ? stats.forcibleCollectedAmounts
                          .map((a) => `${formatNumber(Number(a.amount))} ${currencyLabel(a.currency)}`)
                          .join(' + ')
                      : `${formatNumber(stats.forcibleCollected)} ${currencyLabel('ليرة سورية')}`}
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-sky-600 shrink-0" aria-hidden="true" />
                  <span className="text-gray-700">عرض وايداع</span>
                  <span className="tabular-nums text-gray-500" dir="ltr">
                    ({stats.depositExecutedCount ?? 0})
                  </span>
                  <span className="text-sky-700 tabular-nums whitespace-nowrap" dir="ltr">
                    {formatNumber(Number(stats.depositExecutedAmount ?? 0))} {currencyLabel('ليرة سورية')}
                  </span>
                </div>
              </div>
            </div>
            <div>
              <span className="font-bold text-gray-800">منفذ للضد</span>
              <span className="text-gray-500 tabular-nums" dir="ltr">
                {' '}
                ({Number(stats.executedAgainstCount ?? 0)})
              </span>
              <div className="text-indigo-700 tabular-nums whitespace-nowrap" dir="ltr">
                {formatNumber(Number(stats.executedAgainstAmount ?? 0))} {currencyLabel('ليرة سورية')}
              </div>
            </div>
          </div>
        </LawyerStatCard>

        {appealsStats ? (
          <LawyerStatCard
            title="الاستئنافات"
            value={appealsValue}
            delta={deltaPercent(appealsValue, prevAppealsValue)}
            drillTo="/appeals"
          >
            <div className="space-y-1.5">
              <div className="flex items-center gap-x-2">
                <span className="w-1.5 h-1.5 rounded-full bg-red-600 shrink-0" aria-hidden="true" />
                <span className="text-gray-700">منظور</span>
                <span className="text-gray-500 tabular-nums" dir="ltr">
                  ({appealsStats.pendingCount})
                </span>
              </div>
              <div className="flex items-center gap-x-2">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-600 shrink-0" aria-hidden="true" />
                <span className="text-gray-700">محسوم للصالح</span>
                <span className="text-emerald-700 tabular-nums" dir="ltr">
                  ({appealsStats.decidedInFavor})
                </span>
              </div>
              <div className="flex items-center gap-x-2">
                <span className="w-1.5 h-1.5 rounded-full bg-red-600 shrink-0" aria-hidden="true" />
                <span className="text-red-700 tabular-nums" dir="ltr">
                  ({appealsStats.decidedAgainst})
                </span>
                <span className="text-gray-700">محسوم للضد</span>
              </div>
            </div>
          </LawyerStatCard>
        ) : null}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 sm:gap-4 mt-3 sm:mt-4">
        <LawyerStatCard
          title="تريث"
          value={stats.deferred}
          delta={deltaPercent(stats.deferred, prevStats?.deferred)}
          drillTo={`/documents?status=${encodeURIComponent(EXEC_STATUS_DEFERRED)}`}
        >
          <ContractSplit split={stats.deferredSplit} />
        </LawyerStatCard>
        <LawyerStatCard
          title="محال الى البداية"
          value={stats.referredToStartCount ?? 0}
          delta={deltaPercent(stats.referredToStartCount ?? 0, prevStats?.referredToStartCount)}
          drillTo="/documents/referred-to-start"
        >
          {stats.referredSplit ? (
            <ContractSplit split={stats.referredSplit} />
          ) : (
            <p className="text-gray-400 mt-1">لا توجد مبالغ مسجلة</p>
          )}
        </LawyerStatCard>
      </div>
    </section>
  );
}
