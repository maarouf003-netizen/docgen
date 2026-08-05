import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import Dashboard from './Dashboard';
import type { DashboardStatsDto, DocumentResponse, ManagerLawyerStatDto, ManagerStatsDto, ReminderDto } from '../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
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
  forcibleCount: 1,
  forcibleCollected: 500,
  periodYear: 2026,
  periodQuarter: null,
  periodMonth: 8,
};

const MANAGER_LAWYERS: ManagerLawyerStatDto[] = [
  { lawyerId: 1, lawyerName: 'محامي دمشق', totalCount: 3, points: [{ year: 2026, month: 8, count: 3 }] },
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

function mockApi(overrides?: { reminders?: ReminderDto[]; recent?: DocumentResponse[]; monthly?: [] }) {
  const reminders = overrides?.reminders ?? [];
  const recent = overrides?.recent ?? [];
  const monthly = overrides?.monthly ?? [];
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation(
    (url: string, config?: { params?: Record<string, unknown> }) => {
      if (url === '/dashboard') return Promise.resolve({ data: STATS });
      if (url === '/reminders') return Promise.resolve({ data: reminders });
      if (url === '/monthly-stats') return Promise.resolve({ data: monthly });
      if (url === '/branches') {
        return Promise.resolve({
          data: [
            { id: 1, name: 'الفرع الرئيسي - دمشق', code: 'DAM' },
            { id: 2, name: 'فرع حلب', code: 'ALP' },
          ],
        });
      }
      if (url === '/stats/manager') {
        const period = typeof config?.params?.period === 'string' ? config.params.period : 'monthly';
        return Promise.resolve({ data: managerStatsFor(period) });
      }
      if (url === '/stats/manager/lawyers') return Promise.resolve({ data: MANAGER_LAWYERS });
      if (url.startsWith('/documents')) {
        return Promise.resolve({
          data: { page: 1, perPage: 10, totalCount: recent.length, totalPages: 1, items: recent },
        });
      }
      return Promise.resolve({ data: {} });
    },
  );
}

function expectOrder(texts: string[]) {
  const nodes = texts.map((t) => screen.getByText(t, { exact: true }));
  for (let i = 0; i < nodes.length - 1; i += 1) {
    expect(nodes[i].compareDocumentPosition(nodes[i + 1]) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  }
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({
    user: { id: 1, username: 'lawyer1', fullName: 'محامي', role: 'lawyer', branchId: 1 },
  });
});

describe('Dashboard للمحامي', () => {
  it('يعرض بطاقات الإحصائيات السبعة بالترتيب المحدد ويجلب التذكيرات فقط', async () => {
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByText('متداول')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();

    expectOrder([
      'إجمالي المستندات',
      'متداول',
      'تحت رفع',
      'تريث',
      'منفذ',
      'إجمالي المبالغ (باستثناء تحت رفع)',
      'إجمالي المبالغ المحصلة',
    ]);

    expect(api.get).toHaveBeenCalledWith('/dashboard');
    expect(api.get).toHaveBeenCalledWith('/reminders');
    expect(api.get).not.toHaveBeenCalledWith('/monthly-stats');
    expect(api.get).not.toHaveBeenCalledWith('/documents?perPage=10');
    expect(screen.queryByText('عدد المقترضين')).not.toBeInTheDocument();
    expect(screen.queryByText('المستندات شهرياً')).not.toBeInTheDocument();
    expect(screen.queryByText('أحدث المستندات')).not.toBeInTheDocument();
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
    expect(screen.getByText('2')).toBeInTheDocument();
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

  it('يعرض حالة فارغة عندما لا توجد تذكيرات ولا يعرض قسم رئيس القسم للمحامي', async () => {
    mockApi({ reminders: [] });

    render(<Dashboard />);

    expect(await screen.findByText('لا توجد تذكيرات حالياً')).toBeInTheDocument();
    expect(screen.queryByText('تنبيهات رئيس القسم')).not.toBeInTheDocument();
    expect(screen.queryByText('ستظهر هنا تنبيهات رئيس القسم قريباً')).not.toBeInTheDocument();
  });
});

describe('Dashboard لغير المحامي', () => {
  it('يعرض تنبيهات رئيس القسم مع الشهري والأحدث', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    mockApi();

    render(<Dashboard />);

    expect(await screen.findByText('عدد المقترضين')).toBeInTheDocument();
    expect(screen.getByText('المستندات شهرياً')).toBeInTheDocument();
    expect(screen.getByText('أحدث المستندات')).toBeInTheDocument();
    expect(screen.getByText('تنبيهات رئيس القسم')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/reminders');
    expect(api.get).toHaveBeenCalledWith('/monthly-stats');
    expect(api.get).toHaveBeenCalledWith('/documents?perPage=10');
    expect(screen.queryByText('التذكيرات')).not.toBeInTheDocument();
    expect(screen.queryByText('متداول')).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith('/branches');
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager');
  });

  it('يعرض تذكيرات الفرع في تنبيهات رئيس القسم دون زر إلغاء', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
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
      ],
    });

    render(<Dashboard />);

    expect(await screen.findByText('سامر محمد حسن')).toBeInTheDocument();
    expect(screen.getByText('مراجعة دائرة التنفيذ')).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /سامر محمد حسن/ })[0]).toHaveAttribute('href', '/documents/5');
    expect(screen.queryAllByRole('button', { name: 'إلغاء التذكير' }).length).toBe(0);
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
    expect(screen.getByText('المنفذ')).toBeInTheDocument();
    expect(screen.getByText('منفذ بالتسوية')).toBeInTheDocument();
    expect(screen.getByText('منفذ جبريا')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'شهري' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ربعي' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'سنوي' })).toBeInTheDocument();

    expect(api.get).toHaveBeenCalledWith('/branches');
    expect(api.get).toHaveBeenCalledWith(
      '/stats/manager',
      expect.objectContaining({ params: expect.objectContaining({ period: 'monthly' }) }),
    );
    expect(api.get).not.toHaveBeenCalledWith('/stats/manager/lawyers');
    expect(api.get).not.toHaveBeenCalledWith('/dashboard');
    expect(api.get).not.toHaveBeenCalledWith('/monthly-stats');
    expect(api.get).not.toHaveBeenCalledWith('/documents?perPage=10');
    expect(api.get).not.toHaveBeenCalledWith('/reminders');
    expect(screen.queryByText('المستندات شهرياً')).not.toBeInTheDocument();
    expect(screen.queryByText('عدد المقترضين')).not.toBeInTheDocument();
    expect(screen.getByText(/عرض الفترة/)).toBeInTheDocument();
    expect(screen.getByText('الشهر الحالي — آب 2026')).toBeInTheDocument();
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
    expect(await screen.findByText('الربع الحالي — الربع الثالث 2026')).toBeInTheDocument();
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
      expect.objectContaining({ params: expect.objectContaining({ branchId: 1, period: 'monthly' }) }),
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
