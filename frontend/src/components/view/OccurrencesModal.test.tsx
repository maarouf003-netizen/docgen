import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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
});