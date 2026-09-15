import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OccurrencesModal } from './OccurrencesModal';
import { formatDate } from '../../utils/dates';
import type { DocumentOccurrenceDto } from '../../types';

function makeOccurrence(overrides: Partial<DocumentOccurrenceDto> = {}): DocumentOccurrenceDto {
  return {
    id: 1,
    occurrenceType: 'struck-off',
    occurrenceTypeLabel: 'شطب',
    eventDate: '2026-08-04',
    fileNumber: '77',
    fileType: 'حقوق',
    year: 2026,
    ...overrides,
  };
}

describe('OccurrencesModal', () => {
  it('يعرض تفاصيل الشطب: رقم الملف المشطوب، نوع الملف، تاريخ الشطب، وسنة الشطب', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[makeOccurrence()]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByText('رقم الملف المشطوب')).toBeInTheDocument();
    expect(screen.getByText('77')).toBeInTheDocument();
    expect(screen.getByText('نوع الملف المشطوب')).toBeInTheDocument();
    expect(screen.getByText('حقوق')).toBeInTheDocument();
    expect(screen.getByText('تاريخ الشطب')).toBeInTheDocument();
    expect(screen.getByText(formatDate('2026-08-04'))).toBeInTheDocument();
    expect(screen.getByText('سنة الشطب')).toBeInTheDocument();
    expect(screen.getByText('2026')).toBeInTheDocument();
  });

  it('يعرض النوع والتاريخ فقط عند توافرهما ولا يكسر عند غيابهما', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[makeOccurrence({ fileType: undefined, eventDate: undefined, year: undefined })]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByText('رقم الملف المشطوب')).toBeInTheDocument();
    expect(screen.queryByText('نوع الملف المشطوب')).not.toBeInTheDocument();
    expect(screen.queryByText('تاريخ الشطب')).not.toBeInTheDocument();
    expect(screen.queryByText('سنة الشطب')).not.toBeInTheDocument();
  });

  it('يستدعي onClose عند زر الإغلاق', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(
      <OccurrencesModal documentTitle="الملف 77" occurrences={[]} onClose={onClose} />,
    );

    const closeButtons = screen.getAllByRole('button', { name: /^إغلاق$/ });
    await user.click(closeButtons[1]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يوسم الوقعة النظامية بشارة «نظامي» ولا يعرضها للوقعة اليدوية', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({ id: 1, source: 'system' }),
          makeOccurrence({ id: 2, source: 'manual' }),
        ]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getAllByText('نظامي')).toHaveLength(1);
  });

  it('يجمع وقعة «تغيير جهة» في قسم خاص بسردها النصي وتاريخ التغيير', () => {
    const narrative = 'تم نقل قيد «وزارة التعليم» (دمشق/الفرع الرئيسي) بموجب قرار إداري رقم 123 بتاريخ 2026-08-01';
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({ id: 1 }),
          makeOccurrence({
            id: 2,
            occurrenceType: 'entity-change',
            occurrenceTypeLabel: 'تغيير جهة',
            source: 'system',
            fileNumber: undefined,
            fileType: undefined,
            year: undefined,
            eventDate: '2026-08-05',
            detailsText: narrative,
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    const section = screen.getByRole('region', { name: 'التغييرات التي وقعت على الجهة العامة' });
    expect(screen.getByRole('heading', { name: 'التغييرات التي وقعت على الجهة العامة' })).toBeInTheDocument();
    expect(screen.getByText(narrative)).toBeInTheDocument();
    expect(screen.getByText('تاريخ التغيير')).toBeInTheDocument();
    expect(screen.getByText(formatDate('2026-08-05'))).toBeInTheDocument();
    // الوقعة العادية تبقى خارج القسم الخاص
    expect(within(section).queryByText('شطب')).not.toBeInTheDocument();
  });

  it('يعرض تسمية وقعة «تغيير جهة» عند غياب السرد النصي بدل كسر العرض', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({
            id: 2,
            occurrenceType: 'entity-change',
            occurrenceTypeLabel: 'تغيير جهة',
            source: 'system',
            fileNumber: undefined,
            fileType: undefined,
            year: undefined,
            eventDate: undefined,
            detailsText: undefined,
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByRole('heading', { name: 'التغييرات التي وقعت على الجهة العامة' })).toBeInTheDocument();
    // التسمية في الشارة وسطر الملخص معًا (احتياطي غياب السرد)
    expect(screen.getAllByText('تغيير جهة')).toHaveLength(2);
    expect(screen.getByText('لا توجد تفاصيل مسجلة')).toBeInTheDocument();
  });
});