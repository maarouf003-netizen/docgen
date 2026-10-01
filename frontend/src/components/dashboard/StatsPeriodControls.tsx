import { PERIODS } from './dashboardFormat';
import type { PeriodOption, PeriodSelection } from './dashboardTypes';
import type { StatsPeriod } from '../../types';

/**
 * عنصرا شريط الفترة المشتركان بين قسمي المحامي والمدير
 * (`LawyerStatsSection` و`ManagerStatsSection`): مجموعة نطاق الفترة
 * ومنتقي الفترة — بنفس البنية والأصناف والسلوك تمامًا.
 */
export function PeriodScopeGroup({
  period,
  onPeriodChange,
}: {
  period: StatsPeriod;
  onPeriodChange: (p: StatsPeriod) => void;
}) {
  return (
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
  );
}

export function PeriodSelect({
  options,
  selectedValue,
  onSelectionChange,
}: {
  options: PeriodOption[];
  selectedValue: string;
  onSelectionChange: (s: PeriodSelection) => void;
}) {
  return (
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
  );
}
