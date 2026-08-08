import { useEffect, useState, type FormEvent } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { richToPlainText } from '../utils/richText';
import type {
  BranchDto,
  HeadAlertDto,
  HeadAlertTargetType,
  LawyerListItem,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
  ReminderDto,
  StatsPeriod,
} from '../types';

const MONTHS = [
  'كانون الثاني', 'شباط', 'آذار', 'نيسان', 'أيار', 'حزيران',
  'تموز', 'آب', 'أيلول', 'تشرين الأول', 'تشرين الثاني', 'كانون الأول',
];

const PERIODS: { key: StatsPeriod; label: string }[] = [
  { key: 'monthly', label: 'شهري' },
  { key: 'quarterly', label: 'ربعي' },
  { key: 'yearly', label: 'سنوي' },
];

const QUARTERS = ['الأول', 'الثاني', 'الثالث', 'الرابع'];

type PeriodSelection = { year: number; month?: number; quarter?: number };

/** تسمية توضيحية للفترة المعروضة، محسوبة من حقول الفترة الصادرة من الخادم. */
function periodLabel(stats: ManagerStatsDto): string {
  if (stats.periodMonth) {
    return `${MONTHS[stats.periodMonth - 1]} ${stats.periodYear}`;
  }
  if (stats.periodQuarter) {
    return `الربع ${QUARTERS[stats.periodQuarter - 1]} ${stats.periodYear}`;
  }
  return `السنة ${stats.periodYear}`;
}

type PeriodOption = PeriodSelection & { value: string; label: string };

/** خيارات الفترة المحددة من الأشهر المتاحة (تاريخ القيد): شهر/ربع/سنة، الأحدث أولًا. */
function periodOptions(available: MonthlyStatDto[], period: StatsPeriod): PeriodOption[] {
  if (period === 'monthly') {
    return [...available]
      .sort((a, b) => b.year - a.year || b.month - a.month)
      .map((m) => ({
        value: `${m.year}-${m.month}`,
        label: `${MONTHS[m.month - 1]} ${m.year}`,
        year: m.year,
        month: m.month,
      }));
  }
  if (period === 'quarterly') {
    const map = new Map<string, { year: number; quarter: number }>();
    for (const m of available) {
      const quarter = Math.floor((m.month - 1) / 3) + 1;
      map.set(`${m.year}-${quarter}`, { year: m.year, quarter });
    }
    return [...map.values()]
      .sort((a, b) => b.year - a.year || b.quarter - a.quarter)
      .map((q) => ({
        value: `${q.year}-${q.quarter}`,
        label: `الربع ${QUARTERS[q.quarter - 1]} ${q.year}`,
        year: q.year,
        quarter: q.quarter,
      }));
  }
  const years = [...new Set(available.map((m) => m.year))].sort((a, b) => b - a);
  return years.map((y) => ({ value: String(y), label: String(y), year: y }));
}

/** أحدث فترة متاحة حسب النوع، أو null عند غياب أي بيانات مسجلة. */
function mostRecentSelection(available: MonthlyStatDto[], period: StatsPeriod): PeriodSelection | null {
  const option = periodOptions(available, period)[0];
  return option ? { year: option.year, month: option.month, quarter: option.quarter } : null;
}

/** قيمة الفترة المحددة كمفتاح مقارنة مع خيارات periodOptions. */
function selectionValue(selection: PeriodSelection | null): string {
  if (!selection) return '';
  if (selection.month != null) return `${selection.year}-${selection.month}`;
  if (selection.quarter != null) return `${selection.year}-${selection.quarter}`;
  return String(selection.year);
}

