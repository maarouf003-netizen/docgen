import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RowTriple } from './RowTriple';

describe('RowTriple', () => {
  it('يعرض ثلاثة حقول بجانب بعضها', () => {
    render(
      <RowTriple
        firstLabel="رقم كتاب الجهة العامة"
        firstValue="و-77"
        secondLabel="تاريخ كتاب الجهة العامة"
        secondValue="2026-07-30"
        thirdLabel="رقم تحت رفع"
        thirdValue="ت-55"
      />,
    );

    expect(screen.getByText('رقم كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByText('و-77')).toBeInTheDocument();
    expect(screen.getByText('تاريخ كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByText('2026-07-30')).toBeInTheDocument();
    expect(screen.getByText('رقم تحت رفع')).toBeInTheDocument();
    expect(screen.getByText('ت-55')).toBeInTheDocument();
  });

  it('يعرض الشرطة للقيم الفارغة افتراضياً', () => {
    render(
      <RowTriple
        firstLabel="رقم كتاب الجهة العامة"
        firstValue="و-77"
        secondLabel="تاريخ كتاب الجهة العامة"
        secondValue=""
        thirdLabel="رقم تحت رفع"
        thirdValue={null}
      />,
    );

    expect(screen.getByText('و-77')).toBeInTheDocument();
    expect(screen.getAllByText('—')).toHaveLength(2);
  });

  it('يخفي كل خلية فارغة عندما يكون showEmpty=false وتختفي البطاقة كلها إذا كانت كلها فارغة', () => {
    const { unmount } = render(
      <RowTriple
        firstLabel="رقم كتاب الجهة العامة"
        firstValue="و-77"
        secondLabel="تاريخ كتاب الجهة العامة"
        secondValue=""
        thirdLabel="رقم تحت رفع"
        thirdValue=""
        firstShowEmpty={false}
        secondShowEmpty={false}
        thirdShowEmpty={false}
      />,
    );

    expect(screen.getByText('رقم كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByText('و-77')).toBeInTheDocument();
    expect(screen.queryByText('تاريخ كتاب الجهة العامة')).not.toBeInTheDocument();
    expect(screen.queryByText('رقم تحت رفع')).not.toBeInTheDocument();
    unmount();

    render(
      <RowTriple
        firstLabel="رقم كتاب الجهة العامة"
        firstValue=""
        secondLabel="تاريخ كتاب الجهة العامة"
        secondValue={null}
        thirdLabel="رقم تحت رفع"
        thirdValue={undefined}
        firstShowEmpty={false}
        secondShowEmpty={false}
        thirdShowEmpty={false}
      />,
    );

    expect(screen.queryByText('رقم كتاب الجهة العامة')).not.toBeInTheDocument();
  });
});
