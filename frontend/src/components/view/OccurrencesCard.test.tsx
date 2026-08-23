import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OccurrencesCard } from './OccurrencesCard';
import { makeDocument, makeStruckOffDocument } from '../../test/factories';
import type { AppealDto } from '../../types';

function makeAppeal(overrides: Partial<AppealDto> = {}): AppealDto {
  return {
    id: 3,
    documentId: 1,
    documentLabel: 'أحمد خالد الخطيب',
    court: 'دمشق',
    direction: 'appellants',
    directionLabel: 'مستأنِفين',
    status: 'pending',
    statusLabel: 'منظور',
    appellants: [],
    appellees: [],
    appealedDecisionDate: '2026-08-01',
    needsRotation: false,
    createdAt: '2026-08-02T00:00:00Z',
    createdById: 1,
    ...overrides,
  };
}

describe('OccurrencesCard', () => {
  it('يخفي البطاقة كليًا عند غياب الوقوعات والاستئنافات', () => {
    const { container } = render(
      <OccurrencesCard doc={makeDocument()} onOpen={vi.fn()} onOpenAppeal={vi.fn()} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('يعرض جزء «الشطوبات» مع سطر الشطب والتاريخ التراثي', () => {
    const doc = makeStruckOffDocument();
    render(<OccurrencesCard doc={doc} onOpen={vi.fn()} onOpenAppeal={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'الشطوبات' })).toBeInTheDocument();
    expect(screen.getByText('لا توجد استئنافات.')).toBeInTheDocument();
  });

  it('يعرض جزء «الاستئنافات» بسطر الاستئناف وشارة حالته ويفتح تفاصيله', async () => {
    const onOpenAppeal = vi.fn();
    const user = userEvent.setup();
    const appeal = makeAppeal({ status: 'decided', statusLabel: 'محسوم' });

    render(
      <OccurrencesCard
        doc={makeDocument()}
        appeals={[appeal]}
        onOpen={vi.fn()}
        onOpenAppeal={onOpenAppeal}
      />,
    );

    expect(screen.getByRole('heading', { name: 'الاستئنافات' })).toBeInTheDocument();
    const row = screen.getByRole('button', { name: /استئناف قرار رئيس التنفيذ/ });
    expect(row).toHaveTextContent('محسوم');

    await user.click(row);
    expect(onOpenAppeal).toHaveBeenCalledWith(appeal);
  });

  it('يعرض الشارتين معًا عند وجود شطب واستئنافات', () => {
    const appeal = makeAppeal();
    render(
      <OccurrencesCard
        doc={makeDocument({ struckOffDate: '2026-01-05' })}
        appeals={[appeal]}
        onOpen={vi.fn()}
        onOpenAppeal={vi.fn()}
      />,
    );

    expect(screen.getByRole('heading', { name: 'الشطوبات' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'الاستئنافات' })).toBeInTheDocument();
    expect(screen.queryByText('لا توجد استئنافات.')).not.toBeInTheDocument();
  });
});