const ICONS: Record<string, ReactNode> = {
  documents: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <path d="M14 2v6h6" />
      <path d="M8 13h8" />
      <path d="M8 17h5" />
    </svg>
  ),
  active: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8" />
      <path d="M21 3v5h-5" />
    </svg>
  ),
  drafts: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z" />
      <path d="m15 5 4 4" />
    </svg>
  ),
  deferred: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="10" />
      <path d="M10 9v6" />
      <path d="M14 9v6" />
    </svg>
  ),
  executed: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="10" />
      <path d="m9 12 2 2 4-4" />
    </svg>
  ),
  amount: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect width="20" height="12" x="2" y="6" rx="2" />
      <circle cx="12" cy="12" r="2" />
      <path d="M6 12h.01" />
      <path d="M18 12h.01" />
    </svg>
  ),
  collected: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M22 7 13.5 15.5 8.5 10.5 2 17" />
      <path d="M16 7h6v6" />
    </svg>
  ),
  borrowers: (
    <svg viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  ),
};

function StatCard({
  label,
  value,
  accent,
  icon,
  amount,
  amount2,
}: {
  label: string;
  value: string | number;
  accent: string;
  icon?: ReactNode;
  amount?: string | number | null;
  amount2?: string | number | null;
}) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5 relative overflow-hidden">
      <span className="absolute inset-x-0 top-0 h-1" style={{ backgroundColor: accent }} aria-hidden="true" />
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-2xl sm:text-3xl font-bold text-gray-900 tabular-nums" dir="ltr">
            {value}
          </div>
          <div className="text-xs sm:text-sm text-gray-500 mt-1.5 leading-snug">{label}</div>
          {amount !== undefined && amount !== null && Number(amount) > 0 && (
            <div className="text-sm font-semibold text-gray-800 tabular-nums mt-1.5 truncate" dir="ltr">
              {Number(amount).toLocaleString('en-US')} ل.س
            </div>
          )}
          {amount2 !== undefined && amount2 !== null && Number(amount2) > 0 && (
            <div className="text-sm font-semibold text-gray-800 tabular-nums mt-1.5 truncate" dir="ltr">
              {Number(amount2).toLocaleString('en-US')} دولار
            </div>
          )}
        </div>
        {icon ? (
          <div
            className="shrink-0 rounded-xl p-2.5"
            style={{ backgroundColor: `${accent}14`, color: accent }}
            aria-hidden="true"
          >
            {icon}
          </div>
        ) : null}
      </div>
    </div>
  );
}

