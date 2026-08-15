import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
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
  ReminderDto,
} from '../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}));

import { api } from '../api/client';

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
  tradingAgainstAmounts: [],
  periodYear: 2026,
  periodQuarter: null,
  periodMonth: 8,
};

const MANAGER_LAWYERS: ManagerLawyerStatDto[] = [
  { lawyerId: 1, lawyerName: 'محامي دمشق', totalCount: 3, points: [{ year: 2026, month: 8, count: 3 }] },
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
}) {
  const reminders = overrides?.reminders ?? [];
  const monthly = overrides?.monthly ?? [];
  const alerts = overrides?.alerts ?? [];
  const unreadCount = overrides?.unreadCount ?? 0;
  const lawyers = overrides?.lawyers ?? [];
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation(
    (url: string, config?: { params?: Record<string, unknown> }) => {
      if (url === '/dashboard') return Promise.resolve({ data: STATS });
      if (url === '/reminders') return Promise.resolve({ data: reminders });
      if (url === '/alerts') return Promise.resolve({ data: alerts });
      if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: unreadCount } });
      if (url === '/users/lawyers') return Promise.resolve({ data: lawyers });
      if (url === '/monthly-stats') return Promise.resolve({ data: monthly });
      if (url === '/stats/periods') return Promise.resolve({ data: PERIODS });
      if (url === '/stats/me') {
        const period = typeof config?.params?.period === 'string' ? config.params.period : 'yearly';
        return Promise.resolve({ data: managerStatsFor(period) });
      }
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
  useAuthMock.mockReturnValue({
    user: { id: 1, username: 'lawyer1', fullName: 'محامي', role: 'lawyer', branchId: 1 },
  });
});

