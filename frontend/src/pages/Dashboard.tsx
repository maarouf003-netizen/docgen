import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import type { BranchDto, DashboardStatsDto, DocumentResponse, ManagerLawyerStatDto, ManagerStatsDto, MonthlyStatDto, ReminderDto, StatsPeriod } from '../types';

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

/** تسمية توضيحية للفترة المعروضة، محسوبة من حقول الفترة الصادرة من الخادم. */
function periodLabel(stats: ManagerStatsDto): string {
  if (stats.periodMonth) {
    return `الشهر الحالي — ${MONTHS[stats.periodMonth - 1]} ${stats.periodYear}`;
  }
  if (stats.periodQuarter) {
    return `الربع الحالي — الربع ${QUARTERS[stats.periodQuarter - 1]} ${stats.periodYear}`;
  }
  return `السنة الحالية — ${stats.periodYear}`;
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

function StatCard({ label, value, accent, icon }: { label: string; value: string | number; accent: string; icon?: ReactNode }) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5 relative overflow-hidden">
      <span className="absolute inset-x-0 top-0 h-1" style={{ backgroundColor: accent }} aria-hidden="true" />
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-2xl sm:text-3xl font-bold text-gray-900 tabular-nums" dir="ltr">
            {value}
          </div>
          <div className="text-xs sm:text-sm text-gray-500 mt-1.5 leading-snug">{label}</div>
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
  branches,
  branchId,
  onBranchChange,
  stats,
  lawyers,
  error,
}: {
  period: StatsPeriod;
  onPeriodChange: (p: StatsPeriod) => void;
  branches: BranchDto[];
  branchId: number | null;
  onBranchChange: (id: number | null) => void;
  stats: ManagerStatsDto | null;
  lawyers: ManagerLawyerStatDto[];
  error: string;
}) {
  if (error) return <div className="text-red-600">{error}</div>;
  if (!stats) return <div className="text-gray-500">جارِ التحميل...</div>;

  return (
    <>
      <div className="flex flex-col sm:flex-row gap-3 sm:items-center justify-between mb-6">
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
      </div>

      <p className="text-sm text-gray-500 mb-6">
        عرض الفترة: <span className="font-medium text-gray-800">{periodLabel(stats)}</span>
      </p>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 mb-6">
        <StatCard label="إجمالي الملفات" value={stats.totalFiles} accent="#059669" icon={ICONS.documents} />
        <StatCard label="متداول" value={stats.active} accent="#2563eb" icon={ICONS.active} />
        <StatCard label="تحت رفع" value={stats.drafts} accent="#d97706" icon={ICONS.drafts} />
        <StatCard label="تريث" value={stats.deferred} accent="#dc2626" icon={ICONS.deferred} />
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
        </div>
      </div>

      {branchId ? (
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
      ) : (
        <p className="text-sm text-gray-400 mb-6">اختر فرعًا لعرض إحصائيات محامي الفرع</p>
      )}
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
                <span className="block text-sm text-gray-500 truncate mt-0.5">{r.actionText}</span>
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

export default function Dashboard() {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';
  const isManager = user?.role === 'manager' || user?.role === 'admin';

  const [stats, setStats] = useState<DashboardStatsDto | null>(null);
  const [monthly, setMonthly] = useState<MonthlyStatDto[]>([]);
  const [recent, setRecent] = useState<DocumentResponse[]>([]);
  const [reminders, setReminders] = useState<ReminderDto[]>([]);
  const [error, setError] = useState('');
  const [cancellingKey, setCancellingKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState('');

  const [period, setPeriod] = useState<StatsPeriod>('monthly');
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

  useEffect(() => {
    if (!user) return;

    if (isManager) {
      api
        .get<BranchDto[]>('/branches')
        .then((r) => setBranches(Array.isArray(r.data) ? r.data : []))
        .catch(() => {});
      return;
    }

    api
      .get<DashboardStatsDto>('/dashboard')
      .then((r) => setStats(r.data))
      .catch((err) => setError(getApiErrorMessage(err)));

    api
      .get<ReminderDto[]>('/reminders')
      .then((r) => setReminders(Array.isArray(r.data) ? r.data : []))
      .catch(() => {});

    if (!isLawyer) {
      api
        .get<MonthlyStatDto[]>('/monthly-stats')
        .then((r) => setMonthly(r.data))
        .catch(() => {});
      api
        .get<{ items: DocumentResponse[] }>('/documents?perPage=10')
        .then((r) => setRecent(r.data.items))
        .catch(() => {});
    }
  }, [isLawyer, isManager, user]);

  useEffect(() => {
    if (!isManager || !user) return;

    setManagerError('');
    api
      .get<ManagerStatsDto>('/stats/manager', { params: { period, branchId: branchId ?? undefined } })
      .then((r) => setManagerStats(r.data))
      .catch((err) => setManagerError(getApiErrorMessage(err)));

    if (branchId) {
      api
        .get<ManagerLawyerStatDto[]>('/stats/manager/lawyers', { params: { period, branchId } })
        .then((r) => setLawyerStats(Array.isArray(r.data) ? r.data : []))
        .catch(() => {});
    } else {
      setLawyerStats([]);
    }
  }, [isManager, period, branchId, user]);

  if (isManager) {
    return (
      <div className="max-w-7xl mx-auto">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6">لوحة التحكم</h2>
        <ManagerStatsSection
          period={period}
          onPeriodChange={setPeriod}
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

  if (error) return <div className="text-red-600">{error}</div>;
  if (!stats) return <div className="text-gray-500">جارِ التحميل...</div>;

  const maxMonth = monthly.length ? Math.max(...monthly.map((m) => m.count)) : 1;

  return (
    <div className="max-w-7xl mx-auto">
      <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6">لوحة التحكم</h2>

      {isLawyer ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 mb-8">
          <StatCard label="إجمالي المستندات" value={stats.totalDocuments} accent="#059669" icon={ICONS.documents} />
          <StatCard label="متداول" value={stats.totalActive} accent="#2563eb" icon={ICONS.active} />
          <StatCard label="تحت رفع" value={stats.totalDrafts} accent="#d97706" icon={ICONS.drafts} />
          <StatCard label="تريث" value={stats.totalDeferred} accent="#dc2626" icon={ICONS.deferred} />
          <StatCard label="منفذ" value={stats.totalExecuted} accent="#047857" icon={ICONS.executed} />
          <StatCard
            label="إجمالي المبالغ (باستثناء تحت رفع)"
            value={stats.totalAmount.toLocaleString('en-US')}
            accent="#7c3aed"
            icon={ICONS.amount}
          />
          <StatCard
            label="إجمالي المبالغ المحصلة"
            value={stats.totalCollectedAmount.toLocaleString('en-US')}
            accent="#0f766e"
            icon={ICONS.collected}
          />
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4 mb-8">
          <StatCard label="إجمالي المستندات" value={stats.totalDocuments} accent="#059669" icon={ICONS.documents} />
          <StatCard label="تحت رفع" value={stats.totalDrafts} accent="#d97706" icon={ICONS.drafts} />
          <StatCard label="منفذة" value={stats.totalExecuted} accent="#2563eb" icon={ICONS.executed} />
          <StatCard label="مؤجلة (تريث)" value={stats.totalDeferred} accent="#dc2626" icon={ICONS.deferred} />
          <StatCard label="عدد المقترضين" value={stats.totalBorrowers} accent="#7c3aed" icon={ICONS.borrowers} />
          <StatCard
            label="إجمالي المبالغ (غير تحت رفع)"
            value={stats.totalAmount.toLocaleString('en-US')}
            accent="#0f766e"
            icon={ICONS.amount}
          />
          <StatCard
            label="إجمالي المبلغ المحصل"
            value={stats.totalCollectedAmount.toLocaleString('en-US')}
            accent="#047857"
            icon={ICONS.collected}
          />
        </div>
      )}

      {isLawyer ? (
        <div className="grid lg:grid-cols-3 gap-6">
          <div className="lg:col-span-3 bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden">
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
        </div>
      ) : (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mb-6">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-red-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">تنبيهات رئيس القسم</h3>
                <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium">
                  {reminders.length}
                </span>
              </div>
              <span className="text-xs text-gray-400">الأقرب أولاً</span>
            </div>
            {reminders.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تذكيرات حالياً</p>
              </div>
            ) : (
              <ReminderList reminders={reminders} />
            )}
          </div>

          <div className="grid md:grid-cols-2 gap-6">
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
            <h3 className="font-bold text-gray-800 mb-4">المستندات شهرياً</h3>
            {monthly.length === 0 ? (
              <p className="text-gray-400 text-sm">لا توجد بيانات بعد</p>
            ) : (
              <div className="flex items-end gap-2 h-40">
                {monthly.map((m) => (
                  <div key={`${m.year}-${m.month}`} className="flex-1 flex flex-col items-center gap-1">
                    <span className="text-xs text-gray-600">{m.count}</span>
                    <div
                      className="w-full bg-emerald-600 rounded-t"
                      style={{ height: `${Math.max(4, (m.count / maxMonth) * 120)}px` }}
                      title={`${MONTHS[m.month - 1]} ${m.year}`}
                    />
                    <span className="text-[10px] text-gray-400">{MONTHS[m.month - 1].slice(0, 6)}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
            <h3 className="font-bold text-gray-800 mb-4">أحدث المستندات</h3>
            {recent.length === 0 ? (
              <p className="text-gray-400 text-sm">لا توجد مستندات بعد</p>
            ) : (
              <ul className="divide-y divide-gray-100">
                {recent.map((d) => (
                  <li key={d.id} className="py-2.5">
                    <Link to={`/documents/${d.id}`} className="inline-flex items-center flex-wrap min-h-11 hover:text-emerald-700">
                      <span className="font-medium">{d.documentType || `مستند ${d.id}`}</span>
                      <span className="text-gray-400 text-xs mr-2">
                        {d.borrowerName} {d.borrowerFamily}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
        </>
      )}
    </div>
  );
}
