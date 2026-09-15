import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
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

  it('يعرض السرد النصي لوقعة «تغيير جهة» في سطور الوقوعات', () => {
    const narrative = 'تم نقل قيد «وزارة التعليم» (دمشق/الفرع الرئيسي) بموجب قرار إداري رقم 123 بتاريخ 2026-08-01';
    const doc = makeDocument({
      occurrences: [
        {
          id: 9,
          occurrenceType: 'entity-change',
          occurrenceTypeLabel: 'تغيير جهة',
          source: 'system',
          detailsText: narrative,
        },
      ],
    });
    render(<OccurrencesCard doc={doc} onOpen={vi.fn()} onOpenAppeal={vi.fn()} />);

    expect(screen.getByText(narrative)).toBeInTheDocument();
  });

  it('يجمع سطور «تغيير جهة» في قسم خاص منفصل عن «الشطوبات»', () => {
    const narrative = 'تم نقل قيد «وزارة التعليم» (دمشق/الفرع الرئيسي)';
    const doc = makeDocument({
      occurrences: [
        {
          id: 9,
          occurrenceType: 'entity-change',
          occurrenceTypeLabel: 'تغيير جهة',
          source: 'system',
          detailsText: narrative,
        },
      ],
    });
    render(<OccurrencesCard doc={doc} onOpen={vi.fn()} onOpenAppeal={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'التغييرات التي وقعت على الجهة العامة' })).toBeInTheDocument();
    const section = screen.getByRole('region', { name: 'التغييرات التي وقعت على الجهة العامة' });
    expect(within(section).getByText(narrative)).toBeInTheDocument();
    // قسم الشطوبات يبقى فارغًا (لا سطور شطب) مع بقاء البطاقة ظاهرة
    expect(screen.getByText('لا توجد شطوبات.')).toBeInTheDocument();
    // نافذة التفاصيل تبقى متاحة لأن البطاقة تحمل وقعات مسجلة
    expect(screen.getByRole('button', { name: 'عرض تفاصيل وقوعات الملف' })).toBeInTheDocument();
  });

  it('يعرض وقعة تغيير الحالة في قسم مستقل ويُبقي زر التفاصيل متاحًا', () => {
    const statusLine = 'تريث بموجب كتاب التريث رقم 33 بتاريخ 3/3/2024';
    const doc = makeDocument({
      occurrences: [
        {
          id: 2,
          occurrenceType: 'deferred',
          occurrenceTypeLabel: 'تريث',
          eventDate: '2026-08-01',
          source: 'system',
          details: { tarithNumber: '33', tarithDate: '3/3/2024' },
        },
      ],
    });
    render(<OccurrencesCard doc={doc} onOpen={vi.fn()} onOpenAppeal={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'تغييرات الحالة' })).toBeInTheDocument();
    const section = screen.getByRole('region', { name: 'تغييرات الحالة' });
    expect(within(section).getByText(statusLine)).toBeInTheDocument();
    expect(screen.getByText('لا توجد شطوبات.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'عرض تفاصيل وقوعات الملف' })).toBeInTheDocument();
  });

  it('يفصل سطر الشطب عن سطر تغيير الحالة في قسميهما', () => {
    const statusLine = 'تريث بموجب كتاب التريث رقم 33 بتاريخ 3/3/2024';
    const doc = makeDocument({
      occurrences: [
        {
          id: 1,
          occurrenceType: 'struck-off',
          occurrenceTypeLabel: 'شطب',
          eventDate: '2026-07-01',
          fileNumber: '99',
          year: 2026,
          source: 'system',
        },
        {
          id: 2,
          occurrenceType: 'deferred',
          occurrenceTypeLabel: 'تريث',
          eventDate: '2026-08-01',
          source: 'system',
          details: { tarithNumber: '33', tarithDate: '3/3/2024' },
        },
      ],
    });
    render(<OccurrencesCard doc={doc} onOpen={vi.fn()} onOpenAppeal={vi.fn()} />);

    const struckSection = screen.getByRole('region', { name: 'الشطوبات' });
    const statusSection = screen.getByRole('region', { name: 'تغييرات الحالة' });
    expect(within(struckSection).getByText(/تم شطب الملف رقم 99 لعام 2026/)).toBeInTheDocument();
    expect(within(struckSection).queryByText(statusLine)).not.toBeInTheDocument();
    expect(within(statusSection).getByText(statusLine)).toBeInTheDocument();
    expect(within(statusSection).queryByText(/تم شطب الملف/)).not.toBeInTheDocument();
  });
});
