import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LawyersTable } from './LawyersTable';

describe('LawyersTable', () => {
  it('يعرض الجدول مع حاشية الوسم عند وجود نقاط محسوبة بتاريخ الإدخال', () => {
    render(
      <LawyersTable
        showTable
        branchId={1}
        lawyers={[
          {
            lawyerId: 1,
            lawyerName: 'محامي دمشق',
            totalCount: 2,
            points: [{ year: 2026, month: 5, count: 2, fromCreatedAtCount: 1 }],
          },
        ]}
      />,
    );

    expect(screen.getByRole('heading', { name: 'إحصائيات محامي الفرع' })).toBeInTheDocument();
    expect(screen.getByText('محامي دمشق')).toBeInTheDocument();
    expect(screen.getByText(/محسوبة\s*بتاريخ الإدخال لغياب تاريخ قيدها أو تعذّر تحليله/)).toBeInTheDocument();
  });

  it('يعرض رسالة الفرع الفارغ ويخفي الحاشية عند غياب الوسم', () => {
    render(<LawyersTable showTable branchId={1} lawyers={[]} />);

    expect(screen.getByText('لا يوجد محامون في هذا الفرع')).toBeInTheDocument();
    expect(screen.queryByText(/محسوبة\s*بتاريخ الإدخال/)).not.toBeInTheDocument();
  });

  it('يعرض رسالة اختيار الفرع عند غياب المعرف ولا شيء عند التعطيل', () => {
    const { unmount } = render(<LawyersTable showTable branchId={null} lawyers={[]} />);
    expect(screen.getByText('اختر فرعًا لعرض إحصائيات محامي الفرع')).toBeInTheDocument();
    unmount();

    const { container } = render(<LawyersTable showTable={false} branchId={1} lawyers={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('يعرض خطأ الجلب (role=alert) بدل عبارة «لا يوجد محامون» المضللة', () => {
    render(<LawyersTable showTable branchId={1} lawyers={[]} error="تعذّر جلب جدول المحامين" />);

    expect(screen.getByRole('alert')).toHaveTextContent('تعذّر جلب جدول المحامين');
    expect(screen.queryByText('لا يوجد محامون في هذا الفرع')).not.toBeInTheDocument();
  });

  it('يدعم رسالة غياب فرع مخصصة (رئيس بلا فرع — لا يملك منتقي فرع)', () => {
    render(<LawyersTable showTable branchId={null} lawyers={[]} noBranchMessage="رسالة مخصصة" />);

    expect(screen.getByText('رسالة مخصصة')).toBeInTheDocument();
    expect(screen.queryByText('اختر فرعًا لعرض إحصائيات محامي الفرع')).not.toBeInTheDocument();
  });
});
