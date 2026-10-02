import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StatsCards } from './StatsCards';
import type { ManagerStatsDto } from '../../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }: { children: React.ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

function makeStats(): ManagerStatsDto {
  const split = { bankingCount: 0, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] };
  return {
    totalFiles: 6,
    active: 3,
    drafts: 1,
    deferred: 1,
    activeSplit: split,
    draftsSplit: split,
    deferredSplit: split,
    totalAmounts: [],
    tradingAgainstAmounts: [],
    settledCount: 1,
    settledCollected: 0,
    settledCollectedAmounts: [],
    forcibleCount: 0,
    forcibleCollected: 0,
    forcibleCollectedAmounts: [],
    tradingAgainstCount: 0,
    executedAgainstCount: 0,
    executedAgainstAmount: 0,
    depositTradingCount: 0,
    depositExecutedCount: 0,
    depositExecutedAmount: 0,
    periodYear: 2026,
    periodQuarter: null,
    periodMonth: null,
    periodDateFallbackCount: 0,
    periodDateFromReceiptCount: 0,
  };
}

describe('StatsCards', () => {
  it('يعرض البطل والمؤشرات بروابط التعمّق عند التفعيل', () => {
    render(<StatsCards stats={makeStats()} prevStats={null} showDrillLinks appealsStats={null} />);

    expect(screen.getByRole('heading', { name: /متداولة ضمن/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'إجمالي الملفات' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'تحت رفع' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'منفذ' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'تريث' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'محال الى البداية' })).toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: 'عرض الملفات ←' }).length).toBeGreaterThan(0);
  });

  it('يخفي كل روابط التعمّق عند التعطيل (الرئيس/المدير)', () => {
    render(<StatsCards stats={makeStats()} prevStats={null} showDrillLinks={false} appealsStats={null} />);

    expect(screen.getByRole('heading', { name: /متداولة ضمن/ })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'عرض الملفات ←' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'الاستئنافات' })).not.toBeInTheDocument();
  });

  it('يُظهر بطاقة الاستئنافات عند توفرها ويخفيها عند `null`', () => {
    const { unmount } = render(
      <StatsCards
        stats={makeStats()}
        prevStats={null}
        showDrillLinks
        appealsStats={{ pendingCount: 1, decidedInFavor: 0, decidedAgainst: 0 }}
      />,
    );
    expect(screen.getByRole('heading', { name: 'الاستئنافات' })).toBeInTheDocument();
    unmount();

    render(<StatsCards stats={makeStats()} prevStats={null} showDrillLinks appealsStats={null} />);
    expect(screen.queryByRole('heading', { name: 'الاستئنافات' })).not.toBeInTheDocument();
  });

  it('يعرض مبالغ العملات في البطل والإجمالي وتقسيم العقد (تغطية منقولة من اللوحة)', async () => {
    const user = userEvent.setup();
    const stats = makeStats();
    stats.tradingAgainstAmounts = [
      { currency: 'ليرة سورية', amount: 5000 },
      { currency: 'دولار أمريكي', amount: 200 },
    ];
    stats.totalAmounts = [
      { currency: 'ليرة سورية', amount: 2000 },
      { currency: 'دولار أمريكي', amount: 1200 },
    ];
    stats.activeSplit = {
      bankingCount: 2,
      ordinaryCount: 1,
      bankingAmounts: [{ currency: 'ليرة سورية', amount: 1600 }],
      ordinaryAmounts: [{ currency: 'دولار أمريكي', amount: 300 }],
    };
    render(<StatsCards stats={stats} prevStats={null} showDrillLinks appealsStats={null} />);

    // المبالغ خلف زر التوسيع في كل بطاقة (نفس سلوك اللوحة).
    const hero = screen.getByRole('heading', { name: /متداولة ضمن/ }).closest('article') as HTMLElement;
    await user.click(within(hero).getByRole('button', { name: 'عرض التفاصيل' }));
    expect(within(hero).getByText('5,000 ل.س')).toBeInTheDocument();
    expect(within(hero).getByText('200 دولار')).toBeInTheDocument();

    const total = screen.getByRole('heading', { name: 'إجمالي الملفات' }).closest('article') as HTMLElement;
    await user.click(within(total).getByRole('button', { name: 'عرض التفاصيل' }));
    expect(within(total).getByText('2,000 ل.س')).toBeInTheDocument();
    expect(within(total).getByText('1,200 دولار')).toBeInTheDocument();

    expect(screen.getByText('1,600 ل.س')).toBeInTheDocument();
    expect(screen.getByText('300 دولار')).toBeInTheDocument();
  });

  it('يعرض مبالغ «منفذ للضد» و«عرض وايداع» مفصولة لكل عملة (BQ-029)', async () => {
    const user = userEvent.setup();
    const stats = makeStats();
    stats.executedAgainstCount = 1;
    stats.executedAgainstAmount = 1000;
    stats.executedAgainstAmounts = [
      { currency: 'ليرة سورية', amount: 1000 },
      { currency: 'دولار أمريكي', amount: 200 },
    ];
    stats.depositExecutedCount = 1;
    stats.depositExecutedAmount = 700;
    stats.depositExecutedAmounts = [
      { currency: 'ليرة سورية', amount: 700 },
      { currency: 'يورو', amount: 50 },
    ];
    render(<StatsCards stats={stats} prevStats={null} showDrillLinks appealsStats={null} />);

    const executed = screen.getByRole('heading', { name: 'منفذ' }).closest('article') as HTMLElement;
    await user.click(within(executed).getByRole('button', { name: 'عرض التفاصيل' }));
    expect(within(executed).getByText(/1,000.*ل\.س.*200.*دولار/s)).toBeInTheDocument();
    expect(within(executed).getByText(/700.*ل\.س.*50.*يورو/s)).toBeInTheDocument();
  });
});
