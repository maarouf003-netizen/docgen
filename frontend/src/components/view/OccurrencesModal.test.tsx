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

  it('يفصل وقعة تغيير الحالة عن الشطب في قسم مستقل مع تفاصيلها', () => {
    const statusLine = 'تريث بموجب كتاب التريث رقم 33 بتاريخ 3/3/2024';
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({ id: 1 }),
          makeOccurrence({
            id: 2,
            occurrenceType: 'deferred',
            occurrenceTypeLabel: 'تريث',
            source: 'system',
            fileNumber: undefined,
            fileType: undefined,
            year: undefined,
            eventDate: '2026-08-01',
            details: { tarithNumber: '33', tarithDate: '3/3/2024' },
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByRole('heading', { name: 'الشطوبات' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'تغييرات الحالة' })).toBeInTheDocument();
    const struckSection = screen.getByRole('region', { name: 'الشطوبات' });
    const statusSection = screen.getByRole('region', { name: 'تغييرات الحالة' });
    expect(within(struckSection).getByText(/تم شطب الملف رقم 77/)).toBeInTheDocument();
    expect(within(struckSection).queryByText(statusLine)).not.toBeInTheDocument();
    expect(within(statusSection).getByText(statusLine)).toBeInTheDocument();
    expect(within(statusSection).getByText('رقم كتاب التريث')).toBeInTheDocument();
    expect(within(statusSection).queryByText(/تم شطب الملف/)).not.toBeInTheDocument();
  });

  it('لا يعرض قسم «الشطوبات» عند وجود وقعة تغيير حالة فقط', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({
            id: 2,
            occurrenceType: 'deferred',
            occurrenceTypeLabel: 'تريث',
            source: 'system',
            fileNumber: undefined,
            fileType: undefined,
            year: undefined,
            eventDate: '2026-08-01',
            details: { tarithNumber: '33', tarithDate: '3/3/2024' },
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByRole('heading', { name: 'تغييرات الحالة' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'الشطوبات' })).not.toBeInTheDocument();
  });

  it('يعرض وقعة «محال الى البداية» في قسم تغييرات الحالة مع كتب المطالعة والإحالة', () => {
    const statusLine =
      'محال الى البداية بموجب كتاب المطالعة بعدم وجود أموال رقم 5 بتاريخ 2026-01-08';
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({
            id: 2,
            occurrenceType: 'referred-to-start',
            occurrenceTypeLabel: 'محال الى البداية',
            source: 'system',
            fileNumber: undefined,
            fileType: undefined,
            year: undefined,
            eventDate: '2026-01-10',
            details: {
              noFundsDemandNumber: '5',
              noFundsDemandDate: '2026-01-08',
              startReferralNumber: '6',
              startReferralDate: '2026-02-01',
            },
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByRole('heading', { name: 'تغييرات الحالة' })).toBeInTheDocument();
    expect(screen.getByText(statusLine)).toBeInTheDocument();
    expect(screen.getByText('رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها')).toBeInTheDocument();
    expect(screen.getByText('تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها')).toBeInTheDocument();
    expect(screen.getByText('رقم كتاب الإحالة')).toBeInTheDocument();
    expect(screen.getByText('تاريخ كتاب الإحالة')).toBeInTheDocument();
  });

  it('يعرض وقعة العودة من «محال» بالسرد النصي ومفاتيح التجديد/الشطب دون حقول سير', () => {
    const narrative = 'أعيد السير به بعد موافاتنا بأموال للتنفيذ عليها وجدد الملف برقم 999';
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({
            id: 2,
            occurrenceType: 'revert',
            occurrenceTypeLabel: 'تراجع',
            source: 'system',
            fileNumber: undefined,
            fileType: undefined,
            year: undefined,
            eventDate: '2026-03-01',
            details: {
              revertNarration: narrative,
              struckOffDate: '2026-03-01',
              renewalFileNumber: '999',
              renewalFileType: 'حقوق',
              renewalDate: '2026-03-01',
              renewalYear: '2026',
            },
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByText(narrative)).toBeInTheDocument();
    expect(screen.getByText('رقم الملف الجديد')).toBeInTheDocument();
    expect(screen.getByText('سنة الإعادة')).toBeInTheDocument();
    expect(screen.getByText('تاريخ شطب الملف السابق')).toBeInTheDocument();
    expect(screen.queryByText('رقم كتاب الجهة العامة بالسير بالملف')).not.toBeInTheDocument();
  });

  it('يعرض تفاصيل الإحالة الجماعية: الدائرتان والأرقام القديمة وذيل إنابة المنيب عند وجوده', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({
            id: 2,
            occurrenceType: 'circuit-referred',
            occurrenceTypeLabel: 'إحالة نقل دائرة',
            source: 'system',
            eventDate: '2026-01-05',
            fileNumber: '66',
            fileType: 'صلح',
            year: 2026,
            fromCircuitName: 'دائرة المصدر',
            toCircuitName: 'دائرة الوجهة',
            details: {
              fromCircuit: 'دائرة المصدر',
              toCircuit: 'دائرة الوجهة',
              oldNumber: '66',
              oldType: 'صلح',
              oldYear: '2026',
              delegationCircuitChange: 'من «دائرة أ» إلى «دائرة ب»',
            },
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    const section = screen.getByRole('region', { name: 'تغييرات الحالة' });
    expect(within(section).getByText('الدائرة المصدر')).toBeInTheDocument();
    expect(within(section).getByText('دائرة المصدر')).toBeInTheDocument();
    expect(within(section).getByText('الدائرة الهدف')).toBeInTheDocument();
    expect(within(section).getByText('دائرة الوجهة')).toBeInTheDocument();
    expect(within(section).getByText('الرقم القديم')).toBeInTheDocument();
    expect(within(section).getByText('66')).toBeInTheDocument();
    expect(within(section).getByText('النوع القديم')).toBeInTheDocument();
    expect(within(section).getByText('صلح')).toBeInTheDocument();
    expect(within(section).getByText('السنة القديمة')).toBeInTheDocument();
    expect(within(section).getByText('2026')).toBeInTheDocument();
    expect(within(section).getByText('تغيّر دائرة إنابة الملف المنيب')).toBeInTheDocument();
    expect(within(section).getByText('من «دائرة أ» إلى «دائرة ب»')).toBeInTheDocument();
    expect(within(section).getByText('تم نقل هذا الملف رقم 66 نوع صلح لعام 2026 من دائرة «دائرة المصدر» إلى دائرة «دائرة الوجهة» — وتغيّرت بذلك دائرة إنابة الملف المنيب: من «دائرة أ» إلى «دائرة ب»')).toBeInTheDocument();
  });

  it('يعرض تفاصيل التبديل من التعديل العادي (via=edit) بمسمّى «رقم الملف عند التبديل»', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({
            id: 2,
            occurrenceType: 'circuit-referred',
            occurrenceTypeLabel: 'إحالة نقل دائرة',
            source: 'system',
            fileNumber: '101',
            fileType: 'حقوق',
            year: 2026,
            fromCircuitName: 'دائرة السابق',
            toCircuitName: 'دائرة الجديد',
            details: { fromCircuit: 'دائرة السابق', toCircuit: 'دائرة الجديد', via: 'edit' },
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    const section = screen.getByRole('region', { name: 'تغييرات الحالة' });
    expect(within(section).getByText('رقم الملف عند التبديل')).toBeInTheDocument();
    expect(within(section).getByText('101')).toBeInTheDocument();
    expect(within(section).getByText('نوع الملف عند التبديل')).toBeInTheDocument();
    expect(within(section).getByText('سنة التبديل')).toBeInTheDocument();
    expect(within(section).queryByText('الرقم القديم')).not.toBeInTheDocument();
    expect(within(section).queryByText('تغيّر دائرة إنابة الملف المنيب')).not.toBeInTheDocument();
  });

  it('يعرض تفاصيل نقلي الطرف المقابل بمسمّيات الأرقام المخصصة', () => {
    render(
      <OccurrencesModal
        documentTitle="الملف 77"
        occurrences={[
          makeOccurrence({
            id: 2,
            occurrenceType: 'source-file-moved',
            occurrenceTypeLabel: 'نقل الملف المنيب',
            source: 'system',
            fileNumber: '66',
            fileType: 'صلح',
            year: 2026,
            fromCircuitName: 'دائرة المصدر',
            toCircuitName: 'دائرة الوجهة',
            details: { fromCircuit: 'دائرة المصدر', toCircuit: 'دائرة الوجهة' },
          }),
          makeOccurrence({
            id: 3,
            occurrenceType: 'mirror-file-moved',
            occurrenceTypeLabel: 'نقل الملف المناب',
            source: 'system',
            fileNumber: '150',
            fileType: 'حقوق',
            year: 2025,
            fromCircuitName: 'دائرة الوجهة',
            toCircuitName: 'دائرة الجديدة',
            details: { fromCircuit: 'دائرة الوجهة', toCircuit: 'دائرة الجديدة' },
          }),
        ]}
        onClose={vi.fn()}
      />,
    );

    const section = screen.getByRole('region', { name: 'تغييرات الحالة' });
    expect(within(section).getByText('رقم الملف المنيب')).toBeInTheDocument();
    expect(within(section).getByText('66')).toBeInTheDocument();
    // التسميتان المشتركتان بين البطاقتين (كل بطاقة تحمل النوعين)
    expect(within(section).getAllByText('نوعه')).toHaveLength(2);
    expect(within(section).getAllByText('سنة قيده')).toHaveLength(2);
    expect(within(section).getByText('رقم الملف المناب')).toBeInTheDocument();
    expect(within(section).getByText('150')).toBeInTheDocument();
    expect(within(section).getAllByText('الدائرة المصدر')).toHaveLength(2);
    expect(within(section).getAllByText('الدائرة الهدف')).toHaveLength(2);
    expect(within(section).getByText('تم نقل الملف المنيب رقم 66 نوع صلح لعام 2026 من دائرة «دائرة المصدر» إلى دائرة «دائرة الوجهة»')).toBeInTheDocument();
    expect(within(section).getByText('تم نقل الملف المناب رقم 150 نوع حقوق لعام 2025 من دائرة «دائرة الوجهة» إلى دائرة «دائرة الجديدة»')).toBeInTheDocument();
  });
});
