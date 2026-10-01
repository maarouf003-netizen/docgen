import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import Dashboard from './Dashboard';
import type {
  DashboardStatsDto,
  HeadAlertDto,
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

const LAWYER_ALERTS: HeadAlertDto[] = [
  {
    id: 1,
    message: 'راجع ملف القرض',
    targetType: 'document',
    documentId: 5,
    isRead: false,
    createdAt: '2026-08-01T10:00:00Z',
    createdByName: 'رئيس القسم',
  },
  {
    id: 2,
    message: 'تعميم اجتماع الفرع',
    targetType: 'branch',
    isRead: true,
    createdAt: '2026-07-20T10:00:00Z',
    createdByName: 'رئيس القسم',
  },
];

const HEAD_ALERTS: HeadAlertDto[] = [
  {
    id: 3,
    message: 'تعميم يوم الأحد',
    targetType: 'branch',
    recipientCount: 2,
    unreadCount: 2,
    createdAt: '2026-08-02T10:00:00Z',
    createdByName: 'رئيس القسم',
  },
  {
    id: 1,
    message: 'راجع ملف القرض',
    targetType: 'document',
    documentId: 5,
    recipientCount: 1,
    unreadCount: 0,
    createdAt: '2026-08-01T10:00:00Z',
    createdByName: 'رئيس القسم',
  },
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

function mockApi(overrides?: {
  reminders?: ReminderDto[];
  monthly?: [];
  alerts?: HeadAlertDto[];
  unreadCount?: number;
  lawyers?: LawyerListItem[];
  personal?: PersonalReminderDto[];
  pendingReviews?: number;
  urgentCount?: number;
  pendingDelegationsCount?: number;
  entityReviewCount?: number;
}) {
  const reminders = overrides?.reminders ?? [];
  const monthly = overrides?.monthly ?? [];
  const alerts = overrides?.alerts ?? [];
  const unreadCount = overrides?.unreadCount ?? 0;
  const lawyers = overrides?.lawyers ?? [];
  const personal = overrides?.personal ?? [];
  const pendingReviews = overrides?.pendingReviews ?? 0;
  const urgentCount = overrides?.urgentCount ?? 0;
  const pendingDelegationsCount = overrides?.pendingDelegationsCount ?? 0;
  const entityReviewCount = overrides?.entityReviewCount ?? 0;
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation(
    (url: string, config?: { params?: Record<string, unknown> }) => {
      if (url === '/dashboard') return Promise.resolve({ data: STATS });
      if (url === '/reminders') return Promise.resolve({ data: reminders });
      if (url === '/appeals/reminders') return Promise.resolve({ data: [] });
      if (url === '/personal-reminders') return Promise.resolve({ data: personal });
      if (url === '/alerts') return Promise.resolve({ data: alerts });
      if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: unreadCount } });
      if (url === '/users/lawyers') return Promise.resolve({ data: lawyers });
      if (url === '/monthly-stats') return Promise.resolve({ data: monthly });
      if (url === '/review-letters/pending-count') return Promise.resolve({ data: { count: pendingReviews } });
      if (url === '/correspondence/urgent-unseen-count') return Promise.resolve({ data: { count: urgentCount } });
      if (url === '/delegations/pending-count') return Promise.resolve({ data: { count: pendingDelegationsCount } });
      if (url === '/entity-registry/pending-review-count') return Promise.resolve({ data: { count: entityReviewCount } });
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

    // صف الأيقونات الخمس بروابط الصفحات.
    const quickNav = screen.getByRole('navigation', { name: 'أقسام لوحة المحامي' });
    expect(within(quickNav).getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(within(quickNav).getByRole('link', { name: 'المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(within(quickNav).getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(within(quickNav).getByRole('link', { name: 'التقويم' })).toHaveAttribute('href', '/calendar');
    expect(within(quickNav).getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute('href', '/account');

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
    expect(api.get).toHaveBeenCalledWith('/alerts', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/alerts/unread-count', expect.any(Object));
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
    expect(screen.getByText('1 غير مقروء')).toBeInTheDocument();
    expect(screen.getByText('راجع ملف القرض')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'راجع ملف القرض' })).toHaveAttribute('href', '/documents/5');
    expect(screen.getByText('تعميم اجتماع الفرع')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تمت القراءة' })).toBeInTheDocument();
    expect(screen.getByText('مقروء')).toBeInTheDocument();
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
});

describe('Dashboard لرئيس القسم', () => {
  it('يعرض الترحيب وصف الأيقونات التسع وتنبيهات القسم دون إحصائيات أو قسم سجل', async () => {
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
    expect(within(quickNav).getAllByRole('link')).toHaveLength(9);
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
    expect(api.get).toHaveBeenCalledWith('/alerts', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/users/lawyers', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/reminders', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/branches', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    expect(api.get).not.toHaveBeenCalledWith('/alerts/unread-count', expect.any(Object));
    expect(screen.queryByText('عدد المقترضين')).not.toBeInTheDocument();
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
    expect(await screen.findByText('اجتماع الفرع يوم الأحد')).toBeInTheDocument();
    expect(screen.queryByLabelText('نص التنبيه')).not.toBeInTheDocument();
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
    expect(await screen.findByText('رسالة خاصة')).toBeInTheDocument();
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

  it('رئيس بلا فرع: الشارات صفر بلا كسر ولا طلبات عدّادات أو إحصائيات', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: null },
    });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/alerts') return Promise.resolve({ data: [] });
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
    expect(screen.getByText('تنبيهات رئيس القسم')).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers', expect.any(Object));
  });
});

describe('Dashboard للمدير/المشرف', () => {
  it('يعرض بطاقات إحصاءات المدير الجديدة (بطل + مؤشرات) ومحدد الفترة والفرع بلا روابط تعمّق', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByRole('heading', { name: /متداولة ضمن/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'إجمالي الملفات' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'تحت رفع' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'تريث' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'منفذ' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'محال الى البداية' })).toBeInTheDocument();
    // بلا روابط تعمّق للمدير.
    expect(screen.queryByRole('link', { name: 'عرض الملفات ←' })).not.toBeInTheDocument();
    // الرسم المصغّر للمسجَّلة شهريًا حاضر بوصفه.
    expect(screen.getByText(/الملفات المسجَّلة شهريًا/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'شهري' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ربعي' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'سنوي' })).toBeInTheDocument();
    expect(await screen.findAllByText('السنة 2026')).not.toHaveLength(0);
    // بلا فرع مختار: رسالة اختيار الفرع بدل الجدول.
    expect(screen.getByText('اختر فرعًا لعرض إحصائيات محامي الفرع')).toBeInTheDocument();

    expect(api.get).toHaveBeenCalledWith('/branches', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith(
      '/stats/manager',
      expect.objectContaining({ params: expect.objectContaining({ period: 'yearly' }) }),
    );
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers');
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    expect(api.get).not.toHaveBeenCalledWith('/monthly-stats');
    expect(api.get).not.toHaveBeenCalledWith('/reminders', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/users/lawyers', expect.any(Object));
    expect(screen.queryByText('المستندات شهرياً')).not.toBeInTheDocument();
    expect(screen.queryByText('عدد المقترضين')).not.toBeInTheDocument();
    expect(screen.getByText(/عرض الفترة/)).toBeInTheDocument();
  });

  it('يجمع إحصائيات المنفذ في بطاقة واحدة: للصالح (بالتسوية + جبريا + عرض وايداع) وللضد بعدد الملفات والمبالغ', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    await screen.findByRole('heading', { name: 'منفذ' });

    const card = screen.getByRole('heading', { name: 'منفذ' }).closest('article') as HTMLElement;
    expect(card).toBeTruthy();

    expect(within(card).getByText('5')).toBeInTheDocument();
    await user.click(within(card).getByRole('button', { name: 'عرض التفاصيل' }));
    expect(within(card).getByText('منفذ للصالح')).toBeInTheDocument();
    expect(within(card).getByText('منفذ بالتسوية')).toBeInTheDocument();
    expect(within(card).getByText('منفذ جبريا')).toBeInTheDocument();
    expect(within(card).getByText('عرض وايداع')).toBeInTheDocument();
    expect(within(card).getByText('منفذ للضد')).toBeInTheDocument();
    expect(within(card).getByText('1,500 ل.س')).toBeInTheDocument();
    expect(within(card).getByText('500 ل.س')).toBeInTheDocument();
    expect(within(card).getByText('750 ل.س')).toBeInTheDocument();
    expect(within(card).getByText('0 ل.س')).toBeInTheDocument();
    expect(screen.queryByText('المبلغ المحصل')).not.toBeInTheDocument();
  });

  it('يثبّت عداد الملفات يسارًا بأرقام جدولية في جميع بطاقات الإحصائيات', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    await screen.findByRole('heading', { name: 'إجمالي الملفات' });

    const titles = [/متداولة ضمن/, 'إجمالي الملفات', 'تحت رفع', 'منفذ', 'تريث', 'محال الى البداية'];
    expect(titles).toHaveLength(6);
    for (const name of titles) {
      const card = screen.getByRole('heading', { name }).closest('article') as HTMLElement;
      expect(card).toBeTruthy();
      const value = card.querySelector('[dir="ltr"].tabular-nums');
      expect(value).not.toBeNull();
    }
  });

  it('يعرض ملفات «عرض وايداع» المتداولة كسطر فرعي داخل بطاقة البطل', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    await screen.findByRole('heading', { name: /متداولة ضمن/ });

    const card = screen.getByRole('heading', { name: /متداولة ضمن/ }).closest('article') as HTMLElement;
    expect(card).toBeTruthy();

    await user.click(within(card).getByRole('button', { name: 'عرض التفاصيل' }));
    expect(within(card).getByText('متداول للصالح')).toBeInTheDocument();
    expect(within(card).getByText('عرض وايداع')).toBeInTheDocument();
    expect(within(card).getByText('متداول للضد')).toBeInTheDocument();
  });

  it('تغيير الفترة يعيد الجلب بالفترة الجديدة', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    const user = userEvent.setup();
    mockApi();

    render(<Dashboard />);

    await user.click(await screen.findByRole('button', { name: 'ربعي' }));

    expect(api.get).toHaveBeenCalledWith(
      '/stats/manager',
      expect.objectContaining({ params: expect.objectContaining({ period: 'quarterly' }) }),
    );
    expect((await screen.findAllByText('الربع الثالث 2026')).length).toBeGreaterThanOrEqual(1);
  });

  it('اختيار فرع يجلب جدول محامي الفرع ويعرضه', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    const user = userEvent.setup();
    mockApi();

    render(<Dashboard />);

    const select = await screen.findByLabelText('الفرع');
    await user.selectOptions(select, '1');

    expect(api.get).toHaveBeenCalledWith(
      '/stats/manager/lawyers',
      expect.objectContaining({ params: expect.objectContaining({ branchId: 1, period: 'yearly' }) }),
    );
    expect(await screen.findByText('إحصائيات محامي الفرع')).toBeInTheDocument();
    expect(screen.getByText('محامي دمشق')).toBeInTheDocument();
  });

  it('المشرف يرى إحصاءات المدير أيضًا', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'admin1', fullName: 'مشرف', role: 'admin', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByText('إجمالي الملفات')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/stats/manager', expect.any(Object));
  });

  it.each(['manager', 'admin'] as const)(
    'لا يعرض لوحة «تنبيهات رئيس القسم» ولا زر إصدار تنبيه لدور %s (خارج نطاق التنبيهات خلفيًا)',
    async (role) => {
      useAuthMock.mockReturnValue({
        user: { id: 1, username: `${role}1`, fullName: 'مدير', role, branchId: null },
      });
      mockApi();

      render(<Dashboard />);

      // انتظر اكتمال لوحة المدير أولًا — الغياب قبل الاكتمال قد يكون تحميلًا لا منعًا.
      expect(await screen.findByText('إجمالي الملفات')).toBeInTheDocument();
      expect(screen.queryByRole('heading', { name: 'تنبيهات رئيس القسم' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: '+ إصدار تنبيه' })).not.toBeInTheDocument();
      expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
    },
  );
});