function ManagerStatsSection({
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

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 mb-6">
        <StatCard label="إجمالي الملفات" value={stats.totalFiles} amount={stats.totalAmount} amount2={stats.totalAmount2} accent="#059669" icon={ICONS.documents} />
        <StatCard label="متداول" value={stats.active} amount={stats.activeAmount} amount2={stats.activeAmount2} accent="#2563eb" icon={ICONS.active} />
        <StatCard label="تحت رفع" value={stats.drafts} amount={stats.draftsAmount} amount2={stats.draftsAmount2} accent="#d97706" icon={ICONS.drafts} />
        <StatCard label="تريث" value={stats.deferred} amount={stats.deferredAmount} amount2={stats.deferredAmount2} accent="#dc2626" icon={ICONS.deferred} />
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5 mb-6">
        <h3 className="font-bold text-gray-900 mb-4">المنفذ</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="rounded-xl border border-emerald-100 bg-emerald-50/50 p-4">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-emerald-600" aria-hidden="true" />
              <span className="font-bold text-gray-900">منفذ بالتسوية</span>
            </div>
            <div className="mt-3 flex flex-wrap gap-6">
              <div>
                <div className="text-2xl font-bold text-gray-900 tabular-nums" dir="ltr">
                  {stats.settledCount}
                </div>
                <div className="text-xs text-gray-500 mt-1">عدد الملفات</div>
              </div>
              <div className="min-w-0">
                <div className="text-2xl font-bold text-emerald-700 tabular-nums" dir="ltr">
                  {stats.settledCollected.toLocaleString('en-US')}
                </div>
                <div className="text-xs text-gray-500 mt-1">المبلغ المحصل</div>
              </div>
            </div>
          </div>
          <div className="rounded-xl border border-red-100 bg-red-50/50 p-4">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-red-600" aria-hidden="true" />
              <span className="font-bold text-gray-900">منفذ جبريا</span>
            </div>
            <div className="mt-3 flex flex-wrap gap-6">
              <div>
                <div className="text-2xl font-bold text-gray-900 tabular-nums" dir="ltr">
                  {stats.forcibleCount}
                </div>
                <div className="text-xs text-gray-500 mt-1">عدد الملفات</div>
              </div>
              <div className="min-w-0">
                <div className="text-2xl font-bold text-red-700 tabular-nums" dir="ltr">
                  {stats.forcibleCollected.toLocaleString('en-US')}
                </div>
                <div className="text-xs text-gray-500 mt-1">المبلغ المحصل</div>
              </div>
            </div>
          </div>
          <div className="rounded-xl border border-cyan-100 bg-cyan-50/50 p-4">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-cyan-600" aria-hidden="true" />
              <span className="font-bold text-gray-900">متداول للضد</span>
            </div>
            <div className="mt-3 flex flex-wrap gap-6">
              <div>
                <div className="text-2xl font-bold text-gray-900 tabular-nums" dir="ltr">
                  {Number(stats.tradingAgainstCount ?? 0)}
                </div>
                <div className="text-xs text-gray-500 mt-1">عدد الملفات</div>
              </div>
              <div className="min-w-0">
                <div className="text-2xl font-bold text-cyan-700 tabular-nums" dir="ltr">
                  {Number(stats.tradingAgainstAmount ?? 0).toLocaleString('en-US')}
                </div>
                <div className="text-xs text-gray-500 mt-1">المبلغ المطلوب دفعه من الجهة العامة</div>
              </div>
            </div>
          </div>
          <div className="rounded-xl border border-indigo-100 bg-indigo-50/50 p-4">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-indigo-600" aria-hidden="true" />
              <span className="font-bold text-gray-900">منفذ للضد</span>
            </div>
            <div className="mt-3 flex flex-wrap gap-6">
              <div>
                <div className="text-2xl font-bold text-gray-900 tabular-nums" dir="ltr">
                  {Number(stats.executedAgainstCount ?? 0)}
                </div>
                <div className="text-xs text-gray-500 mt-1">عدد الملفات</div>
              </div>
              <div className="min-w-0">
                <div className="text-2xl font-bold text-indigo-700 tabular-nums" dir="ltr">
                  {Number(stats.executedAgainstAmount ?? 0).toLocaleString('en-US')}
                </div>
                <div className="text-xs text-gray-500 mt-1">المبلغ الذي دفعته الجهة العامة</div>
              </div>
            </div>
          </div>
        </div>
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

const REMINDER_COLOR_STYLES: Record<string, string> = {
  'أحمر': 'bg-red-100 text-red-700 border-red-200',
  'بنفسجي': 'bg-purple-100 text-purple-700 border-purple-200',
  'أصفر': 'bg-amber-100 text-amber-700 border-amber-200',
};

const REMINDER_COLOR_DOTS: Record<string, string> = {
  'أحمر': '#dc2626',
  'بنفسجي': '#9333ea',
  'أصفر': '#f59e0b',
};

function daysUntilDue(dueDate: string): number {
  const due = new Date(dueDate);
  if (Number.isNaN(due.getTime())) return 0;
  due.setHours(0, 0, 0, 0);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((due.getTime() - today.getTime()) / 86_400_000);
}

function pluralDays(n: number): string {
  const abs = Math.abs(n);
  if (abs === 2) return 'يومين';
  if (abs >= 3 && abs <= 10) return `${abs} أيام`;
  return `${abs} يوم`;
}

function dueLabel(dueDate: string): { text: string; tone: string } {
  const days = daysUntilDue(dueDate);
  if (days < 0) return { text: `متأخر ${pluralDays(days)}`, tone: 'bg-red-100 text-red-700 border-red-200' };
  if (days === 0) return { text: 'اليوم', tone: 'bg-red-100 text-red-700 border-red-200' };
  if (days === 1) return { text: 'غدًا', tone: 'bg-amber-100 text-amber-800 border-amber-200' };
  return { text: `بعد ${pluralDays(days)}`, tone: 'bg-emerald-100 text-emerald-800 border-emerald-200' };
}

function borrowerFullName(r: ReminderDto): string {
  const parts = [r.borrowerName, r.borrowerFather, r.borrowerFamily].filter(Boolean);
  return parts.length > 0 ? parts.join(' ') : r.documentType || `مستند ${r.documentId}`;
}

/**
 * قائمة تذكيرات مشتركة للمحامي ورئيس القسم.
 * زر «إلغاء التذكير» يظهر فقط عند تمرير onCancel (المحامي)،
 * ويبقى العرض قراءة فقط لرئيس القسم.
 */
function ReminderList({
  reminders,
  onCancel,
  cancellingKey,
}: {
  reminders: ReminderDto[];
  onCancel?: (r: ReminderDto) => void;
  cancellingKey?: string | null;
}) {
  return (
    <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
      {reminders.map((r) => {
        const due = dueLabel(r.dueDate);
        const dot = REMINDER_COLOR_DOTS[r.reminderColor ?? ''];
        const isCancelling = cancellingKey === String(r.actionId);
        return (
          <li key={`${r.documentId}-${r.actionId}`}>
            <div className="flex items-center gap-3 px-4 sm:px-5 py-3">
              <span
                className="shrink-0 w-2.5 h-2.5 rounded-full"
                style={{ backgroundColor: dot ?? '#9ca3af' }}
                aria-hidden="true"
              />
              <Link
                to={`/documents/${r.documentId}`}
                className="min-w-0 flex-1 group min-h-11"
              >
                <span className="block font-medium text-gray-800 group-hover:text-emerald-700 truncate">
                  {borrowerFullName(r)}
                </span>
                <span className="block text-sm text-gray-500 truncate mt-0.5">{richToPlainText(r.actionText)}</span>
              </Link>
              <span className="shrink-0 flex flex-col items-end gap-1.5">
                {r.reminderColor ? (
                  <span
                    className={`text-[11px] px-2 py-0.5 rounded-full border ${
                      REMINDER_COLOR_STYLES[r.reminderColor] ?? 'bg-gray-100 text-gray-700 border-gray-200'
                    }`}
                  >
                    {r.reminderColor}
                  </span>
                ) : null}
                <span className={`text-[11px] px-2 py-0.5 rounded-full border ${due.tone}`}>{due.text}</span>
              </span>
              {onCancel ? (
                <button
                  type="button"
                  onClick={() => onCancel(r)}
                  disabled={isCancelling}
                  className="shrink-0 min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 text-gray-600 hover:text-red-700 hover:border-red-200 hover:bg-red-50 disabled:opacity-50 transition-colors"
                >
                  {isCancelling ? 'جارِ الإلغاء...' : 'إلغاء التذكير'}
                </button>
              ) : null}
            </div>
          </li>
        );
      })}
    </ul>
  );
}

const TARGET_TYPE_LABELS: Record<HeadAlertTargetType, string> = {
  document: 'مرتبط بملف',
  lawyer: 'رسالة لمحامٍ',
  branch: 'تعميم للفرع',
};

const TARGET_TYPE_BADGES: Record<HeadAlertTargetType, string> = {
  document: 'bg-sky-100 text-sky-700 border-sky-200',
  lawyer: 'bg-purple-100 text-purple-700 border-purple-200',
  branch: 'bg-emerald-100 text-emerald-700 border-emerald-200',
};

function formatDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleString('ar-SY');
}

/**
 * صف تنبيه واحد. يظهر زر «تمت القراءة» للمحامي فقط عبر onMarkRead،
 * وتظهر العدادات (غير مقروء/المجموع) لرئيس القسم فقط عند توفرها.
 */
function AlertRow({
  alert,
  onMarkRead,
  markingKey,
}: {
  alert: HeadAlertDto;
  onMarkRead?: (a: HeadAlertDto) => void;
  markingKey?: string | null;
}) {
  const isMarking = markingKey === String(alert.id);
  return (
    <li key={alert.id}>
      <div className="flex items-center gap-3 px-4 sm:px-5 py-3">
        <span
          className={`shrink-0 w-2 h-2 rounded-full ${alert.isRead ? 'bg-gray-300' : 'bg-red-500'}`}
          aria-hidden="true"
        />
        <div className="min-w-0 flex-1">
          {alert.documentId ? (
            <Link
              to={`/documents/${alert.documentId}`}
              className="block font-medium text-gray-800 hover:text-emerald-700 truncate"
            >
              {alert.message}
            </Link>
          ) : (
            <p className="font-medium text-gray-800 leading-snug">{alert.message}</p>
          )}
          <p className="text-sm text-gray-500 mt-0.5 truncate">
            {alert.createdByName ?? 'رئيس القسم'} · {formatDateTime(alert.createdAt)}
          </p>
          <div className="flex flex-wrap gap-1.5 mt-1.5">
            <span
              className={`text-[11px] px-2 py-0.5 rounded-full border ${
                TARGET_TYPE_BADGES[alert.targetType] ?? 'bg-gray-100 text-gray-700 border-gray-200'
              }`}
            >
              {TARGET_TYPE_LABELS[alert.targetType] ?? alert.targetType}
            </span>
            {alert.targetLawyerName ? (
              <span className="text-[11px] px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 border border-gray-200">
                إلى: {alert.targetLawyerName}
              </span>
            ) : null}
          </div>
        </div>
        <div className="shrink-0 flex flex-col items-end gap-1.5">
          {onMarkRead && !alert.isRead ? (
            <button
              type="button"
              onClick={() => onMarkRead(alert)}
              disabled={isMarking}
              className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 text-gray-600 hover:text-emerald-700 hover:border-emerald-200 hover:bg-emerald-50 disabled:opacity-50 transition-colors"
            >
              {isMarking ? 'جارِ التحديث...' : 'تمت القراءة'}
            </button>
          ) : onMarkRead && alert.isRead ? (
            <span className="text-[11px] px-2 py-0.5 rounded-full bg-gray-100 text-gray-500 border border-gray-200">
              مقروء
            </span>
          ) : null}
          {alert.recipientCount != null ? (
            <span className="text-[11px] px-2 py-0.5 rounded-full border border-gray-200 text-gray-600">
              غير مقروء: {alert.unreadCount ?? 0} / {alert.recipientCount}
            </span>
          ) : null}
        </div>
      </div>
    </li>
  );
}

/**
 * نموذج إصدار تنبيه لرئيس القسم: رسالة خاصة لمحامٍ أو تعميم لجميع محامي الفرع.
 * Mobile-first مع أهداف لمس 44px+.
 */
function CreateAlertForm({
  targetType,
  onTargetTypeChange,
  lawyers,
  lawyerId,
  onLawyerIdChange,
  message,
  onMessageChange,
  submitting,
  error,
  onSubmit,
  onCancel,
}: {
  targetType: HeadAlertTargetType;
  onTargetTypeChange: (t: HeadAlertTargetType) => void;
  lawyers: LawyerListItem[];
  lawyerId: string;
  onLawyerIdChange: (v: string) => void;
  message: string;
  onMessageChange: (v: string) => void;
  submitting: boolean;
  error: string;
  onSubmit: (e: FormEvent) => void;
  onCancel: () => void;
}) {
  return (
    <form onSubmit={onSubmit} className="px-4 sm:px-5 py-4 border-b border-gray-100 grid gap-4">
      <div role="group" aria-label="نوع التنبيه" className="flex flex-wrap gap-2">
        {(['lawyer', 'branch'] as HeadAlertTargetType[]).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => onTargetTypeChange(t)}
            aria-pressed={targetType === t}
            className={`min-h-11 px-4 rounded-xl text-sm font-medium transition-colors ${
              targetType === t
                ? 'bg-emerald-600 text-white'
                : 'text-gray-600 border border-gray-200 bg-white hover:bg-gray-50'
            }`}
          >
            {TARGET_TYPE_LABELS[t]}
          </button>
        ))}
      </div>

      {targetType === 'lawyer' ? (
        <div>
          <label htmlFor="alert-lawyer" className="block text-xs font-medium text-gray-600 mb-1">
            المحامي
          </label>
          <select
            id="alert-lawyer"
            value={lawyerId}
            onChange={(e) => onLawyerIdChange(e.target.value)}
            className="w-full min-h-11 rounded-xl border border-gray-200 bg-white px-3 text-sm"
          >
            <option value="">اختر محامياً...</option>
            {lawyers.map((l) => (
              <option key={l.id} value={l.id}>
                {l.fullName}
              </option>
            ))}
          </select>
        </div>
      ) : null}

      <div>
        <label htmlFor="alert-message" className="block text-xs font-medium text-gray-600 mb-1">
          نص التنبيه
        </label>
        <textarea
          id="alert-message"
          value={message}
          onChange={(e) => onMessageChange(e.target.value)}
          rows={3}
          className="w-full min-h-11 border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
      </div>

      {error ? <p className="text-red-600 text-sm">{error}</p> : null}

      <div className="flex flex-wrap gap-2">
        <button
          type="submit"
          disabled={submitting}
          className="min-h-11 px-4 rounded-lg bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white text-sm font-medium"
        >
          {submitting ? 'جارِ الإرسال...' : 'إرسال التنبيه'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="min-h-11 px-4 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50"
        >
          إلغاء
        </button>
      </div>
    </form>
  );
}

