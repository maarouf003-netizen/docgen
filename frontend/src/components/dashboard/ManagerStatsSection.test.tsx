import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ManagerStatsSection } from './ManagerStatsSection';
import type { ManagerLawyerStatDto, ManagerStatsDto, StatsPeriod } from '../../types';

function makeStats(overrides: Partial<ManagerStatsDto> = {}): ManagerStatsDto {
  const split = { bankingCount: 0, ordinaryCount: 0, bankingAmounts: [], ordinaryAmounts: [] };
  return {
    totalFiles: 3,
    active: 2,
    drafts: 1,
    deferred: 0,
    activeSplit: split,
    draftsSplit: split,
    deferredSplit: split,
    totalAmounts: [],
    tradingAgainstAmounts: [],
    settledCount: 0,
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
    periodMonth: 5,
    periodDateFallbackCount: 0,
    periodDateFromReceiptCount: 0,
    ...overrides,
  };
}

function renderSection(stats: ManagerStatsDto, lawyers: ManagerLawyerStatDto[] = [], branchId: number | null = null) {
  render(
    <ManagerStatsSection
      period={'monthly' as StatsPeriod}
      onPeriodChange={vi.fn()}
      availablePeriods={[]}
      selection={{ year: 2026, month: 5 }}
      onSelectionChange={vi.fn()}
      branches={[]}
      branchId={branchId}
      onBranchChange={vi.fn()}
      stats={stats}
      lawyers={lawyers}
      error=""
    />,
  );
}

describe('ManagerStatsSection — وسم مصدر الفترة', () => {
  it('يعرض حاشية الملفات المحسوبة بتاريخ إدخالها ويخفيها عند الصفر', () => {
    const { rerender } = render(
      <ManagerStatsSection
        period={'monthly' as StatsPeriod}
        onPeriodChange={vi.fn()}
        availablePeriods={[]}
        selection={{ year: 2026, month: 5 }}
        onSelectionChange={vi.fn()}
        branches={[]}
        branchId={null}
        onBranchChange={vi.fn()}
        stats={makeStats({ periodDateFallbackCount: 2 })}
        lawyers={[]}
        error=""
      />,
    );
    expect(screen.getByText(/حُسبت بتاريخ إدخالها لغياب تاريخ قيدها أو تعذّر تحليله/)).toBeInTheDocument();

    rerender(
      <ManagerStatsSection
        period={'monthly' as StatsPeriod}
        onPeriodChange={vi.fn()}
        availablePeriods={[]}
        selection={{ year: 2026, month: 5 }}
        onSelectionChange={vi.fn()}
        branches={[]}
        branchId={null}
        onBranchChange={vi.fn()}
        stats={makeStats({ periodDateFallbackCount: 0 })}
        lawyers={[]}
        error=""
      />,
    );
    expect(screen.queryByText(/حُسبت بتاريخ إدخالها لغياب تاريخ قيدها أو تعذّر تحليله/)).not.toBeInTheDocument();
  });

  it('يعرض حاشية غياب الإخطار بسببها الدقيق ويخفيها عند الصفر', () => {
    const { rerender } = render(
      <ManagerStatsSection
        period={'monthly' as StatsPeriod}
        onPeriodChange={vi.fn()}
        availablePeriods={[]}
        selection={{ year: 2026, month: 5 }}
        onSelectionChange={vi.fn()}
        branches={[]}
        branchId={null}
        onBranchChange={vi.fn()}
        stats={makeStats({ periodDateFromReceiptCount: 2 })}
        lawyers={[]}
        error=""
      />,
    );
    expect(screen.getByText(/حُسبت بتاريخ إدخالها لغياب تاريخ ورود الإخطار/)).toBeInTheDocument();

    rerender(
      <ManagerStatsSection
        period={'monthly' as StatsPeriod}
        onPeriodChange={vi.fn()}
        availablePeriods={[]}
        selection={{ year: 2026, month: 5 }}
        onSelectionChange={vi.fn()}
        branches={[]}
        branchId={null}
        onBranchChange={vi.fn()}
        stats={makeStats({ periodDateFromReceiptCount: 0 })}
        lawyers={[]}
        error=""
      />,
    );
    expect(screen.queryByText(/حُسبت بتاريخ إدخالها لغياب تاريخ ورود الإخطار/)).not.toBeInTheDocument();
  });

  it('يعرض حاشية جدول المحامين عند وجود نقاط محسوبة بتاريخ الإدخال', () => {
    renderSection(makeStats(), [
      {
        lawyerId: 1,
        lawyerName: 'محامٍ',
        totalCount: 2,
        points: [
          { year: 2026, month: 5, count: 2, fromCreatedAtCount: 1 },
        ],
      },
    ], 1);
    expect(screen.getByText(/محسوبة\s*بتاريخ الإدخال لغياب تاريخ قيدها أو تعذّر تحليله/)).toBeInTheDocument();
  });

  it('يخفي حاشية جدول المحامين عند غياب الوسم', () => {
    renderSection(makeStats(), [
      {
        lawyerId: 1,
        lawyerName: 'محامٍ',
        totalCount: 2,
        points: [{ year: 2026, month: 5, count: 2, fromCreatedAtCount: 0 }],
      },
    ], 1);
    expect(screen.queryByText(/محسوبة\s*بتاريخ الإدخال/)).not.toBeInTheDocument();
  });
});
