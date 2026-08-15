import type {
  BranchDto,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
  StatsPeriod,
} from '../../types';
import { ContractSplit, CurrencyAmountList } from './CurrencyAmountList';
import { MONTHS, PERIODS, currencyLabel, periodLabel, periodOptions, selectionValue } from './dashboardFormat';
import { ICONS } from './dashboardIcons';
import type { PeriodSelection } from './dashboardTypes';
import { StatCard } from './StatCard';

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
  lawyers,
  error,
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
  lawyers: ManagerLawyerStatDto[];
  error: string;
}) {
  if (error) return <div className="text-red-600">{error}</div>;
  if (!stats) return <div className="text-gray-500">جارِ التحميل...</div>;

  const options = periodOptions(availablePeriods, period);
  const selectedValue = selectionValue(selection);

  return (
    <>
      <div className="flex flex-col lg:flex-row gap-3 lg:items-center justify-between mb-6">
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

        <div className="flex flex-col sm:flex-row gap-3 sm:items-center">
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

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-5 gap-3 sm:gap-4 mb-6">
        <StatCard label="إجمالي الملفات" value={stats.totalFiles} accent="#059669" icon={ICONS.documents}>
          <CurrencyAmountList amounts={stats.totalAmounts} />
        </StatCard>
        <StatCard
          label="متداول"
          value={(stats.active ?? 0) + (stats.tradingAgainstCount ?? 0) + (stats.depositTradingCount ?? 0)}
          accent="#2563eb"
          icon={ICONS.active}
        >
          <div className="mt-2 space-y-2 text-xs">
            <div>
              <span className="font-bold text-gray-800">متداول للصالح</span>
              <span className="text-gray-500 tabular-nums" dir="ltr"> ({stats.active})</span>
              <ContractSplit split={stats.activeSplit} />
            </div>
            <div>
              <span className="font-bold text-gray-800">عرض وايداع</span>
              <span className="text-gray-500 tabular-nums" dir="ltr"> ({stats.depositTradingCount ?? 0})</span>
            </div>
            <div>
              <span className="font-bold text-gray-800">متداول للضد</span>
              <span className="text-gray-500 tabular-nums" dir="ltr"> ({stats.tradingAgainstCount ?? 0})</span>
              <CurrencyAmountList amounts={stats.tradingAgainstAmounts} />
            </div>
          </div>
        </StatCard>
        <StatCard label="تحت رفع" value={stats.drafts} accent="#d97706" icon={ICONS.drafts}>
          <ContractSplit split={stats.draftsSplit} />
        </StatCard>
        <StatCard label="تريث" value={stats.deferred} accent="#dc2626" icon={ICONS.deferred}>
          <ContractSplit split={stats.deferredSplit} />
        </StatCard>
        <StatCard
          label="منفذ"
          value={stats.settledCount + stats.forcibleCount + Number(stats.executedAgainstCount ?? 0) + Number(stats.depositExecutedCount ?? 0)}
          accent="#7c3aed"
          icon={ICONS.executed}
        >
          <div className="mt-2 space-y-2 text-xs">
            <div>
              <span className="font-bold text-gray-800">منفذ للصالح</span>
              <span className="text-gray-500 tabular-nums" dir="ltr"> ({stats.settledCount + stats.forcibleCount})</span>
              <div className="mt-1.5 space-y-1.5">
                <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-600 shrink-0" aria-hidden="true" />
                  <span className="text-gray-700">منفذ بالتسوية</span>
                  <span className="tabular-nums text-gray-500" dir="ltr">({stats.settledCount})</span>
                  <span className="text-emerald-700 tabular-nums whitespace-nowrap" dir="ltr">
                    {stats.settledCollectedAmounts?.length
                      ? stats.settledCollectedAmounts.map((a) => `${Number(a.amount).toLocaleString('en-US')} ${currencyLabel(a.currency)}`).join(' + ')
                      : `${stats.settledCollected.toLocaleString('en-US')} ${currencyLabel('ليرة سورية')}`}
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-red-600 shrink-0" aria-hidden="true" />
                  <span className="text-gray-700">منفذ جبريا</span>
                  <span className="tabular-nums text-gray-500" dir="ltr">({stats.forcibleCount})</span>
                  <span className="text-red-700 tabular-nums whitespace-nowrap" dir="ltr">
                    {stats.forcibleCollectedAmounts?.length
                      ? stats.forcibleCollectedAmounts.map((a) => `${Number(a.amount).toLocaleString('en-US')} ${currencyLabel(a.currency)}`).join(' + ')
                      : `${stats.forcibleCollected.toLocaleString('en-US')} ${currencyLabel('ليرة سورية')}`}
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-sky-600 shrink-0" aria-hidden="true" />
                  <span className="text-gray-700">عرض وايداع</span>
                  <span className="tabular-nums text-gray-500" dir="ltr">({stats.depositExecutedCount ?? 0})</span>
                  <span className="text-sky-700 tabular-nums whitespace-nowrap" dir="ltr">
                    {Number(stats.depositExecutedAmount ?? 0).toLocaleString('en-US')} {currencyLabel('ليرة سورية')}
                  </span>
                </div>
              </div>
            </div>
            <div>
              <span className="font-bold text-gray-800">منفذ للضد</span>
              <span className="text-gray-500 tabular-nums" dir="ltr"> ({Number(stats.executedAgainstCount ?? 0)})</span>
              <div className="text-indigo-700 tabular-nums whitespace-nowrap" dir="ltr">
                {Number(stats.executedAgainstAmount ?? 0).toLocaleString('en-US')} {currencyLabel('ليرة سورية')}
              </div>
            </div>
          </div>
        </StatCard>
      </div>

      {showLawyerTable && branchId ? (
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
          <h3 className="font-bold text-gray-800 mb-4">إحصائيات محامي الفرع</h3>
          {lawyers.length === 0 ? (
            <p className="text-gray-400 text-sm">لا يوجد محامون في هذا الفرع</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="text-gray-500 border-b border-gray-100">
                    <th className="text-right font-medium py-2.5 px-3">المحامي</th>
                    <th className="text-right font-medium py-2.5 px-3">المجموع</th>
                    {lawyers[0]?.points.map((p) => (
                      <th key={`${p.year}-${p.month}`} className="text-right font-medium py-2.5 px-3">
                        {MONTHS[p.month - 1]}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {lawyers.map((l) => (
                    <tr key={l.lawyerId}>
                      <td className="py-2.5 px-3 font-medium text-gray-800 whitespace-nowrap">{l.lawyerName}</td>
                      <td className="py-2.5 px-3 text-gray-900 tabular-nums">{l.totalCount}</td>
                      {l.points.map((p) => (
                        <td key={`${p.year}-${p.month}`} className="py-2.5 px-3 text-gray-600 tabular-nums">
                          {p.count}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ) : showLawyerTable ? (
        <p className="text-sm text-gray-400 mb-6">اختر فرعًا لعرض إحصائيات محامي الفرع</p>
      ) : null}
    </>
  );
}
