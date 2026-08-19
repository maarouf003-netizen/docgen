import type { HeadAlertTargetType, ManagerStatsDto, MonthlyStatDto, ReminderDto, StatsPeriod } from '../../types';
import { tripleName } from '../../utils/documentDisplay';
import type { PeriodOption, PeriodSelection } from './dashboardTypes';

export const TARGET_TYPE_LABELS: Record<HeadAlertTargetType, string> = {
  document: 'مرتبط بملف',
  lawyer: 'رسالة لمحامٍ',
  branch: 'تعميم للفرع',
  head: 'مرحلة إنابة',
};

export const TARGET_TYPE_BADGES: Record<HeadAlertTargetType, string> = {
  document: 'bg-sky-100 text-sky-700 border-sky-200',
  lawyer: 'bg-purple-100 text-purple-700 border-purple-200',
  branch: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  head: 'bg-slate-100 text-slate-700 border-slate-200',
};

export const MONTHS = [
  'كانون الثاني', 'شباط', 'آذار', 'نيسان', 'أيار', 'حزيران',
  'تموز', 'آب', 'أيلول', 'تشرين الأول', 'تشرين الثاني', 'كانون الأول',
];

export const PERIODS: { key: StatsPeriod; label: string }[] = [
  { key: 'monthly', label: 'شهري' },
  { key: 'quarterly', label: 'ربعي' },
  { key: 'yearly', label: 'سنوي' },
];

export const QUARTERS = ['الأول', 'الثاني', 'الثالث', 'الرابع'];

/** تسمية قصيرة للعملة المعروفة في الإحصاءات (وإلا تبقى العملة كما هي). */
const CURRENCY_SHORT: Record<string, string> = {
  'ليرة سورية': 'ل.س',
  'دولار أمريكي': 'دولار',
  'يورو': 'يورو',
};

export function currencyLabel(currency: string): string {
  return CURRENCY_SHORT[currency] ?? currency;
}

/** الأصل الوحيد لعرض الأرقام في لوحة التحكم: فواصل الآلاف عبر Intl.NumberFormat (بدل toLocaleString المكرر). */
const numberFormatter = new Intl.NumberFormat('en-US');

export function formatNumber(value: number): string {
  return numberFormatter.format(value);
}

/** تسمية توضيحية للفترة المعروضة، محسوبة من حقول الفترة الصادرة من الخادم. */
export function periodLabel(stats: ManagerStatsDto): string {
  if (stats.periodMonth) {
    return `${MONTHS[stats.periodMonth - 1]} ${stats.periodYear}`;
  }
  if (stats.periodQuarter) {
    return `الربع ${QUARTERS[stats.periodQuarter - 1]} ${stats.periodYear}`;
  }
  return `السنة ${stats.periodYear}`;
}

/** خيارات الفترة المحددة من الأشهر المتاحة (تاريخ القيد): شهر/ربع/سنة، الأحدث أولًا. */
export function periodOptions(available: MonthlyStatDto[], period: StatsPeriod): PeriodOption[] {
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
export function mostRecentSelection(available: MonthlyStatDto[], period: StatsPeriod): PeriodSelection | null {
  const option = periodOptions(available, period)[0];
  return option ? { year: option.year, month: option.month, quarter: option.quarter } : null;
}

/** قيمة الفترة المحددة كمفتاح مقارنة مع خيارات periodOptions. */
export function selectionValue(selection: PeriodSelection | null): string {
  if (!selection) return '';
  if (selection.month != null) return `${selection.year}-${selection.month}`;
  if (selection.quarter != null) return `${selection.year}-${selection.quarter}`;
  return String(selection.year);
}

export function daysUntilDue(dueDate: string): number {
  const due = new Date(dueDate);
  if (Number.isNaN(due.getTime())) return 0;
  due.setHours(0, 0, 0, 0);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((due.getTime() - today.getTime()) / 86_400_000);
}

export function pluralDays(n: number): string {
  const abs = Math.abs(n);
  if (abs === 2) return 'يومين';
  if (abs >= 3 && abs <= 10) return `${abs} أيام`;
  return `${abs} يوم`;
}

export function dueLabel(dueDate: string): { text: string; tone: string } {
  const days = daysUntilDue(dueDate);
  if (days < 0) return { text: `متأخر ${pluralDays(days)}`, tone: 'bg-red-100 text-red-700 border-red-200' };
  if (days === 0) return { text: 'اليوم', tone: 'bg-red-100 text-red-700 border-red-200' };
  if (days === 1) return { text: 'غدًا', tone: 'bg-amber-100 text-amber-800 border-amber-200' };
  return { text: `بعد ${pluralDays(days)}`, tone: 'bg-emerald-100 text-emerald-800 border-emerald-200' };
}

export function borrowerFullName(r: ReminderDto): string {
  return tripleName(r.borrowerName, r.borrowerFather, r.borrowerFamily) || r.documentType || `مستند ${r.documentId}`;
}