describe('Dashboard للمحامي', () => {
  it('يعرض بطاقات إحصاءاته الشخصية ويجلب التذكيرات والتنبيهات', async () => {
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByText('إجمالي الملفات')).toBeInTheDocument();
    expect(screen.getByText('متداول')).toBeInTheDocument();
    expect(screen.getByText('منفذ بالتسوية')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'شهري' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ربعي' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'سنوي' })).toBeInTheDocument();
    expect(await screen.findAllByText('السنة 2026')).not.toHaveLength(0);

    expect(api.get).toHaveBeenCalledWith('/stats/me', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/reminders');
    expect(api.get).toHaveBeenCalledWith('/alerts');
    expect(api.get).toHaveBeenCalledWith('/alerts/unread-count');
    expect(api.get).not.toHaveBeenCalledWith('/users/lawyers');
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    expect(api.get).not.toHaveBeenCalledWith('/monthly-stats');
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager');
    expect(api.get).not.toHaveBeenCalledWith('/branches');
    expect(screen.queryByText('المستندات شهرياً')).not.toBeInTheDocument();
    expect(screen.queryByText('إحصائيات محامي الفرع')).not.toBeInTheDocument();
  });

  it('يعرض التذكيرات بالاسم الثلاثي مع النص والشارة ورابط صفحة الملف', async () => {
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
          dueDate: '2030-01-01',
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
          dueDate: '2030-02-01',
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
    expect(screen.getAllByText(/^بعد \d+ يوم$/).length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText('2').length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByRole('button', { name: 'إلغاء التذكير' }).length).toBe(2);
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
          dueDate: '2030-01-01',
        },
      ],
    });
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    render(<Dashboard />);

    const button = await screen.findByRole('button', { name: 'إلغاء التذكير' });
    await user.click(button);

    expect(api.delete).toHaveBeenCalledWith('/documents/5/actions/7/reminder');
    expect(await screen.findByText('لا توجد تذكيرات حالياً')).toBeInTheDocument();
  });

  it('يعرض حالة فارغة للتنبيهات ولا يظهر شارة غير المقروء', async () => {
    mockApi({ reminders: [] });

    render(<Dashboard />);

    expect(await screen.findByText('لا توجد تذكيرات حالياً')).toBeInTheDocument();
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
  it('يعرض إحصاءات فترة فرعه وجدول محاميه وتنبيهات القسم دون قسم التذكيرات', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi({ alerts: HEAD_ALERTS, lawyers: BRANCH_LAWYERS });

    render(<Dashboard />);

    expect(await screen.findByText('إجمالي الملفات')).toBeInTheDocument();
    expect(screen.getByText('منفذ بالتسوية')).toBeInTheDocument();
    expect(screen.queryByText('التذكيرات')).not.toBeInTheDocument();
    expect(screen.getByText('تنبيهات رئيس القسم')).toBeInTheDocument();
    expect(screen.getByText('إحصائيات محامي الفرع')).toBeInTheDocument();
    expect(screen.getByText('محامي دمشق')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/stats/manager', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).not.toHaveBeenCalledWith('/reminders');
    expect(api.get).toHaveBeenCalledWith('/alerts');
    expect(api.get).toHaveBeenCalledWith('/users/lawyers');
    expect(api.get).not.toHaveBeenCalledWith('/branches');
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    expect(api.get).not.toHaveBeenCalledWith('/alerts/unread-count');
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
    expect(api.get).not.toHaveBeenCalledWith('/reminders');
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

    expect(await screen.findByText('نص التنبيه مطلوب')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
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
});

describe('Dashboard للمدير/المشرف', () => {
  it('يعرض بطاقات إحصاءات المدير ومحدد الفترة والفرع دون الإحصائيات القديمة', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByText('إجمالي الملفات')).toBeInTheDocument();
    expect(screen.getByText('متداول')).toBeInTheDocument();
    expect(screen.getByText('تحت رفع')).toBeInTheDocument();
    expect(screen.getByText('تريث')).toBeInTheDocument();
    expect(screen.getByText('منفذ')).toBeInTheDocument();
    expect(screen.getByText('منفذ للصالح')).toBeInTheDocument();
    expect(screen.getByText('منفذ بالتسوية')).toBeInTheDocument();
    expect(screen.getByText('منفذ جبريا')).toBeInTheDocument();
    expect(screen.getByText('منفذ للضد')).toBeInTheDocument();
    expect(screen.getByText('2,000 ل.س')).toBeInTheDocument();
    expect(screen.getByText('1,600 ل.س')).toBeInTheDocument();
    expect(screen.getByText('1,000 ل.س')).toBeInTheDocument();
    expect(screen.getByText('4,500 ل.س')).toBeInTheDocument();
    expect(screen.getByText('1,500 ل.س')).toBeInTheDocument();
    expect(screen.getByText('500 ل.س')).toBeInTheDocument();
    expect(screen.getByText('5,000 دولار')).toBeInTheDocument();
    expect(screen.getByText('5,200 دولار')).toBeInTheDocument();
    expect(screen.getByText('200 دولار')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'شهري' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ربعي' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'سنوي' })).toBeInTheDocument();
    expect(await screen.findAllByText('السنة 2026')).not.toHaveLength(0);

    expect(api.get).toHaveBeenCalledWith('/branches');
    expect(api.get).toHaveBeenCalledWith('/stats/periods', expect.any(Object));
    expect(api.get).toHaveBeenCalledWith(
      '/stats/manager',
      expect.objectContaining({ params: expect.objectContaining({ period: 'yearly' }) }),
    );
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers');
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    expect(api.get).not.toHaveBeenCalledWith('/monthly-stats');
    expect(api.get).not.toHaveBeenCalledWith('/reminders');
    expect(api.get).not.toHaveBeenCalledWith('/alerts');
    expect(api.get).not.toHaveBeenCalledWith('/users/lawyers');
    expect(screen.queryByText('المستندات شهرياً')).not.toBeInTheDocument();
    expect(screen.queryByText('عدد المقترضين')).not.toBeInTheDocument();
    expect(screen.getByText(/عرض الفترة/)).toBeInTheDocument();
  });

  it('يجمع إحصائيات المنفذ في بطاقة واحدة: للصالح (بالتسوية + جبريا + عرض وايداع) وللضد بعدد الملفات والمبالغ', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    await screen.findByText('إجمالي الملفات');

    const card = screen.getByText('منفذ').closest('.bg-white.rounded-2xl') as HTMLElement;
    expect(card).toBeTruthy();

    expect(within(card).getByText('5')).toBeInTheDocument();
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

  it('يثبّت عداد الملفات في موضع واحد في جميع بطاقات الإحصائيات', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    await screen.findByText('إجمالي الملفات');

    const counters = Array.from(document.querySelectorAll('.font-bold.tabular-nums'));
    expect(counters).toHaveLength(5);
    counters.forEach((el) => {
      expect(el.getAttribute('dir')).toBe('ltr');
      expect(el.className).toContain('text-right');
    });

    const contentBlocks = Array.from(document.querySelectorAll('.bg-white.rounded-2xl .flex-1.min-w-0'));
    expect(contentBlocks).toHaveLength(5);
  });

  it('يعرض ملفات «عرض وايداع» المتداولة كسطر فرعي داخل بطاقة متداول', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'manager1', fullName: 'مدير', role: 'manager', branchId: null },
    });
    mockApi();

    render(<Dashboard />);

    await screen.findByText('إجمالي الملفات');

    const card = screen.getByText('متداول').closest('.bg-white.rounded-2xl') as HTMLElement;
    expect(card).toBeTruthy();

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
});
