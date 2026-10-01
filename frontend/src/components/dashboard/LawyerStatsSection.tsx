import { useEffect, useState } from 'react';
import type {
  AppealsStatsDto,
  ManagerStatsDto,
  MonthlyStatDto,
  StatsPeriod,
} from '../../types';
import {
  periodOptions,
  selectionValue,
  trendDirectionText,
  zeroFilledMonthlyCounts,
} from './dashboardFormat';
import type { PeriodSelection } from './dashboardTypes';
import { Sparkline } from './Sparkline';
import { StatsCards } from './StatsCards';
import { PeriodScopeGroup, PeriodSelect } from './StatsPeriodControls';

/**
 * قسم الإحصائيات المعاد هيكلته (للمحامي — `ManagerStatsSection` يبقى للمدير):
 * بطاقة بطل «متداولة ضمن الفترة» (ليست لقطة العمل الجاري — الفترة معلنة دائمًا) +
 * 4 مؤشرات بدلتا عن الفترة السابقة + `sparkline` واحد للمسجَّلة شهريًا +
 * تفاصيل مصرفي/عادي والمبالغ خلف توسيع + روابط تعمّق لقوائم الملفات.
 *
 * يُعاد استخدامه لرئيس القسم في `/stats` عبر `showDrillLinks={false}`
 * (بلا روابط تعمّق) — الاستئنافات تُخفى تلقائيًا عبر `appealsStats={null}`.
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
  showDrillLinks = true,
  sectionLabel = 'إحصائيات المحامي',
  sectionId = 'lawyer-stats',
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
  showDrillLinks?: boolean;
  sectionLabel?: string;
  sectionId?: string;
}) {
  const [noteOpen, setNoteOpen] = useState(false);
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);

  useEffect(() => {
    if (stats) setUpdatedAt(new Date());
  }, [stats]);

  if (error) return <div className="text-red-600" role="alert">{error}</div>;
  if (!stats) return <div className="text-gray-500">جارِ التحميل...</div>;

  const options = periodOptions(availablePeriods, period);
  const selectedValue = selectionValue(selection);

  const trendPoints = zeroFilledMonthlyCounts(availablePeriods);
  const trendDirection = trendDirectionText(trendPoints);

  const fallbackCount = stats.periodDateFallbackCount ?? 0;
  const receiptCount = stats.periodDateFromReceiptCount ?? 0;

  return (
    <section aria-label={sectionLabel} id={sectionId} className="scroll-mt-4">
      <div className="sticky top-0 z-10 bg-gray-100/95 backdrop-blur py-2 mb-4">
        <div className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
          <PeriodScopeGroup period={period} onPeriodChange={onPeriodChange} />

          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            {updatedAt ? (
              <span className="text-xs text-gray-400 tabular-nums">
                حُدّث الآن{' '}
                {updatedAt.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}
              </span>
            ) : null}
            <PeriodSelect
              options={options}
              selectedValue={selectedValue}
              onSelectionChange={onSelectionChange}
            />
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

      <StatsCards
        stats={stats}
        prevStats={prevStats}
        showDrillLinks={showDrillLinks}
        appealsStats={appealsStats}
        sparkline={
          <Sparkline points={trendPoints} description={`الملفات المسجَّلة شهريًا: ${trendDirection}`} />
        }
      />
    </section>
  );
}