export default function Dashboard() {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';
  const isManager = user?.role === 'manager' || user?.role === 'admin';
  const isHead = user?.role === 'head';

  const [reminders, setReminders] = useState<ReminderDto[]>([]);
  const [cancellingKey, setCancellingKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState('');

  const [alerts, setAlerts] = useState<HeadAlertDto[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [alertsError, setAlertsError] = useState('');
  const [markingKey, setMarkingKey] = useState<string | null>(null);

  const [showAlertForm, setShowAlertForm] = useState(false);
  const [alertTargetType, setAlertTargetType] = useState<HeadAlertTargetType>('branch');
  const [alertLawyerId, setAlertLawyerId] = useState('');
  const [alertMessage, setAlertMessage] = useState('');
  const [alertSubmitting, setAlertSubmitting] = useState(false);
  const [alertFormError, setAlertFormError] = useState('');
  const [branchLawyers, setBranchLawyers] = useState<LawyerListItem[]>([]);

  const [period, setPeriod] = useState<StatsPeriod>('monthly');
  const [available, setAvailable] = useState<MonthlyStatDto[]>([]);
  const [selection, setSelection] = useState<PeriodSelection | null>(null);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [managerStats, setManagerStats] = useState<ManagerStatsDto | null>(null);
  const [lawyerStats, setLawyerStats] = useState<ManagerLawyerStatDto[]>([]);
  const [managerError, setManagerError] = useState('');

  const cancelReminder = async (r: ReminderDto) => {
    const key = String(r.actionId);
    setCancellingKey(key);
    setActionError('');
    try {
      await api.delete(`/documents/${r.documentId}/actions/${r.actionId}/reminder`);
      setReminders((prev) => prev.filter((x) => x.actionId !== r.actionId));
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setCancellingKey(null);
    }
  };

  const markAlertRead = async (a: HeadAlertDto) => {
    const key = String(a.id);
    setMarkingKey(key);
    setAlertsError('');
    try {
      await api.patch(`/alerts/${a.id}/read`);
      setAlerts((prev) => prev.map((x) => (x.id === a.id ? { ...x, isRead: true } : x)));
      setUnreadCount((c) => Math.max(0, c - 1));
    } catch (err) {
      setAlertsError(getApiErrorMessage(err));
    } finally {
      setMarkingKey(null);
    }
  };

  const submitAlert = async (e: FormEvent) => {
    e.preventDefault();
    if (!alertMessage.trim()) {
      setAlertFormError('نص التنبيه مطلوب');
      return;
    }
    let targetLawyerId: number | null = null;
    if (alertTargetType === 'lawyer') {
      targetLawyerId = alertLawyerId ? Number(alertLawyerId) : null;
      if (!targetLawyerId) {
        setAlertFormError('اختر المحامي المستلم');
        return;
      }
    }

    setAlertSubmitting(true);
    setAlertFormError('');
    try {
      const { data } = await api.post<HeadAlertDto>('/alerts', {
        targetType: alertTargetType,
        documentId: null,
        targetLawyerId,
        message: alertMessage.trim(),
      });
      setAlerts((prev) => [data, ...prev]);
      setShowAlertForm(false);
      setAlertMessage('');
      setAlertLawyerId('');
    } catch (err) {
      setAlertFormError(getApiErrorMessage(err));
    } finally {
      setAlertSubmitting(false);
    }
  };

  useEffect(() => {
    if (!user) return;

    if (isManager) {
      api
        .get<BranchDto[]>('/branches')
        .then((r) => setBranches(Array.isArray(r.data) ? r.data : []))
        .catch(() => {});
      return;
    }

    // التذكيرات خاصة بالمحامي فقط؛ لا تُجلب لرئيس القسم.
    if (isLawyer) {
      api
        .get<ReminderDto[]>('/reminders')
        .then((r) => setReminders(Array.isArray(r.data) ? r.data : []))
        .catch(() => {});
    }
  }, [isLawyer, isManager, user]);

  useEffect(() => {
    if (!user || isManager) return;

    api
      .get<HeadAlertDto[]>('/alerts')
      .then((r) => setAlerts(Array.isArray(r.data) ? r.data : []))
      .catch(() => setAlerts([]));

    if (isLawyer) {
      api
        .get<{ count: number }>('/alerts/unread-count')
        .then((r) => setUnreadCount(Number(r.data.count) || 0))
        .catch(() => setUnreadCount(0));
    }
  }, [isLawyer, isManager, user]);

  useEffect(() => {
    if (!isHead) return;

    api
      .get<LawyerListItem[]>('/users/lawyers')
      .then((r) => setBranchLawyers(Array.isArray(r.data) ? r.data : []))
      .catch(() => setBranchLawyers([]));
  }, [isHead]);

  useEffect(() => {
    if (!user) return;

    const params: Record<string, unknown> = {};
    if (isManager && branchId) params.branchId = branchId;
    api
      .get<MonthlyStatDto[]>('/stats/periods', { params })
      .then((r) => setAvailable(Array.isArray(r.data) ? r.data : []))
      .catch(() => setAvailable([]));
  }, [isManager, branchId, user]);

  useEffect(() => {
    const recent = mostRecentSelection(available, period);
    setSelection(recent);
  }, [available, period]);

  useEffect(() => {
    if (!user) return;

    setManagerError('');
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }

    if (isLawyer) {
      api
        .get<ManagerStatsDto>('/stats/me', { params })
        .then((r) => setManagerStats(r.data))
        .catch((err) => setManagerError(getApiErrorMessage(err)));
      return;
    }

    if (isManager && branchId) params.branchId = branchId;
    api
      .get<ManagerStatsDto>('/stats/manager', { params })
      .then((r) => setManagerStats(r.data))
      .catch((err) => setManagerError(getApiErrorMessage(err)));

    const lawyersBranch = isManager ? branchId : (user?.branchId ?? null);
    if (lawyersBranch) {
      api
        .get<ManagerLawyerStatDto[]>('/stats/manager/lawyers', {
          params: { ...params, branchId: lawyersBranch },
        })
        .then((r) => setLawyerStats(Array.isArray(r.data) ? r.data : []))
        .catch(() => setLawyerStats([]));
    } else {
      setLawyerStats([]);
    }
  }, [isLawyer, isManager, period, selection, branchId, user]);

  if (isManager) {
    return (
      <div className="max-w-7xl mx-auto">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6">لوحة التحكم</h2>
        <ManagerStatsSection
          period={period}
          onPeriodChange={setPeriod}
          availablePeriods={available}
          selection={selection}
          onSelectionChange={setSelection}
          branches={branches}
          branchId={branchId}
          onBranchChange={setBranchId}
          stats={managerStats}
          lawyers={lawyerStats}
          error={managerError}
        />
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto">
      <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6">لوحة التحكم</h2>

      <ManagerStatsSection
        period={period}
        onPeriodChange={setPeriod}
        availablePeriods={available}
        selection={selection}
        onSelectionChange={setSelection}
        branches={branches}
        branchId={user?.branchId ?? null}
        onBranchChange={() => {}}
        showBranchSelect={false}
        showLawyerTable={!isLawyer}
        stats={managerStats}
        lawyers={lawyerStats}
        error={managerError}
      />

      {isLawyer ? (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-amber-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">التذكيرات</h3>
                <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium">
                  {reminders.length}
                </span>
              </div>
              <span className="text-xs text-gray-400">الأقرب أولاً</span>
            </div>

            {actionError ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{actionError}</p>
              </div>
            ) : null}

            {reminders.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تذكيرات حالياً</p>
              </div>
            ) : (
              <ReminderList reminders={reminders} onCancel={cancelReminder} cancellingKey={cancellingKey} />
            )}
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-red-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">تنبيهات رئيس القسم</h3>
                {unreadCount > 0 ? (
                  <span className="text-xs bg-red-100 text-red-800 rounded-full px-2 py-0.5 font-medium">
                    {unreadCount} غير مقروء
                  </span>
                ) : null}
              </div>
              <span className="text-xs text-gray-400">الأحدث أولاً</span>
            </div>

            {alertsError ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{alertsError}</p>
              </div>
            ) : null}

            {alerts.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تنبيهات حالياً</p>
              </div>
            ) : (
              <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
                {alerts.map((a) => (
                  <AlertRow key={a.id} alert={a} onMarkRead={markAlertRead} markingKey={markingKey} />
                ))}
              </ul>
            )}
          </div>
        </>
      ) : (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-red-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">تنبيهات رئيس القسم</h3>
                <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium">
                  {alerts.length}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setShowAlertForm((v) => !v)}
                className="min-h-11 px-4 rounded-lg bg-emerald-800 hover:bg-emerald-700 text-white text-sm font-medium"
              >
                {showAlertForm ? 'إلغاء' : '+ إصدار تنبيه'}
              </button>
            </div>

            {showAlertForm ? (
              <CreateAlertForm
                targetType={alertTargetType}
                onTargetTypeChange={setAlertTargetType}
                lawyers={branchLawyers}
                lawyerId={alertLawyerId}
                onLawyerIdChange={setAlertLawyerId}
                message={alertMessage}
                onMessageChange={setAlertMessage}
                submitting={alertSubmitting}
                error={alertFormError}
                onSubmit={submitAlert}
                onCancel={() => setShowAlertForm(false)}
              />
            ) : null}

            {alertsError ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{alertsError}</p>
              </div>
            ) : null}

            {alerts.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تنبيهات حالياً</p>
              </div>
            ) : (
              <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
                {alerts.map((a) => (
                  <AlertRow key={a.id} alert={a} />
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </div>
  );
}
