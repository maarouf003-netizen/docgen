import type { ReactNode } from 'react';
import type { AppealsStatsDto, ManagerStatsDto } from '../../types';
import { EXEC_STATUS_DEFERRED, STATE_DRAFT } from '../../utils/documentStatus';
import { ContractSplit, CurrencyAmountList } from './CurrencyAmountList';
import { currencyLabel, deltaPercent, formatNumber, periodLabel } from './dashboardFormat';
import { LawyerStatCard } from './LawyerStatCard';

export interface StatsCardsProps {
  stats: ManagerStatsDto;
  prevStats: ManagerStatsDto | null;
  /** روابط «عرض الملفات» — للمحامي فقط؛ الرئيس والمدير أرقام عرض بلا تعمّق. */
  showDrillLinks: boolean;
  /** بطاقة «الاستئنافات» — تُمرر `null` لإخفائها (كل مسارات `/stats/manager*`). */
  appealsStats: AppealsStatsDto | null;
  /** الرسم المصغّر في بطاقة البطل (يُبنى من فترات النطاق في الصفحة/القسم). */
  sparkline?: ReactNode;
}

/**
 * نواة بطاقات الإحصائيات المشتركة (حاضر صرف — بلا جلب):
 * بطل «متداولة ضمن الفترة» + 4 مؤشرات بدلتا + صف ثانوي (تريث/محال).
 * تُستخدم في `LawyerStatsSection` (بروابط) وفي `ManagerStatsSection` (بلا روابط).
 */
export function StatsCards({ stats, prevStats, showDrillLinks, appealsStats, sparkline }: StatsCardsProps) {
  const periodName = periodLabel(stats);
  const drill = (to: string) => (showDrillLinks ? to : undefined);

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

  return (
    <>
      <LawyerStatCard
        hero
        title={`متداولة ضمن ${periodName}`}
        value={heroValue}
        delta={deltaPercent(heroValue, prevHero)}
        // الرقم يجمع جانبين (صالح + ضد/إيداع) فلا يمثّله فلتر واحد — التعمّق للقائمة الكاملة.
        drillTo={drill('/documents')}
        aside={sparkline}
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
          drillTo={drill('/documents')}
        >
          <CurrencyAmountList amounts={stats.totalAmounts} />
        </LawyerStatCard>

        <LawyerStatCard
          title="تحت رفع"
          value={stats.drafts}
          delta={deltaPercent(stats.drafts, prevStats?.drafts)}
          drillTo={drill(`/documents?status=${encodeURIComponent(STATE_DRAFT)}`)}
        >
          <ContractSplit split={stats.draftsSplit} />
        </LawyerStatCard>

        <LawyerStatCard
          title="منفذ"
          value={executedValue}
          delta={deltaPercent(executedValue, prevExecuted)}
          drillTo={drill('/documents/executed')}
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
                    {stats.depositExecutedAmounts?.length
                      ? stats.depositExecutedAmounts
                          .map((a) => `${formatNumber(Number(a.amount))} ${currencyLabel(a.currency)}`)
                          .join(' + ')
                      : `${formatNumber(Number(stats.depositExecutedAmount ?? 0))} ${currencyLabel('ليرة سورية')}`}
                  </span>
                  {stats.depositExecutedAmounts?.some((a) => a.currency === 'أخرى') ? (
                    <span role="note" className="w-full text-[11px] font-normal text-amber-700">
                      تتضمن عملات غير معروفة — يلزم المراجعة
                    </span>
                  ) : null}
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
                {stats.executedAgainstAmounts?.length
                  ? stats.executedAgainstAmounts
                      .map((a) => `${formatNumber(Number(a.amount))} ${currencyLabel(a.currency)}`)
                      .join(' + ')
                  : `${formatNumber(Number(stats.executedAgainstAmount ?? 0))} ${currencyLabel('ليرة سورية')}`}
              </div>
              {stats.executedAgainstAmounts?.some((a) => a.currency === 'أخرى') ? (
                <span role="note" className="text-[11px] font-normal text-amber-700">
                  تتضمن عملات غير معروفة — يلزم المراجعة
                </span>
              ) : null}
            </div>
          </div>
        </LawyerStatCard>

        {appealsStats ? (
          <LawyerStatCard
            title="الاستئنافات"
            value={appealsValue}
            delta={deltaPercent(appealsValue, prevAppealsValue)}
            drillTo={drill('/appeals')}
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
          drillTo={drill(`/documents?status=${encodeURIComponent(EXEC_STATUS_DEFERRED)}`)}
        >
          <ContractSplit split={stats.deferredSplit} />
        </LawyerStatCard>
        <LawyerStatCard
          title="محال الى البداية"
          value={stats.referredToStartCount ?? 0}
          delta={deltaPercent(stats.referredToStartCount ?? 0, prevStats?.referredToStartCount)}
          drillTo={drill('/documents/referred-to-start')}
        >
          {stats.referredSplit ? (
            <ContractSplit split={stats.referredSplit} />
          ) : (
            <p className="text-gray-400 mt-1">لا توجد مبالغ مسجلة</p>
          )}
        </LawyerStatCard>
      </div>
    </>
  );
}
