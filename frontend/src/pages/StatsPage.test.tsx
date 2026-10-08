import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import StatsPage from './StatsPage';
import type { ManagerStatsDto, MonthlyStatDto } from '../types';

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

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

const STATS: ManagerStatsDto = {
  totalFiles: 10,
  active: 4,
  drafts: 2,
  deferred: 1,
  settledCount: 2,
  settledCollected: 0,
  settledCollectedAmounts: [],
  forcibleCount: 1,
  forcibleCollected: 0,
  forcibleCollectedAmounts: [],
  tradingAgainstCount: 0,
  executedAgainstCount: 0,
  executedAgainstAmount: 0,
  depositTradingCount: 1,
  depositExecutedCount: 0,
  depositExecutedAmount: 0,
  totalAmounts: [],
  activeSplit: { bankingCount: 4, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] },
  draftsSplit: { bankingCount: 2, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] },
  deferredSplit: { bankingCount: 1, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] },
  tradingAgainstAmounts: [],
  periodYear: 2026,
  periodQuarter: null,
  periodMonth: null,
  periodDateFallbackCount: 0,
  periodDateFromReceiptCount: 0,
};

const PERIODS: MonthlyStatDto[] = [{ year: 2026, month: 8, count: 3 }];

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({
    user: { id: 1, username: 'lawyer1', fullName: 'محامي', role: 'lawyer', branchId: 1 },
  });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/stats/periods') return Promise.resolve({ data: PERIODS });
    if (url === '/stats/me') return Promise.resolve({ data: STATS });
    if (url === '/stats/manager') return Promise.resolve({ data: STATS });
    if (url === '/stats/manager/lawyers') return Promise.resolve({ data: [] });
    return Promise.resolve({ data: {} });
  });
});

describe('StatsPage', () => {
  it('تعرض بطاقة البطل والدلتا من ندائي الفترة الحالية والسابقة', async () => {
    render(<StatsPage />);

    expect(await screen.findByRole('heading', { name: 'الإحصائيات' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'متداولة ضمن السنة 2026' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'إجمالي الملفات' })).toBeInTheDocument();

    const calls = () => vi.mocked(api.get).mock.calls.filter(([url]) => url === '/stats/me');
    await waitFor(() => expect(calls().length).toBeGreaterThanOrEqual(2));
    const years = calls().map(([, config]) => (config as { params?: Record<string, unknown> })?.params?.year);
    expect(years).toContain(2026);
    expect(years).toContain(2025);
  });

  it('تعرض لرئيس القسم إحصائيات فرعه بلا روابط تعمّق مع جدول المحامين', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/stats/periods') return Promise.resolve({ data: PERIODS });
      if (url === '/stats/manager') return Promise.resolve({ data: STATS });
      if (url === '/stats/manager/lawyers')
        return Promise.resolve({
          data: [
            {
              lawyerId: 1,
              lawyerName: 'محامي دمشق',
              totalCount: 3,
              points: [{ year: 2026, month: 8, count: 3, fromCreatedAtCount: 0 }],
            },
          ],
        });
      return Promise.resolve({ data: {} });
    });

    render(<StatsPage />);

    expect(await screen.findByLabelText('إحصائيات القسم')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'متداولة ضمن السنة 2026' })).toBeInTheDocument();
    // بلا روابط تعمّق وبلا استئنافات لرئيس القسم.
    expect(screen.queryByRole('link', { name: 'عرض الملفات ←' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'الاستئنافات' })).not.toBeInTheDocument();
    // جدول محامي الفرع حاضرة.
    expect(screen.getByRole('heading', { name: 'إحصائيات محامي الفرع' })).toBeInTheDocument();
    expect(screen.getByText('محامي دمشق')).toBeInTheDocument();

    const urls = vi.mocked(api.get).mock.calls.map(([url]) => url);
    expect(urls).toContain('/stats/manager');
    expect(urls).toContain('/stats/manager/lawyers');
    expect(urls).not.toContain('/stats/me');
    // فرع الرئيس إجباري خلفيًا — لا وسيط `branchId` إطلاقًا.
    for (const [, config] of vi.mocked(api.get).mock.calls) {
      expect((config as { params?: Record<string, unknown> } | undefined)?.params?.branchId).toBeUndefined();
    }
    // مسار الدلتا السابق يعمل للرئيس (نداءان بسنتين مختلفتين).
    const mgrCalls = () => vi.mocked(api.get).mock.calls.filter(([url]) => url === '/stats/manager');
    await waitFor(() => expect(mgrCalls().length).toBeGreaterThanOrEqual(2));
    const years = mgrCalls().map(([, c]) => (c as { params?: Record<string, unknown> })?.params?.year);
    expect(years).toContain(2026);
    expect(years).toContain(2025);
  });

  it('تعرض خطأ جدول المحامين للرئيس بدل عبارة «لا يوجد محامون» المضللة', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: 1 },
    });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/stats/periods') return Promise.resolve({ data: PERIODS });
      if (url === '/stats/manager') return Promise.resolve({ data: STATS });
      if (url === '/stats/manager/lawyers') return Promise.reject(new Error('تعذّر جلب الجدول'));
      return Promise.resolve({ data: {} });
    });

    render(<StatsPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر جلب الجدول');
    expect(screen.queryByText('لا يوجد محامون في هذا الفرع')).not.toBeInTheDocument();
  });

  it('الرئيس بلا فرع يرى رسالة تعيين الفرع وحده بلا أي طلب إحصائيات', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 1, username: 'head1', fullName: 'رئيس', role: 'head', branchId: null },
    });

    render(<StatsPage />);

    // رسالة واحدة فقط — القسم مخفي فلا تكديس مع خطأ شبكة.
    expect(await screen.findByText('لا يوجد فرع مرتبط بحسابك — تواصل مع المشرف لتعيين فرعك')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    const urls = vi.mocked(api.get).mock.calls.map(([url]) => url);
    expect(urls).not.toContain('/stats/periods');
    expect(urls).not.toContain('/stats/manager');
    expect(urls).not.toContain('/stats/manager/lawyers');
  });

  it('تعرض لرئيس الشعبة إحصائيات شعبته عبر /stats/manager (نطاقه إجباري خلفيًا)', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 6, username: 'sub1', fullName: 'رئيس شعبة', role: 'subhead', branchId: 1, sectionId: 3 },
    });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/stats/periods') return Promise.resolve({ data: PERIODS });
      if (url === '/stats/manager') return Promise.resolve({ data: STATS });
      if (url === '/stats/manager/lawyers') return Promise.resolve({ data: [] });
      return Promise.resolve({ data: {} });
    });

    render(<StatsPage />);

    expect(await screen.findByLabelText('إحصائيات الشعبة')).toBeInTheDocument();
    const urls = vi.mocked(api.get).mock.calls.map(([url]) => url);
    expect(urls).toContain('/stats/manager');
    expect(urls).not.toContain('/stats/me');
  });

  it('رئيس الشعبة برمز بلا شعبة يرى رسالة تعيين الفرع بلا أي طلب', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 6, username: 'sub1', fullName: 'رئيس شعبة', role: 'subhead', branchId: 1, sectionId: null },
    });

    render(<StatsPage />);

    expect(await screen.findByText('لا يوجد فرع مرتبط بحسابك — تواصل مع المشرف لتعيين فرعك')).toBeInTheDocument();
    expect(vi.mocked(api.get).mock.calls).toHaveLength(0);
  });
});
