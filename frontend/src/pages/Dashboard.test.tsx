import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import Dashboard from './Dashboard';
import type {
  DashboardStatsDto,
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
  /** إجمالي غير المقروء في التنبيهات (`scope` حسب الدور في الطلب نفسه). */
  unreadCount?: number;
  lawyers?: Array<{ id: number; fullName: string; isActive?: boolean }>;
  personal?: PersonalReminderDto[];
  pendingReviews?: number;
  urgentCount?: number;
  pendingDelegationsCount?: number;
  entityReviewCount?: number;
  circuits?: Array<{ id: number; name: string; pendingCount: number }>;
  pendingRegistrations?: Array<{ documentId: number }>;
}) {
  const reminders = overrides?.reminders ?? [];
  const monthly = overrides?.monthly ?? [];
  const unreadCount = overrides?.unreadCount ?? 0;
  const lawyers = overrides?.lawyers ?? [];
  const personal = overrides?.personal ?? [];
  const pendingReviews = overrides?.pendingReviews ?? 0;
  const urgentCount = overrides?.urgentCount ?? 0;
  const pendingDelegationsCount = overrides?.pendingDelegationsCount ?? 0;
  const entityReviewCount = overrides?.entityReviewCount ?? 0;
  const circuits = overrides?.circuits ?? [];
  const pendingRegistrations = overrides?.pendingRegistrations ?? [];
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation(
    (url: string, config?: { params?: Record<string, unknown> }) => {
      const params = config?.params ?? {};
      if (url === '/dashboard') return Promise.resolve({ data: STATS });
      if (url === '/reminders') return Promise.resolve({ data: reminders });
      if (url === '/appeals/reminders') return Promise.resolve({ data: [] });
      if (url === '/personal-reminders') return Promise.resolve({ data: personal });
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
        const period = typeof params.period === 'string' ? params.period : 'yearly';
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
    // اللوحة لا تُنزل قائمة تنبيهات: بطاقة «التنبيهات» تفتح `/alerts`، والشارة وحدها
    // تُقرأ من العدّاد الخفيف (بلا قصّ ولا صفحة تُجلب بلا سبب).
    expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/alerts/sent', expect.any(Object));
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

});

describe('Dashboard لرئيس القسم', () => {
  it('يعرض الترحيب وصف الأيقونات العشر وتنبيهات القسم دون إحصائيات أو قسم سجل', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({
      lawyers: BRANCH_LAWYERS,
      pendingReviews: 2,
      urgentCount: 1,
      pendingDelegationsCount: 2,
      entityReviewCount: 1,
    });

    render(<Dashboard />);

    expect(await screen.findByRole('heading', { name: 'مرحبًا، رئيس' })).toBeInTheDocument();

    const quickNav = screen.getByRole('navigation', { name: 'أقسام لوحة رئيس القسم' });
    expect(within(quickNav).getAllByRole('link')).toHaveLength(11);
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

    expect(api.get).toHaveBeenCalledWith('/review-letters/pending-count');
    expect(api.get).toHaveBeenCalledWith('/correspondence/urgent-unseen-count');
    // الشارات الثقيلة سابقًا صارت عدّادات خفيفة — القوائم الكاملة لا تُطلب إطلاقًا.
    expect(api.get).toHaveBeenCalledWith('/delegations/pending-count');
    expect(api.get).toHaveBeenCalledWith('/entity-registry/pending-review-count');
    expect(api.get).not.toHaveBeenCalledWith('/delegations/pending', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/entity-registry/pending-review', expect.any(Object));
    // لا قائمة تنبيهات ولا قائمة صادر في اللوحة: الصفحة `/alerts` هي السطح الوحيد.
    expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/alerts/sent', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/users/lawyers', expect.any(Object));
    // لكن البطاقة تعرض الشارة من العدّاد الخفيف بنطاقها.
    expect(
      within(quickNav).getByRole('link', { name: /التنبيهات/ }),
    ).toHaveAttribute('href', '/alerts');
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/reminders', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/branches', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    // الشارة بالنطاق الشخصي (`user`) لكل الأدوار — «غير المقروء لي» (حُسم 2026-11-02).
    expect(api.get).toHaveBeenCalledWith(
      '/alerts/unread-count',
      expect.objectContaining({ params: { scope: 'user' } }),
    );
    expect(screen.queryByText('عدد المقترضين')).not.toBeInTheDocument();
  });

  it.each([
    { role: 'head', sectionId: undefined },
    { role: 'subhead', sectionId: 4 },
  ] as const)(
    'شارة غير المقروء لدور $role من `scope=user` وليست عدد عناصر الصفحة',
    async ({ role, sectionId }) => {
      useAuthMock.mockReturnValue({
        user: { id: 1, username: 'head1', fullName: 'رئيس', role, branchId: 1, sectionId },
      });
      mockApi({
        unreadCount: 5,
        lawyers: BRANCH_LAWYERS,
      });

      render(<Dashboard />);

      const quickNav = await screen.findByRole('navigation', {
        name: role === 'subhead' ? 'أقسام لوحة رئيس الشعبة' : 'أقسام لوحة رئيس القسم',
      });
      // الشارة من العدّاد الخفيف (5) لا من طول أي صفحة، وبالنطاق الشخصي `user`:
      // «غير المقروء لي» — لا `received` التجميعي الذي أبقى رقمًا بعد قراءة الكل.
      expect(
        within(quickNav).getByRole('link', { name: 'التنبيهات — 5 تنبيهات غير مقروءة' }),
      ).toHaveAttribute('href', '/alerts');
      expect(api.get).toHaveBeenCalledWith(
        '/alerts/unread-count',
        expect.objectContaining({ params: { scope: 'user' } }),
      );
      // ولا تُنزل اللوحة قائمة تنبيهات أصلًا (لا قصّ صامت ولا ترقيم مزدوج).
      expect(api.get).not.toHaveBeenCalledWith('/alerts', expect.any(Object));
      expect(api.get).not.toHaveBeenCalledWith('/alerts/sent', expect.any(Object));
    },
  );

  it('يُظهر شارة معلقات الدوائر لرئيس القسم بعدّادها', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({
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

    render(<Dashboard />);

    expect(await screen.findByRole('heading', { name: 'مرحبًا، رئيس' })).toBeInTheDocument();
    expect(screen.queryByText('التذكيرات')).not.toBeInTheDocument();
    expect(screen.queryByText('لا توجد تذكيرات حالياً')).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith('/reminders', expect.any(Object));
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
    // البطاقة تبقى مدخلًا للصفحة، والصفحة نفسها تعرض الحالة الصريحة («حسابك غير مرتبط بفرع»)
    // — فلا تُعرض قائمة فارغة كاذبة ولا خطأ في اللوحة.
    expect(screen.getByRole('link', { name: 'التنبيهات' })).toHaveAttribute('href', '/alerts');
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
