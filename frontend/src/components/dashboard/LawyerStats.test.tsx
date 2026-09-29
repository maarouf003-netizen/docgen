import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DeltaBadge } from './DeltaBadge';
import { LawyerStatCard } from './LawyerStatCard';
import { LawyerStatsSection } from './LawyerStatsSection';
import { Sparkline } from './Sparkline';
import type { ManagerStatsDto } from '../../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }: { children: React.ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

describe('DeltaBadge', () => {
  it('يعرض الارتفاع بإشارة موجبة ولون زمردي', () => {
    render(<DeltaBadge value={25} />);
    const badge = screen.getByLabelText('ارتفاع +25% عن الفترة السابقة');
    expect(badge).toHaveTextContent('+25%');
    expect(badge.className).toContain('text-emerald-700');
  });

  it('يعرض الانخفاض باللون الأحمر', () => {
    render(<DeltaBadge value={-12.5} />);
    expect(screen.getByLabelText('انخفاض -12.5% عن الفترة السابقة')).toBeInTheDocument();
  });

  it('يعرض الثبات بلون محايد', () => {
    render(<DeltaBadge value={0} />);
    expect(screen.getByLabelText('بلا تغيّر عن الفترة السابقة')).toHaveTextContent('0%');
  });

  it('يُخفي نفسه عند غياب السابق أو صفره', () => {
    const { container } = render(<DeltaBadge value={null} />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('LawyerStatCard', () => {
  it('يعرض العنوان والرقم والدلتا ورابط التعمّق', () => {
    render(
      <LawyerStatCard title="تحت رفع" value={7} delta={-50} drillTo="/documents?status=%D8%AA%D8%AD%D8%AA">
        <p>تفاصيل</p>
      </LawyerStatCard>,
    );

    expect(screen.getByRole('heading', { name: 'تحت رفع' })).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByLabelText('انخفاض -50% عن الفترة السابقة')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'عرض الملفات ←' })).toHaveAttribute(
      'href',
      '/documents?status=%D8%AA%D8%AD%D8%AA',
    );
    // التفاصيل مخفية خلف التوسيع.
    expect(screen.queryByText('تفاصيل')).not.toBeInTheDocument();
  });

  it('يوسّع التفاصيل ويطويها بزر `aria-expanded`', async () => {
    const user = userEvent.setup();
    render(
      <LawyerStatCard title="منفذ" value={3}>
        <p>تفاصيل المنفذ</p>
      </LawyerStatCard>,
    );

    const toggle = screen.getByRole('button', { name: 'عرض التفاصيل' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');

    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('تفاصيل المنفذ')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'إخفاء التفاصيل' }));
    expect(screen.queryByText('تفاصيل المنفذ')).not.toBeInTheDocument();
  });
});

describe('Sparkline', () => {
  it('لا يعرض شيئًا بلا نقاط', () => {
    const { container } = render(<Sparkline points={[]} description="الاتجاه" />);
    expect(container).toBeEmptyDOMElement();
  });

  it('يرسم الخط ويصف الاتجاه لقارئ الشاشة', () => {
    render(
      <Sparkline
        points={[
          { year: 2026, month: 1, count: 2 },
          { year: 2026, month: 2, count: 5 },
        ]}
        description="الملفات المسجَّلة شهريًا: ارتفاع"
      />,
    );

    expect(screen.getByText('الملفات المسجَّلة شهريًا: ارتفاع: من كانون الثاني 2026 إلى شباط 2026')).toBeInTheDocument();
  });
});

const SECTION_STATS: ManagerStatsDto = {
  totalFiles: 6,
  active: 3,
  drafts: 1,
  deferred: 1,
  activeSplit: { bankingCount: 3, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] },
  draftsSplit: { bankingCount: 1, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] },
  deferredSplit: { bankingCount: 1, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] },
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

function renderSection(stats: ManagerStatsDto | null, error = '', fallback = 0) {
  return render(
    <LawyerStatsSection
      period="yearly"
      onPeriodChange={() => {}}
      availablePeriods={[{ year: 2026, month: 8, count: 2 }]}
      selection={{ year: 2026 }}
      onSelectionChange={() => {}}
      stats={stats ? { ...stats, periodDateFallbackCount: fallback } : null}
      prevStats={null}
      appealsStats={null}
      error={error}
    />,
  );
}

describe('LawyerStatsSection', () => {
  it('يعرض الخطأ والتحميل في حالتيهما', () => {
    const { unmount } = renderSection(null, 'تعذر الجلب');
    expect(screen.getByText('تعذر الجلب')).toBeInTheDocument();
    unmount();

    renderSection(null);
    expect(screen.getByText('جارِ التحميل...')).toBeInTheDocument();
  });

  it('يطوي ملاحظة مصدر التواريخ خلف زر ولا يعرضها بلا تحذيرات', async () => {
    const user = userEvent.setup();
    const { unmount } = renderSection(SECTION_STATS, '', 2);

    const toggle = screen.getByRole('button', { name: 'ملاحظة حول مصدر تواريخ الفترة…' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText(/حُسبت بتاريخ إدخالها لغياب تاريخ قيدها/)).toBeInTheDocument();
    unmount();

    renderSection(SECTION_STATS);
    expect(screen.queryByRole('button', { name: /مصدر تواريخ الفترة/ })).not.toBeInTheDocument();
  });

  it('يعرض «حُدّث الآن» عند توفر البيانات', () => {
    renderSection(SECTION_STATS);
    expect(screen.getByText(/حُدّث الآن/)).toBeInTheDocument();
  });
});
