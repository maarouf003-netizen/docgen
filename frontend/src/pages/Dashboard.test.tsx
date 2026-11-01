import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import Dashboard from './Dashboard';
import type {
  DashboardStatsDto,
  HeadAlertSummaryDto,
  LawyerListItem,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
  PersonalReminderDto,
  ReminderDto,
} from '../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/client')>();
  return {
    ...actual,
    api: { get: vi.fn(), post: vi.fn(), patch: vi.fn(), delete: vi.fn() },
  };
});

import { api } from '../api/client';
import { currentWeekRange } from '../components/dashboard/personalReminders';
import { dueLabel } from '../components/dashboard/dashboardFormat';
import { makeHeadAlertSummary } from '../test/factories';

/**
 * تاريخ `yyyy-MM-dd` (بتوقيت الظهيرة المحلي — ثابت اليوم في كل المناطق)
 * بعد `offset` أيام من بداية الأسبوع الحالي، مثبّتًا داخل الأسبوع.
 */
function inWeekIso(offset: number): string {
  const { fromDate } = currentWeekRange();
  const d = new Date(fromDate);
  d.setDate(d.getDate() + Math.min(Math.max(offset, 0), 6));
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T12:00:00`;
}

const STATS: DashboardStatsDto = {
  totalDocuments: 10,
  totalDrafts: 1,
  totalExecuted: 3,
  totalDeferred: 0,
  totalActive: 4,
  totalBorrowers: 5,
  totalAmount: 400,
  totalCollectedAmount: 600,
};

const MANAGER_STATS: ManagerStatsDto = {
  totalFiles: 10,
  active: 4,
  drafts: 2,
  deferred: 1,
  settledCount: 2,
  settledCollected: 1500,
  settledCollectedAmounts: [{ currency: 'ليرة سورية', amount: 1500 }],
  forcibleCount: 1,
  forcibleCollected: 500,
  forcibleCollectedAmounts: [{ currency: 'ليرة سورية', amount: 500 }],
  tradingAgainstCount: 0,
  executedAgainstCount: 0,
  executedAgainstAmount: 0,
  depositTradingCount: 1,
  depositExecutedCount: 2,
  depositExecutedAmount: 750,
  totalAmounts: [{ currency: 'ليرة سورية', amount: 4500 }],
  activeSplit: {
    bankingCount: 2,
    ordinaryCount: 0,
    bankingAmounts: [
      { currency: 'ليرة سورية', amount: 2000 },
      { currency: 'دولار أمريكي', amount: 5000 },
    ],
    ordinaryAmounts: [],
  },
  draftsSplit: {
    bankingCount: 1,
    ordinaryCount: 0,
    bankingAmounts: [
      { currency: 'ليرة سورية', amount: 1600 },
      { currency: 'دولار أمريكي', amount: 200 },
    ],
    ordinaryAmounts: [],
  },
  deferredSplit: {
    bankingCount: 1,
    ordinaryCount: 0,
    bankingAmounts: [
      { currency: 'ليرة سورية', amount: 1000 },
      { currency: 'دولار أمريكي', amount: 5200 },
    ],
    ordinaryAmounts: [],
  },
  referredToStartCount: 2,
  referredSplit: {
    bankingCount: 1,
    ordinaryCount: 1,
    bankingAmounts: [{ currency: 'ليرة سورية', amount: 800 }],
    ordinaryAmounts: [{ currency: 'دولار أمريكي', amount: 1200 }],
  },
  tradingAgainstAmounts: [],
  periodYear: 2026,
  periodQuarter: null,
  periodMonth: 8,
  periodDateFallbackCount: 0,
  periodDateFromReceiptCount: 0,
};

const MANAGER_LAWYERS: ManagerLawyerStatDto[] = [
  { lawyerId: 1, lawyerName: 'محامي دمشق', totalCount: 3, points: [{ year: 2026, month: 8, count: 3, fromCreatedAtCount: 0 }] },
];

const PERIODS: MonthlyStatDto[] = [
  { year: 2026, month: 8, count: 3 },
  { year: 2026, month: 7, count: 2 },
  { year: 2025, month: 12, count: 1 },
];

const BRANCH_LAWYERS: LawyerListItem[] = [
  { id: 2, username: 'lawyer2', fullName: 'محامي دمشق', isActive: true, branchId: 1, branchName: 'الفرع الرئيسي - دمشق' },
];

const LAWYER_ALERTS: HeadAlertSummaryDto[] = [
  makeHeadAlertSummary({
    id: 1,
    kind: 'manual_to_document',
    messagePreview: 'راجع ملف القرض',
    targetType: 'document',
    documentId: 5,
    isRead: false,
    createdAt: '2026-08-01T10:00:00Z',
  }),
  makeHeadAlertSummary({
    id: 2,
    messagePreview: 'تعميم اجتماع الفرع',
    isRead: true,
    createdAt: '2026-07-20T10:00:00Z',
  }),
];

const HEAD_ALERTS: HeadAlertSummaryDto[] = [
  makeHeadAlertSummary({
    id: 3,
    messagePreview: 'تعميم يوم الأحد',
    recipientCount: 2,
    unreadCount: 2,
  }),
  makeHeadAlertSummary({
    id: 1,
    kind: 'manual_to_document',
    messagePreview: 'راجع ملف القرض',
    targetType: 'document',
    documentId: 5,
    recipientCount: 1,
    unreadCount: 0,
    createdAt: '2026-08-01T10:00:00Z',
  }),
];

function managerStatsFor(period?: string): ManagerStatsDto {
  if (period === 'quarterly') {
    return { ...MANAGER_STATS, periodYear: 2026, periodQuarter: 3, periodMonth: null };
  }
  if (period === 'yearly') {
    return { ...MANAGER_STATS, periodYear: 2026, periodQuarter: null, periodMonth: null };
  }
  return MANAGER_STATS;
}

/** `PagedResult` كما يصل على السلك (`pageSize=100`) — الشكل الوحيد المقبول (`D10=atomic`). */
function pageResult<T>(items: T[], totalCount = items.length, page = 1) {
  return {
    items,
    page,
    perPage: 100,
    totalCount,
    totalPages: Math.ceil(totalCount / 100),
  };
}

function mockApi(overrides?: {
  reminders?: ReminderDto[];
  monthly?: [];
  alerts?: HeadAlertSummaryDto[];
  /** عناصر الصفحة الثانية (اختبار «تحميل المزيد»). */
  alertsPage2?: HeadAlertSummaryDto[];
  /** يفشل أول طلب لقائمة المستلمات — اختبار «إعادة المحاولة». */
  alertsFailFirst?: boolean;
  /** رقم صفحة يفشل طلبها (اختبار فشل «تحميل المزيد»). */
  alertsFailPage?: number;
  /** يفشل أول طلب لقائمة الصادر — اختبار شارة الإجمالي المجهول. */
  sentFailFirst?: boolean;
  /** إجمالي غير المقروء في التنبيهات (`scope` حسب الدور في الطلب نفسه). */
  unreadCount?: number;
  /** إجمالي صفوف قائمة المستلمات عند تجاوزه لعناصر الصفحة (اختبار «تحميل المزيد»). */
  alertsTotal?: number;
  lawyers?: Array<{ id: number; fullName: string; isActive?: boolean }>;
  personal?: PersonalReminderDto[];
  pendingReviews?: number;
  urgentCount?: number;
  pendingDelegationsCount?: number;
  entityReviewCount?: number;
  circuits?: Array<{ id: number; name: string; pendingCount: number }>;
  pendingRegistrations?: Array<{ documentId: number }>;
  sent?: HeadAlertSummaryDto[];
  sentTotal?: number;
}) {
  const reminders = overrides?.reminders ?? [];
  const monthly = overrides?.monthly ?? [];
  const alerts = overrides?.alerts ?? [];
  const alertsPage2 = overrides?.alertsPage2 ?? [];
  const alertsTotal = overrides?.alertsTotal;
  const unreadCount = overrides?.unreadCount ?? 0;
  const lawyers = overrides?.lawyers ?? [];
  const personal = overrides?.personal ?? [];
  const pendingReviews = overrides?.pendingReviews ?? 0;
  const urgentCount = overrides?.urgentCount ?? 0;
  const pendingDelegationsCount = overrides?.pendingDelegationsCount ?? 0;
  const entityReviewCount = overrides?.entityReviewCount ?? 0;
  const circuits = overrides?.circuits ?? [];
  const pendingRegistrations = overrides?.pendingRegistrations ?? [];
  const sent = overrides?.sent ?? [];
  const sentTotal = overrides?.sentTotal;
  let alertsAttempts = 0;
  let sentAttempts = 0;
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation(
    (url: string, config?: { params?: Record<string, unknown> }) => {
      const requestedPage = typeof config?.params?.page === 'number' ? config.params.page : 1;
      if (url === '/dashboard') return Promise.resolve({ data: STATS });
      if (url === '/reminders') return Promise.resolve({ data: reminders });
      if (url === '/appeals/reminders') return Promise.resolve({ data: [] });
      if (url === '/personal-reminders') return Promise.resolve({ data: personal });
      if (url === '/alerts') {
        alertsAttempts += 1;
        if (overrides?.alertsFailFirst && alertsAttempts === 1) {
          return Promise.reject(new Error('تعذّر جلب التنبيهات'));
        }
        const rows = requestedPage === 1 ? alerts : alertsPage2;
        if (overrides?.alertsFailPage === requestedPage) {
          return Promise.reject(new Error('تعذّر جلب الصفحة'));
        }
        return Promise.resolve({ data: pageResult(rows, alertsTotal, requestedPage) });
      }
      if (url === '/alerts/sent') {
        sentAttempts += 1;
        if (overrides?.sentFailFirst && sentAttempts === 1) {
          return Promise.reject(new Error('تعذّر جلب الصادر'));
        }
        const rows = requestedPage === 1 ? sent : [];
        return Promise.resolve({ data: pageResult(rows, sentTotal, requestedPage) });
      }
      if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: unreadCount } });
      if (url === '/users/lawyers') return Promise.resolve({ data: lawyers });
      if (url === '/monthly-stats') return Promise.resolve({ data: monthly });
      if (url === '/review-letters/pending-count') return Promise.resolve({ data: { count: pendingReviews } });
      if (url === '/correspondence/urgent-unseen-count') return Promise.resolve({ data: { count: urgentCount } });
      if (url === '/delegations/pending-count') return Promise.resolve({ data: { count: pendingDelegationsCount } });
      if (url === '/entity-registry/pending-review-count') return Promise.resolve({ data: { count: entityReviewCount } });
      if (url === '/execution-circuits/mine') return Promise.resolve({ data: circuits });
      if (url === '/documents/my-pending-registrations') return Promise.resolve({ data: pendingRegistrations });
      if (url === '/stats/periods') return Promise.resolve({ data: PERIODS });
      if (url === '/branches') {
        return Promise.resolve({
          data: [
            { id: 1, name: 'الفرع الرئيسي - دمشق', code: 'DAM' },
            { id: 2, name: 'فرع حلب', code: 'ALP' },
          ],
        });
      }
      if (url === '/stats/manager') {
        const period = typeof config?.params?.period === 'string' ? config.params.period : 'yearly';
        return Promise.resolve({ data: managerStatsFor(period) });
      }
      if (url === '/stats/manager/lawyers') return Promise.resolve({ data: MANAGER_LAWYERS });
      return Promise.resolve({ data: {} });
    },
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  useAuthMock.mockReturnValue({
    user: { id: 1, username: 'lawyer1', fullName: 'محامي', role: 'lawyer', branchId: 1 },
  });
});

describe('Dashboard للمحامي', () => {
  it('يعرض الترحيب وصف الأيقونات دون إحصائيات (لها صفحة `/stats` مستقلة)', async () => {
    mockApi();

    render(<Dashboard />);

    // الترحيب بالاسم الأول (صيغة الزيارة الأولى بعد مسح التخزين).
    expect(await screen.findByRole('heading', { name: 'مرحبًا، محامي' })).toBeInTheDocument();

    // صف الأيقونات الست بروابط الصفحات.
    const quickNav = screen.getByRole('navigation', { name: 'أقسام لوحة المحامي' });
    expect(within(quickNav).getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(within(quickNav).getByRole('link', { name: 'المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(within(quickNav).getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(within(quickNav).getByRole('link', { name: 'التقويم' })).toHaveAttribute('href', '/calendar');
    expect(within(quickNav).getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute('href', '/account');
    // بعد نجاح الجلب والصفر المؤكد تُحجب بطاقة «ملفات معلقة» (fail-open فقط أثناء التحميل/الخطأ).
    await waitFor(() => {
      expect(within(quickNav).queryByRole('link', { name: 'ملفات معلقة' })).not.toBeInTheDocument();
    });

    // لا إحصائيات في اللوحة إطلاقًا.
    expect(screen.queryByRole('heading', { name: /متداولة ضمن/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'إجمالي الملفات' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'شهري' })).not.toBeInTheDocument();

    // قسم التقويم المصغر ما زال في اللوحة.
    expect(screen.getByRole('heading', { name: 'التقويم' })).toBeInTheDocument();

    expect(api.get).not.toHaveBeenCalledWith('/stats/me', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/appeals/reminders', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/personal-reminders', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/review-letters/unseen-replies-count', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/correspondence/urgent-unseen-count', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/reminders', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith(
      '/alerts',
      expect.objectContaining({ params: expect.objectContaining({ page: 1, pageSize: 100 }) }),
    );
    expect(api.get).toHaveBeenCalledWith(
      '/alerts/unread-count',
      expect.objectContaining({ params: { scope: 'user' } }),
    );
    expect(api.get).not.toHaveBeenCalledWith('/users/lawyers', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    expect(api.get).not.toHaveBeenCalledWith('/monthly-stats');
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager');
    expect(api.get).not.toHaveBeenCalledWith('/branches', expect.any(Object));
    expect(screen.queryByText('إحصائيات محامي الفرع')).not.toBeInTheDocument();
  });

  it('يُظهر جرس التقويم عند تذكير اليوم أو متأخر فقط', async () => {
    mockApi({
      reminders: [
        {
          actionId: 1,
          documentId: 5,
          documentType: 'متداول - سامر حسن',
          borrowerName: 'سامر',
          borrowerFather: 'محمد',
          borrowerFamily: 'حسن',
          actionText: 'مراجعة دائرة التنفيذ',
          reminderColor: 'أحمر',
          dueDate: '2000-01-01',
          dueDateSuspect: false,
        },
      ],
    });

    render(<Dashboard />);

    expect(await screen.findByRole('link', { name: 'التقويم — تذكير واحد اليوم أو متأخر' })).toBeInTheDocument();
  });

  it('يضمّن الجرس التذكيرات الشخصية المستحقة ويتجاهل المنجزة', async () => {
    mockApi({
      personal: [
        {
          id: 1,
          title: 'متأخر',
          notes: null,
          dueDate: '2000-01-01',
          color: 'أحمر',
          recurrence: 'مرة واحدة',
          recurrenceEnd: null,
          isArchived: false,
          completedOccurrenceKeys: [],
          createdAt: '2000-01-01',
        },
        {
          id: 2,
          title: 'منجز',
          notes: null,
          dueDate: '2000-01-01',
          color: 'أحمر',
          recurrence: 'مرة واحدة',
          recurrenceEnd: null,
          isArchived: false,
          completedOccurrenceKeys: ['2000-01-01'],
          createdAt: '2000-01-01',
        },
      ],
    });

    render(<Dashboard />);

    expect(await screen.findByRole('link', { name: 'التقويم — تذكير واحد اليوم أو متأخر' })).toBeInTheDocument();
  });

  it('يُظهر شارة الملفات المحالة للمحامي بعدّادها', async () => {
    mockApi({ pendingRegistrations: [{ documentId: 7 }, { documentId: 8 }] });

    render(<Dashboard />);

    const quickNav = await screen.findByRole('navigation', { name: 'أقسام لوحة المحامي' });
    expect(
      within(quickNav).getByRole('link', { name: 'ملفات معلقة — 2 ملفات بانتظار تحديث بياناتها' }),
    ).toHaveAttribute('href', '/pending-registrations');
    expect(api.get).toHaveBeenCalledWith('/documents/my-pending-registrations', expect.any(Object));
  });

  it('يضمّن عدّاد بطاقة التذكيرات الشخصي مع سطر إحالة للتقويم', async () => {
    mockApi({
      personal: [
        {
          id: 1,
          title: 'شخصي أول',
          notes: null,
          dueDate: inWeekIso(1),
          color: 'زمردي',
          recurrence: 'مرة واحدة',
          recurrenceEnd: null,
          isArchived: false,
          completedOccurrenceKeys: [],
          createdAt: '2026-08-01',
        },
        {
          id: 2,
          title: 'شخصي ثانٍ',
          notes: null,
          dueDate: inWeekIso(2),
          color: 'أحمر',
          recurrence: 'مرة واحدة',
          recurrenceEnd: null,
          isArchived: false,
          completedOccurrenceKeys: [],
          createdAt: '2026-08-01',
        },
      ],
    });

    render(<Dashboard />);

    await waitFor(() => expect(screen.queryByText('لا توجد تذكيرات هذا الأسبوع')).not.toBeInTheDocument());
    const referral = screen.getByRole('link', { name: 'تذكيرات شخصية هذا الأسبوع (2) — عرض في التقويم ←' });
    expect(referral).toHaveAttribute('href', '/calendar');
  });

  it('يعرض تذكيرات الأسبوع بالاسم الثلاثي مع النص والشارة ورابط صفحة الملف', async () => {
    const due1 = inWeekIso(2);
    const due2 = inWeekIso(3);
    mockApi({
      reminders: [
        {
          actionId: 1,
          documentId: 5,
          documentType: 'متداول - سامر حسن',
          borrowerName: 'سامر',
          borrowerFather: 'محمد',
          borrowerFamily: 'حسن',
          actionText: 'مراجعة دائرة التنفيذ',
          reminderColor: 'أحمر',
          dueDate: due1,
          dueDateSuspect: false,
        },
        {
          actionId: 2,
          documentId: 8,
          documentType: 'متداول - أحمد العلي',
          borrowerName: 'أحمد',
          borrowerFather: 'خالد',
          borrowerFamily: 'العلي',
          actionText: 'تقديم كتاب براءة',
          reminderColor: 'بنفسجي',
          dueDate: due2,
          dueDateSuspect: false,
        },
      ],
    });

    render(<Dashboard />);

    const first = await screen.findByRole('link', { name: /سامر محمد حسن/ });
    expect(first).toHaveAttribute('href', '/documents/5');
    expect(screen.getByText('سامر محمد حسن')).toBeInTheDocument();
    expect(screen.getByText('أحمد خالد العلي')).toBeInTheDocument();
    expect(screen.getByText('مراجعة دائرة التنفيذ')).toBeInTheDocument();
    expect(screen.getByText('أحمر')).toBeInTheDocument();
    expect(screen.getByText('بنفسجي')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /أحمد خالد العلي/ })).toHaveAttribute('href', '/documents/8');
    expect(screen.getByText(dueLabel(due1).text)).toBeInTheDocument();
    expect(screen.getByText(dueLabel(due2).text)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'إلغاء التذكير' }).length).toBe(2);
  });

  it('يخفي تذكيرات خارج الأسبوع الحالي من بطاقة اللوحة', async () => {
    mockApi({
      reminders: [
        {
          actionId: 1,
          documentId: 5,
          documentType: 'متداول - سامر حسن',
          borrowerName: 'سامر',
          borrowerFather: 'محمد',
          borrowerFamily: 'حسن',
          actionText: 'مراجعة بعيدة',
          reminderColor: 'أحمر',
          dueDate: '2030-01-01T12:00:00',
          dueDateSuspect: false,
        },
      ],
    });

    render(<Dashboard />);

    await waitFor(() => expect(screen.queryByText('لا توجد تذكيرات هذا الأسبوع')).not.toBeNull());
    expect(screen.getByText('لا توجد تذكيرات هذا الأسبوع')).toBeInTheDocument();
    expect(screen.queryByText('سامر محمد حسن')).not.toBeInTheDocument();
  });

  it('يلغي التذكير ويستدعي النقطة المناسبة ويزيل العنصر من القائمة', async () => {
    const user = userEvent.setup();
    mockApi({
      reminders: [
        {
          actionId: 7,
          documentId: 5,
          documentType: 'متداول - سامر حسن',
          borrowerName: 'سامر',
          borrowerFather: 'محمد',
          borrowerFamily: 'حسن',
          actionText: 'مراجعة دائرة التنفيذ',
          reminderColor: 'أحمر',
          dueDate: inWeekIso(1),
          dueDateSuspect: false,
        },
      ],
    });
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    render(<Dashboard />);

    const button = await screen.findByRole('button', { name: 'إلغاء التذكير' });
    await user.click(button);

    expect(api.delete).toHaveBeenCalledWith('/documents/5/actions/7/reminder');
    expect(await screen.findByText('لا توجد تذكيرات هذا الأسبوع')).toBeInTheDocument();
  });

  it('يعرض حالة فارغة للتنبيهات ولا يظهر شارة غير المقروء', async () => {
    mockApi({ reminders: [] });

    render(<Dashboard />);

    expect(await screen.findByText('لا توجد تذكيرات هذا الأسبوع')).toBeInTheDocument();
    expect(screen.getByText('تنبيهات رئيس القسم')).toBeInTheDocument();
    expect(screen.getByText('لا توجد تنبيهات حالياً')).toBeInTheDocument();
    expect(screen.queryByText(/غير مقروء/)).not.toBeInTheDocument();
  });

  it('يعرض تنبيهات رئيس القسم مع زر تمت القراءة وشارة غير المقروء', async () => {
    mockApi({ alerts: LAWYER_ALERTS, unreadCount: 1 });

    render(<Dashboard />);

    expect(await screen.findByText('تنبيهات رئيس القسم')).toBeInTheDocument();
    expect(await screen.findByText('1 غير مقروء')).toBeInTheDocument();
    expect(await screen.findByText('راجع ملف القرض')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'راجع ملف القرض' })).toHaveAttribute('href', '/documents/5');
    expect(screen.getByText('تعميم اجتماع الفرع')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تمت القراءة' })).toBeInTheDocument();
    expect(screen.getByText('مقروء')).toBeInTheDocument();
    // القائمة لا تحمل النص الكامل ولا تطلبه: لا استدعاء لتفاصيل أي تنبيه قبل فتحه.
    expect(api.get).not.toHaveBeenCalledWith('/alerts/1', expect.anything());
    expect(api.get).not.toHaveBeenCalledWith('/alerts/2', expect.anything());
  });

  it('يطلب صفحة 100 ويعرض «تحميل المزيد» عند تجاوز الإجمالي حدود الصفحة', async () => {
    mockApi({ alerts: LAWYER_ALERTS, alertsTotal: 137 });

    render(<Dashboard />);

    expect(await screen.findByText('يُعرض 2 من أصل 137 تنبيهًا')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تحميل المزيد' })).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
    );
  });

  it('«تحميل المزيد» يجلب الصفحة التالية ويُلحقها ويُخفي الزر عند اكتمال التحميل', async () => {
    const user = userEvent.setup();
    mockApi({
      alerts: LAWYER_ALERTS,
      alertsTotal: 3,
      alertsPage2: [makeHeadAlertSummary({ id: 3, messagePreview: 'تنبيه الصفحة الثانية', isRead: true })],
    });

    render(<Dashboard />);

    expect(await screen.findByText('يُعرض 2 من أصل 3 تنبيهًا')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تحميل المزيد' }));

    // الإلحاق لا الاستبدال: عناصر الصفحة الأولى باقية مع الجديدة.
    expect(await screen.findByText('تنبيه الصفحة الثانية')).toBeInTheDocument();
    expect(screen.getByText('راجع ملف القرض')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(
      '/alerts',
      expect.objectContaining({ params: { page: 2, pageSize: 100 } }),
    );
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'تحميل المزيد' })).not.toBeInTheDocument(),
    );
  });

  it('فشل تحميل القائمة يعرض «إعادة المحاولة» ويستعيد القائمة بلا كسر', async () => {
    const user = userEvent.setup();
    mockApi({ alerts: LAWYER_ALERTS, alertsFailFirst: true });

    render(<Dashboard />);

    // خطأ عام (الفشل ليس استجابة خادم) معلن بجانب القائمة، مع مخرج صريح.
    expect(await screen.findByText('حدث خطأ غير متوقع')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));

    expect(await screen.findByText('راجع ملف القرض')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'إعادة المحاولة' })).not.toBeInTheDocument();
  });

  it('فشل الصفحة التالية يُبقي المعروض ويعيد التذييل إلى «إعادة المحاولة»', async () => {
    const user = userEvent.setup();
    mockApi({
      alerts: LAWYER_ALERTS,
      alertsTotal: 3,
      alertsPage2: [makeHeadAlertSummary({ id: 3, messagePreview: 'تنبيه ثالث', isRead: true })],
      alertsFailPage: 2,
    });

    render(<Dashboard />);

    expect(await screen.findByText('يُعرض 2 من أصل 3 تنبيهًا')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تحميل المزيد' }));

    // الفشل لا يُسقط المعروض، والزر يصبح إعادة محاولة واضحة لا «تحميل المزيد».
    expect(await screen.findByText('حدث خطأ غير متوقع')).toBeInTheDocument();
    expect(screen.getByText('راجع ملف القرض')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تحميل المزيد' })).not.toBeInTheDocument();
  });

  it('يعلم المحامي التنبيه كمقروء ويخفض العداد', async () => {
    const user = userEvent.setup();
    mockApi({ alerts: LAWYER_ALERTS, unreadCount: 1 });
    (api.patch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    render(<Dashboard />);

    const button = await screen.findByRole('button', { name: 'تمت القراءة' });
    await user.click(button);

    expect(api.patch).toHaveBeenCalledWith('/alerts/1/read');
    expect((await screen.findAllByText('مقروء')).length).toBe(2);
    expect(screen.queryByText('1 غير مقروء')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تمت القراءة' })).not.toBeInTheDocument();
  });

  it('تعليم التنبيه مقروءًا يُنقص عدّاد الصف أيضًا (عدّادات PR-4 المحتفظ بها)', async () => {
    const user = userEvent.setup();
    // العدّادان المحتفظ بهما حتى `PR-6` يصلان لمنظور المستلم، فصف المحامي قد
    // يحمل «غير مقروء: N / M» — وتعليمه مقروءًا يجب أن يُنقص الرقمين معًا،
    // وإلا بقي «2 / 2» بعد قراءته حتى إعادة التركيب.
    mockApi({
      alerts: [
        makeHeadAlertSummary({
          id: 1,
          messagePreview: 'راجع ملف القرض',
          recipientCount: 2,
          unreadCount: 2,
          isRead: false,
        }),
      ],
      unreadCount: 1,
    });
    (api.patch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    render(<Dashboard />);

    expect(await screen.findByText('غير مقروء: 2 / 2')).toBeInTheDocument();
    expect(screen.getByText('1 غير مقروء')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'تمت القراءة' }));

    expect(api.patch).toHaveBeenCalledWith('/alerts/1/read');
    expect(await screen.findByText('غير مقروء: 1 / 2')).toBeInTheDocument();
    expect(screen.getByText('مقروء')).toBeInTheDocument();
    // وشارة الترويسة تنزل إلى الصفر فتختفي (لا رقم مجهول/قديم).
    expect(screen.queryByText('1 غير مقروء')).not.toBeInTheDocument();
  });
});

describe('Dashboard لرئيس القسم', () => {
  it('يعرض الترحيب وصف الأيقونات العشر وتنبيهات القسم دون إحصائيات أو قسم سجل', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({
      alerts: HEAD_ALERTS,
      lawyers: BRANCH_LAWYERS,
      pendingReviews: 2,
      urgentCount: 1,
      pendingDelegationsCount: 2,
      entityReviewCount: 1,
    });

    render(<Dashboard />);

    expect(await screen.findByRole('heading', { name: 'مرحبًا، رئيس' })).toBeInTheDocument();

    const quickNav = screen.getByRole('navigation', { name: 'أقسام لوحة رئيس القسم' });
    expect(within(quickNav).getAllByRole('link')).toHaveLength(10);
    expect(
      within(quickNav).getByRole('link', { name: 'إدارة دوائر التنفيذ' }),
    ).toHaveAttribute('href', '/execution-circuits');
    expect(within(quickNav).getByRole('link', { name: 'المطالعات — 2 كتب مطالعة بانتظار الرد' })).toHaveAttribute(
      'href',
      '/reviews',
    );
    expect(within(quickNav).getByRole('link', { name: 'المراسلات — مراسلة عاجلة واحدة' })).toHaveAttribute(
      'href',
      '/correspondence',
    );
    expect(
      within(quickNav).getByRole('link', { name: 'طلبات الإنابة — 2 طلبات إنابة معلّقة' }),
    ).toHaveAttribute('href', '/delegations/requests');
    expect(
      within(quickNav).getByRole('link', { name: 'مراجعة سجل الجهات — جهة واحدة بانتظار المراجعة' }),
    ).toHaveAttribute('href', '/entities/review');

    // لا إحصائيات ولا قسم سجل في اللوحة إطلاقًا.
    expect(screen.queryByRole('heading', { name: /متداولة ضمن/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'إجمالي الملفات' })).not.toBeInTheDocument();
    expect(screen.queryByText('إحصائيات محامي الفرع')).not.toBeInTheDocument();
    expect(screen.queryByText('مراجعة سجل الجهات العامة')).not.toBeInTheDocument();
    expect(screen.getByText('تنبيهات رئيس القسم')).toBeInTheDocument();

    expect(api.get).toHaveBeenCalledWith('/review-letters/pending-count');
    expect(api.get).toHaveBeenCalledWith('/correspondence/urgent-unseen-count');
    // الشارات الثقيلة سابقًا صارت عدّادات خفيفة — القوائم الكاملة لا تُطلب إطلاقًا.
    expect(api.get).toHaveBeenCalledWith('/delegations/pending-count');
    expect(api.get).toHaveBeenCalledWith('/entity-registry/pending-review-count');
    expect(api.get).not.toHaveBeenCalledWith('/delegations/pending', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/entity-registry/pending-review', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith(
      '/alerts',
      expect.objectContaining({ params: expect.objectContaining({ page: 1, pageSize: 100 }) }),
    );
    expect(api.get).toHaveBeenCalledWith('/users/lawyers', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/reminders', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/branches', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    // الشارة بنطاق الرئيس الصريح (`received`) — لا الافتراضي `user` ولا جمع فروعه.
    expect(api.get).toHaveBeenCalledWith(
      '/alerts/unread-count',
      expect.objectContaining({ params: { scope: 'received' } }),
    );
    expect(screen.queryByText('عدد المقترضين')).not.toBeInTheDocument();
  });

  it.each([
    { role: 'head', sectionId: undefined },
    { role: 'subhead', sectionId: 4 },
  ] as const)(
    'شارة غير المقروء لدور $role من `scope=received` وليست عدد عناصر الصفحة',
    async ({ role, sectionId }) => {
      useAuthMock.mockReturnValue({
        user: { id: 1, username: 'head1', fullName: 'رئيس', role, branchId: 1, sectionId },
      });
      mockApi({
        alerts: HEAD_ALERTS,
        alertsTotal: 250,
        unreadCount: 5,
        lawyers: BRANCH_LAWYERS,
      });

      render(<Dashboard />);

      // الشارة تجميعية غير مرقّمة (5) لا طول الصفحة (2) ولا إجمالي القائمة (250).
      expect(await screen.findByText('5 غير مقروء')).toBeInTheDocument();
      // دلالة الشارة صريحة تحتها (لا «غير مقروءة لي») — نفس نص `AccountPage`.
      expect(screen.getByText('غير المقروء في تنبيهاتك')).toBeInTheDocument();
      expect(api.get).toHaveBeenCalledWith(
        '/alerts/unread-count',
        expect.objectContaining({ params: { scope: 'received' } }),
      );
      // ولا قصّ صامت: الإجمالي يظهر في تذييل «تحميل المزيد».
      expect(await screen.findByText('يُعرض 2 من أصل 250 تنبيهًا')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'تحميل المزيد' })).toBeInTheDocument();
    },
  );

  it('يُظهر شارة معلقات الدوائر لرئيس القسم بعدّادها', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({
      alerts: HEAD_ALERTS,
      lawyers: BRANCH_LAWYERS,
      circuits: [{ id: 1, name: 'دائرة أ', pendingCount: 2 }],
    });

    render(<Dashboard />);

    const quickNav = await screen.findByRole('navigation', { name: 'أقسام لوحة رئيس القسم' });
    expect(
      within(quickNav).getByRole('link', { name: 'إدارة دوائر التنفيذ — 2 ملفات محالة' }),
    ).toHaveAttribute('href', '/execution-circuits');
    expect(api.get).toHaveBeenCalledWith('/execution-circuits/mine', expect.any(Object));
  });

  it('لا يجلب التذكيرات ولا يعرض قسمها في لوحة رئيس القسم', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: HEAD_ALERTS, lawyers: BRANCH_LAWYERS });

    render(<Dashboard />);

    expect(await screen.findByText('تنبيهات رئيس القسم')).toBeInTheDocument();
    expect(screen.queryByText('التذكيرات')).not.toBeInTheDocument();
    expect(screen.queryByText('لا توجد تذكيرات حالياً')).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith('/reminders', expect.any(Object));
  });

  it('يعرض تنبيهات الفرع مع عدادات غير مقروء', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: HEAD_ALERTS, lawyers: BRANCH_LAWYERS });

    render(<Dashboard />);

    expect(await screen.findByText('تعميم يوم الأحد')).toBeInTheDocument();
    expect(screen.getByText('غير مقروء: 2 / 2')).toBeInTheDocument();
    expect(screen.getByText('غير مقروء: 0 / 1')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'راجع ملف القرض' })).toHaveAttribute('href', '/documents/5');
    expect(screen.getAllByText('مرتبط بملف').length).toBeGreaterThan(0);
    expect(screen.getAllByText('تعميم للفرع').length).toBeGreaterThan(0);
  });

  it('إزاحة الترقيم بين صفحتين: يتوقف «المزيد» ويظهر «تحديث» بلا طلبات صفحات فارغة', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({
      alerts: HEAD_ALERTS,
      alertsTotal: 3,
      // الإزاحة: الصف الثاني من الصفحة الأولى يعود وحده في الصفحة الثانية.
      alertsPage2: [makeHeadAlertSummary({ id: 1, messagePreview: 'راجع ملف القرض', documentId: 5 })],
      lawyers: BRANCH_LAWYERS,
    });

    render(<Dashboard />);

    expect(await screen.findByText('يُعرض 2 من أصل 3 تنبيهًا')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تحميل المزيد' }));

    // لا وعد ترقيم لا يُغلق: الانقطاع معلن والمخرج «تحديث» بدل صفحات فارغة.
    expect(
      await screen.findByText(/تغيّرت القائمة أثناء التصفّح فتعذّر تحميل البقية/),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تحميل المزيد' })).not.toBeInTheDocument();
    const alertPages = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls
      .filter((call) => call[0] === '/alerts')
      .map((call) => (call[1] as { params?: { page?: number } } | undefined)?.params?.page);
    expect(alertPages).toEqual([1, 2]);

    // «تحديث» يعيد من الصفحة الأولى فيستعيد المتابعة الطبيعية.
    await user.click(screen.getByRole('button', { name: 'تحديث' }));
    expect(await screen.findByRole('button', { name: 'تحميل المزيد' })).toBeInTheDocument();
    expect(screen.queryByText(/تغيّرت القائمة أثناء التصفّح/)).not.toBeInTheDocument();
  });

  it('ربط التبويب بلوحته: `aria-controls` يشير إلى لوحة موجودة بدورها واسمها', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: HEAD_ALERTS, lawyers: BRANCH_LAWYERS });

    render(<Dashboard />);

    const receivedTab = await screen.findByRole('tab', { name: 'مستلمة' });
    expect(receivedTab).toHaveAttribute('id', 'alerts-tab-received');
    expect(receivedTab).toHaveAttribute('aria-controls', 'alerts-panel-received');

    const receivedPanel = document.getElementById('alerts-panel-received');
    expect(receivedPanel).toHaveAttribute('role', 'tabpanel');
    expect(receivedPanel).toHaveAttribute('aria-labelledby', 'alerts-tab-received');

    await user.click(screen.getByRole('tab', { name: 'أرسلتها' }));

    const sentTab = document.getElementById('alerts-tab-sent');
    expect(sentTab).toHaveAttribute('aria-controls', 'alerts-panel-sent');
    const sentPanel = document.getElementById('alerts-panel-sent');
    expect(sentPanel).toHaveAttribute('role', 'tabpanel');
    expect(sentPanel).toHaveAttribute('aria-labelledby', 'alerts-tab-sent');
    // اللوحة الحاوية تتبع التبويب الظاهر وحده (لا لوحة معلّقة للتبويب السابق).
    expect(document.getElementById('alerts-panel-received')).toBeNull();
  });

  it('يصدر تعميماً للفرع فيضاف التنبيه للقائمة', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: HEAD_ALERTS, lawyers: BRANCH_LAWYERS });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        id: 9,
        message: 'اجتماع الفرع يوم الأحد',
        targetType: 'branch',
        recipientCount: 2,
        unreadCount: 2,
        createdAt: '2026-08-03T10:00:00Z',
        createdByName: 'رئيس القسم',
      },
    });

    render(<Dashboard />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));

    const textarea = await screen.findByLabelText('نص التنبيه');
    await user.type(textarea, 'اجتماع الفرع يوم الأحد');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    expect(api.post).toHaveBeenCalledWith('/alerts', {
      targetType: 'branch',
      documentId: null,
      targetLawyerId: null,
      message: 'اجتماع الفرع يوم الأحد',
    });
    // بلا إلحاق تفاؤلي: رسالة نجاح + النموذج مغلق + المُرسَل في تبويب «أرسلتها» لا المستلَمات.
    expect(await screen.findByRole('status')).toHaveTextContent(/تم إصدار التنبيه/);
    expect(screen.queryByLabelText('نص التنبيه')).not.toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/alerts/sent', expect.any(Object));
  });

  it('إصدار تنبيه بلا نص يعرض خطأ ولا يرسل', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: [], lawyers: BRANCH_LAWYERS });

    render(<Dashboard />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    // الخطأ منسوب لحقل النص وحده: معلن ومرتبط به ومركّز عليه، ولا تعليم لحقل آخر.
    const error = await screen.findByRole('alert');
    expect(error).toHaveTextContent('نص التنبيه مطلوب');
    const textarea = screen.getByLabelText('نص التنبيه');
    expect(textarea).toHaveAttribute('aria-invalid', 'true');
    expect(textarea).toHaveAttribute('aria-describedby', 'alert-form-error');
    expect(document.activeElement).toBe(textarea);
    expect(api.post).not.toHaveBeenCalled();
  });

  it('إصدار رسالة لمحامٍ بلا اختيار يعزو الخطأ للقائمة وحدها', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: [], lawyers: BRANCH_LAWYERS });

    render(<Dashboard />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));
    await user.click(screen.getByRole('button', { name: 'رسالة لمحامٍ' }));
    await user.type(await screen.findByLabelText('نص التنبيه'), 'رسالة خاصة');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    const error = await screen.findByRole('alert');
    expect(error).toHaveTextContent('اختر المحامي المستلم');
    const select = screen.getByLabelText('المحامي');
    expect(select).toHaveAttribute('aria-invalid', 'true');
    expect(select).toHaveAttribute('aria-describedby', 'alert-form-error');
    expect(document.activeElement).toBe(select);
    // حقل النص السليم لا يُعلَّم بخطأ غيره.
    expect(screen.getByLabelText('نص التنبيه')).not.toHaveAttribute('aria-invalid');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('فشل إرسال التنبيه خادميًا يُعلن الخطأ دون تعليم أي حقل', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: [], lawyers: BRANCH_LAWYERS });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('عطل'));

    render(<Dashboard />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));
    await user.type(await screen.findByLabelText('نص التنبيه'), 'اجتماع الفرع');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    // خطأ بلا حقل مخالف (`field: null`): معلن وحده، ولا `aria-invalid` على أي حقل.
    const error = await screen.findByRole('alert');
    expect(error).toHaveTextContent('حدث خطأ غير متوقع');
    expect(screen.getByLabelText('نص التنبيه')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByLabelText('نص التنبيه')).not.toHaveAttribute('aria-describedby');
  });

  it('اختيار «رسالة لمحامٍ» يعرض قائمة محامي الفرع ويرسلها', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: [], lawyers: BRANCH_LAWYERS });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        id: 10,
        message: 'رسالة خاصة',
        targetType: 'lawyer',
        targetLawyerId: 2,
        targetLawyerName: 'محامي دمشق',
        createdAt: '2026-08-03T10:00:00Z',
        createdByName: 'رئيس القسم',
      },
    });

    render(<Dashboard />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));
    await user.click(screen.getByRole('button', { name: 'رسالة لمحامٍ' }));

    const select = await screen.findByLabelText('المحامي');
    await user.selectOptions(select, '2');
    await user.type(await screen.findByLabelText('نص التنبيه'), 'رسالة خاصة');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    expect(api.post).toHaveBeenCalledWith('/alerts', {
      targetType: 'lawyer',
      documentId: null,
      targetLawyerId: 2,
      message: 'رسالة خاصة',
    });
    expect(await screen.findByRole('status')).toHaveTextContent(/تم إصدار التنبيه/);
  });

  it('تبويبا المستلمة/أرسلتها: المُرسَل لا يظهر في المستلمات ويظهر في الصادر بلا زر قراءة', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({
      alerts: [
        makeHeadAlertSummary({
          id: 1,
          kind: 'delegation_pending_approval',
          messagePreview: 'وارد من النظام',
          targetType: 'head',
          recipientCount: 1,
          unreadCount: 1,
          createdAt: '2026-08-03T10:00:00Z',
          createdByName: 'النظام',
        }),
      ],
      lawyers: BRANCH_LAWYERS,
      sent: [
        makeHeadAlertSummary({
          id: 9,
          kind: 'manual_to_branch',
          messagePreview: 'تعميم صادر مني',
          recipientCount: 2,
          unreadCount: 0,
          createdAt: '2026-08-04T10:00:00Z',
          createdByName: 'رئيس',
        }),
      ],
      // إجمالي الصادر أكبر من عناصر الصفحة ⇒ سطر «المزيد» يتبع التبويب الظاهر.
      sentTotal: 3,
    });

    render(<Dashboard />);

    expect(await screen.findByRole('tab', { name: 'مستلمة' })).toHaveAttribute('aria-selected', 'true');
    expect(await screen.findByText('وارد من النظام')).toBeInTheDocument();
    expect(screen.queryByText('تعميم صادر مني')).not.toBeInTheDocument();
    // الشارة في تبويب «مستلمة» بدلالتها الصريحة (`received`).
    expect(screen.getByText('غير المقروء في تنبيهاتك')).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'أرسلتها' }));
    expect(await screen.findByText('تعميم صادر مني')).toBeInTheDocument();
    expect(screen.queryByText('وارد من النظام')).not.toBeInTheDocument();
    // الدلالة التوضيحية تخص «مستلمة» وحدها — لا تُعرض مع تبويب الصادر.
    expect(screen.queryByText('غير المقروء في تنبيهاتك')).not.toBeInTheDocument();
    // الصادر قراءة فقط: لا زر «تمت القراءة» (تعليمه 404 خلفيًا).
    expect(screen.queryByRole('button', { name: 'تمت القراءة' })).not.toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(
      '/alerts/sent',
      expect.objectContaining({ params: expect.objectContaining({ page: 1, pageSize: 100 }) }),
    );
    // تذييل «تحميل المزيد» يتبع إجمالي التبويب الظاهر (3 صادر) لا إجمالي المستلمات.
    expect(screen.getByText('يُعرض 1 من أصل 3 تنبيهًا')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تحميل المزيد' })).toBeInTheDocument();
    // الرقم وحده بلا سياق للقارئ الصوتي: يسبقه نص مخفي يسمّيه.
    expect(screen.getByText('إجمالي الصادر:')).toBeInTheDocument();
    // الكل قرأ: شارة صريحة بدل العدّاد الصفري المضلل.
    expect(screen.getByText('مقروء من الجميع')).toBeInTheDocument();
  });

  it('تعذّر الصادر: لا شارة «0» كاذبة بل خطأ ظاهر مع «إعادة المحاولة»', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: HEAD_ALERTS, lawyers: BRANCH_LAWYERS, sentFailFirst: true });

    render(<Dashboard />);

    await user.click(await screen.findByRole('tab', { name: 'أرسلتها' }));

    expect(await screen.findByText('حدث خطأ غير متوقع')).toBeInTheDocument();
    // الإجمالي مجهول ⇒ لا شارة رقمية (0) في الترويسة، ومسار استعادة صريح.
    expect(screen.queryByText('إجمالي الصادر:')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
  });

  it('لا يعرض خيار «مرتبط بملف» في نموذج إصدار التنبيه', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: [], lawyers: BRANCH_LAWYERS });

    render(<Dashboard />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));

    expect(screen.getByRole('button', { name: 'رسالة لمحامٍ' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تعميم للفرع' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'مرتبط بملف' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('الملف')).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith(
      '/documents',
      expect.objectContaining({ params: expect.objectContaining({ perPage: 100 }) }),
    );
  });

  it('رئيس بلا فرع: حالة صريحة قابلة للمعالجة ولا طلبات تنبيهات أو عدّادات', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: null },
    });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/users/lawyers') return Promise.resolve({ data: [] });
      return Promise.reject(new Error('400 رئيس القسم دون فرع'));
    });

    render(<Dashboard />);

    expect(await screen.findByRole('heading', { name: 'مرحبًا، رئيس' })).toBeInTheDocument();
    // الشارات صفر بلا كسر — الأسماء المجردة فقط، ونقاط العدّ لا تُستدعى أصلًا.
    expect(api.get).not.toHaveBeenCalledWith('/review-letters/pending-count', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/correspondence/urgent-unseen-count', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/delegations/pending-count', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/entity-registry/pending-review-count', expect.any(Object));
    // النطاق مرفوض خلفيًا (`403` — `D11`): لا طلب قائمة ولا صادر ولا عدّاد أصلًا.
    expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/alerts/sent', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/alerts/unread-count', expect.any(Object));
    // لا قائمة فارغة كاذبة: حالة صريحة تقول السبب والعلاج.
    expect(
      screen.getByText(
        'حسابك غير مرتبط بفرع، فلا يمكن عرض تنبيهات القسم. راجع مدير النظام لربط حسابك بفرع.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'تنبيهات رئيس القسم' })).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(screen.getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(screen.getByRole('link', { name: 'طلبات الإنابة' })).toHaveAttribute(
      'href',
      '/delegations/requests',
    );
    expect(screen.getByRole('link', { name: 'مراجعة سجل الجهات' })).toHaveAttribute(
      'href',
      '/entities/review',
    );
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers', expect.any(Object));
  });
});

describe('Dashboard للمدير/المشرف', () => {
  it('يعرض للمدير ترحيبًا وصف أيقونات (10 بطاقات) بلا إحصائيات مباشرة في اللوحة', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByRole('heading', { name: 'مرحبًا، مدير' })).toBeInTheDocument();

    const quickNav = screen.getByRole('navigation', { name: 'أقسام لوحة المدير' });
    expect(within(quickNav).getAllByRole('link')).toHaveLength(10);
    expect(within(quickNav).getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(within(quickNav).getByRole('link', { name: 'كتب المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(within(quickNav).getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(within(quickNav).getByRole('link', { name: 'اقتراحات التطوير' })).toHaveAttribute(
      'href',
      '/suggestions',
    );
    expect(within(quickNav).getByRole('link', { name: 'مراجعة سجل الجهات العامة' })).toHaveAttribute(
      'href',
      '/entities/review-management',
    );
    expect(within(quickNav).getByRole('link', { name: 'مندوبو الجهات' })).toHaveAttribute(
      'href',
      '/delegates',
    );
    expect(within(quickNav).getByRole('link', { name: 'إدارة المستخدمين' })).toHaveAttribute(
      'href',
      '/users/manage',
    );
    expect(within(quickNav).getByRole('link', { name: 'إدارة الفروع' })).toHaveAttribute(
      'href',
      '/branches/manage',
    );
    expect(within(quickNav).getByRole('link', { name: 'سجل التدقيق' })).toHaveAttribute(
      'href',
      '/audit-logs',
    );
    expect(within(quickNav).getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute(
      'href',
      '/account',
    );
    // المدير بلا بطاقة «محامو الفرع» (للمشرف وحده).
    expect(within(quickNav).queryByRole('link', { name: 'محامو الفرع' })).not.toBeInTheDocument();

    // لا إحصائيات مباشرة في اللوحة — تُفتح من بطاقة الإحصائيات في `/stats`.
    expect(screen.queryByRole('heading', { name: /متداولة ضمن/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'إجمالي الملفات' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'شهري' })).not.toBeInTheDocument();

    // اللوحة لا تجلب الإحصائيات ولا الفروع ولا التنبيهات.
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/branches', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/reminders', expect.any(Object));
  });

  it('يعرض للمشرف 11 بطاقة مع «محامو الفرع» و«الحساب الشخصي»', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'admin1', fullName: 'مشرف', role: 'admin', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByRole('heading', { name: 'مرحبًا، مشرف' })).toBeInTheDocument();

    const quickNav = screen.getByRole('navigation', { name: 'أقسام لوحة مشرف النظام' });
    expect(within(quickNav).getAllByRole('link')).toHaveLength(11);
    expect(within(quickNav).getByRole('link', { name: 'محامو الفرع' })).toHaveAttribute(
      'href',
      '/branch-lawyers',
    );
    expect(within(quickNav).getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute(
      'href',
      '/account',
    );
    expect(within(quickNav).getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager', expect.any(Object));
  });

  it.each(['manager', 'admin'] as const)(
    'لا يعرض لوحة «تنبيهات رئيس القسم» ولا زر إصدار تنبيه لدور %s (خارج نطاق التنبيهات خلفيًا)',
    async (role) => {
      useAuthMock.mockReturnValue({
        user: { id: 1, username: `${role}1`, fullName: 'مدير', role, branchId: null },
      });
      mockApi();

      render(<Dashboard />);

      // انتظر اكتمال صف الأيقونات أولًا — الغياب قبل الاكتمال قد يكون تحميلًا لا منعًا.
      expect(await screen.findByRole('navigation', { name: /أقسام لوحة/ })).toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'تنبيهات رئيس القسم' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: '+ إصدار تنبيه' })).not.toBeInTheDocument();
      expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
    },
  );
});
