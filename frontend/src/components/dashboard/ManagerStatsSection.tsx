import type {
  AppealsStatsDto,
  BranchDto,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
  StatsPeriod,
} from '../../types';
import { periodLabel, periodOptions, selectionValue, trendDirectionText, zeroFilledMonthlyCounts } from './dashboardFormat';
import type { PeriodSelection } from './dashboardTypes';
import { LawyersTable } from './LawyersTable';
import { Sparkline } from './Sparkline';
import { StatsCards } from './StatsCards';
import { PeriodScopeGroup, PeriodSelect } from './StatsPeriodControls';

export function ManagerStatsSection({
  period,
  onPeriodChange,
  availablePeriods,
  selection,
  onSelectionChange,
  branches,
  branchId,
  onBranchChange,
  showBranchSelect = true,
  showLawyerTable = true,
  stats,
  prevStats = null,
  lawyers,
  lawyersError = '',
  error,
  appealsStats = null,
}: {
  period: StatsPeriod;
  onPeriodChange: (p: StatsPeriod) => void;
  availablePeriods: MonthlyStatDto[];
  selection: PeriodSelection | null;
  onSelectionChange: (s: PeriodSelection) => void;
  branches: BranchDto[];
  branchId: number | null;
  onBranchChange: (id: number | null) => void;
  showBranchSelect?: boolean;
  showLawyerTable?: boolean;
  stats: ManagerStatsDto | null;
  /** إحصائيات الفترة السابقة للدلتا — تُمرر من اللوحة (تُخفى الدلتا عند `null`). */
  prevStats?: ManagerStatsDto | null;
  lawyers: ManagerLawyerStatDto[];
  /** خطأ جلب جدول المحامين — يُعرض بدل «لا يوجد محامون». */
  lawyersError?: string;
  error: string;
  /** بطاقة «الاستئنافات» للمحامي فقط — تُمرر null لإخفائها. */
  appealsStats?: AppealsStatsDto | null;
}) {
  if (error) return <div className="text-red-600" role="alert">{error}</div>;
  if (!stats) return <div className="text-gray-500">جارِ التحميل...</div>;

  const options = periodOptions(availablePeriods, period);
  const selectedValue = selectionValue(selection);

  const trendPoints = zeroFilledMonthlyCounts(availablePeriods);
  const trendDirection = trendDirectionText(trendPoints);

  return (
    <>
      <div className="flex flex-col lg:flex-row gap-3 lg:items-center justify-between mb-6">
        <PeriodScopeGroup period={period} onPeriodChange={onPeriodChange} />

        <div className="flex flex-col sm:flex-row gap-3 sm:items-center">
          <PeriodSelect
            options={options}
            selectedValue={selectedValue}
            onSelectionChange={onSelectionChange}
          />

          {showBranchSelect ? (
            <label className="flex items-center gap-2 text-sm text-gray-600">
              الفرع
              <select
                value={branchId ?? ''}
                onChange={(e) => onBranchChange(e.target.value ? Number(e.target.value) : null)}
                className="min-h-11 rounded-xl border border-gray-200 bg-white px-3 text-sm"
              >
                <option value="">كل الفروع</option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
        </div>
      </div>

      <p className="text-sm text-gray-500 mb-6">
        عرض الفترة: <span className="font-medium text-gray-800">{periodLabel(stats)}</span>
      </p>

      {(stats.periodDateFallbackCount ?? 0) > 0 ? (
        <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 mb-6">
          تشمل هذه الفترة{' '}
          <span className="font-bold tabular-nums" dir="ltr">
            ({stats.periodDateFallbackCount})
          </span>{' '}
          ملفًا حُسبت بتاريخ إدخالها لغياب تاريخ قيدها أو تعذّر تحليله
        </p>
      ) : null}

      {(stats.periodDateFromReceiptCount ?? 0) > 0 ? (
        <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 mb-6">
          وتشمل أيضًا{' '}
          <span className="font-bold tabular-nums" dir="ltr">
            ({stats.periodDateFromReceiptCount})
          </span>{' '}
          ملفًا لجهات «منفذ عليها» أو «عرض وايداع» حُسبت بتاريخ إدخالها لغياب تاريخ ورود الإخطار
        </p>
      ) : null}

      <StatsCards
        stats={stats}
        prevStats={prevStats}
        showDrillLinks={false}
        appealsStats={appealsStats}
        sparkline={
          <Sparkline points={trendPoints} description={`الملفات المسجَّلة شهريًا: ${trendDirection}`} />
        }
      />

      <div className="mt-3 sm:mt-4">
        <LawyersTable showTable={showLawyerTable} branchId={branchId} lawyers={lawyers} error={lawyersError} />
      </div>
    </>
  );
}
