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
});
