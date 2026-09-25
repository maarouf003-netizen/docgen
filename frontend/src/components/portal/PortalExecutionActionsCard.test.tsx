import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PortalExecutionActionsCard } from './PortalExecutionActionsCard';
import type { PortalExecutionActionDto } from '../../types';

function action(overrides: Partial<PortalExecutionActionDto> = {}): PortalExecutionActionDto {
  return {
    id: 1,
    text: 'كتابة إخطار للجهة العامة',
    actionDate: '1/8/2026',
    createdByName: 'المحامي سامر',
    createdAt: '2026-08-01T10:00:00Z',
    ...overrides,
  };
}

describe('PortalExecutionActionsCard', () => {
  it('يعرض النص المعقّم والتاريخ الحر والمحامي المُدخل لكل إجراء (ق7)', () => {
    render(
      <PortalExecutionActionsCard
        actions={[
          action({ id: 2, text: '<b>مراجعة الملف</b>', actionDate: '5/9/2026', createdByName: 'المحامية لينا' }),
          action({ id: 1, actionDate: '', createdByName: undefined }),
        ]}
      />,
    );

    // النص الغني معقّم كـ HTML آمن — المحتوى النصي يظهر بلا وسم.
    expect(screen.getAllByText('مراجعة الملف').length).toBeGreaterThan(0);
    expect(screen.getByText('5/9/2026')).toBeInTheDocument();
    expect(screen.getByText('· المحامية لينا')).toBeInTheDocument();
    expect(screen.getByText('كتابة إخطار للجهة العامة')).toBeInTheDocument();
    // إجراء بلا تاريخ وبلا محامٍ: يُعرض «—» فقط.
    expect(screen.getAllByText('—').length).toBeGreaterThan(0);
    expect(screen.queryByText('· المحامي سامر')).not.toBeInTheDocument();
  });

  it('يعرض رسالة الفراغ حين لا توجد إجراءات تنفيذية', () => {
    render(<PortalExecutionActionsCard actions={[]} />);
    expect(screen.getByText('لا توجد إجراءات تنفيذية')).toBeInTheDocument();
  });

  it('يعرض حالة التحميل عند أول جلب بلا بيانات', () => {
    render(<PortalExecutionActionsCard actions={[]} loading />);
    expect(screen.getByText(/جارِ تحميل الإجراءات/)).toBeInTheDocument();
  });
});