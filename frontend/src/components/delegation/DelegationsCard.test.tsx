import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { DelegationsCard } from './DelegationsCard';
import type { DelegationDto } from '../../types';

function delegation(overrides: Partial<DelegationDto> = {}): DelegationDto {
  return {
    id: 1,
    sourceDocumentId: 10,
    sourceDocumentLabel: 'أحمد محمد خالد',
    targetDocumentId: null,
    delegatedCourt: 'محكمة التنفيذ الأولى',
    isExternal: false,
    externalBranchId: null,
    externalBranchName: null,
    delegationDate: '2026-08-01',
    delegationText: '',
    depositBookNumber: 'K-1',
    depositBookDate: '2026-08-02',
    assignedLawyerId: null,
    assignedLawyerName: null,
    returnDate: '',
    status: 'بانتظار رئيس القسم',
    createdAt: '2026-08-01',
    createdByName: 'سامر',
    createdById: 7,
    assets: [{ id: 100, assetKind: 'مركبة', assetLabel: 'مركبة سيارة — لوحة 123', snapshotAdjusted: false }],
    ...overrides,
  };
}

const noop = () => {};

describe('DelegationsCard', () => {
  it('يعرض «تشعبات الملف» مع تفاصيل كل إنابة وحالتها', () => {
    render(
      <DelegationsCard
        delegations={[delegation()]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('تشعبات الملف')).toBeInTheDocument();
    expect(screen.getByText('محكمة التنفيذ الأولى')).toBeInTheDocument();
    expect(screen.getByText('بانتظار رئيس القسم')).toBeInTheDocument();
    expect(screen.getByText('إنابة داخلية')).toBeInTheDocument();
    expect(screen.getByText('مركبة سيارة — لوحة 123')).toBeInTheDocument();
    expect(screen.getByText(/كتاب الإيداع رقم K-1 بتاريخ/)).toBeInTheDocument();
  });

  it('يعرض «إنابة خارجية» مع اسم الفرع المناب', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ isExternal: true, externalBranchName: 'فرع حمص' })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('إنابة خارجية — الفرع المناب: فرع حمص')).toBeInTheDocument();
  });

  it('يعرض المحامي المختص عند اعتماد الإنابة', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ status: 'محالة', assignedLawyerName: 'المحامي هشام' })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('المحامي هشام')).toBeInTheDocument();
  });

  it('يعرض زر «تسطير إنابة» عند الإذن ويستدعي onCreate', () => {
    const onCreate = vi.fn();
    render(
      <DelegationsCard delegations={[]} canCreate onCreate={onCreate} onEdit={noop} onDelete={noop} />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'تسطير إنابة' }));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it('لا يعرض زر «تسطير إنابة» دون إذن، ويعرض رسالة الفراغ', () => {
    render(
      <DelegationsCard delegations={[]} canCreate={false} onCreate={noop} onEdit={noop} onDelete={noop} />,
    );

    expect(screen.queryByRole('button', { name: 'تسطير إنابة' })).not.toBeInTheDocument();
    expect(screen.getByText('لا توجد إنابات مسجلة لهذا الملف')).toBeInTheDocument();
  });

  it('يعرض «تعديل» و«حذف» للإنابة المعلّقة لمحامي المنيب المالك فقط', () => {
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    const d = delegation({ createdById: 7, status: 'بانتظار رئيس القسم' });

    const { unmount } = render(
      <DelegationsCard
        delegations={[d]}
        canCreate={false}
        currentUserId={7}
        onCreate={noop}
        onEdit={onEdit}
        onDelete={onDelete}
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'تعديل' }));
    expect(onEdit).toHaveBeenCalledWith(d);
    fireEvent.click(screen.getByRole('button', { name: 'حذف' }));
    expect(onDelete).toHaveBeenCalledWith(d);

    unmount();
    render(
      <DelegationsCard
        delegations={[d]}
        canCreate={false}
        currentUserId={8}
        onCreate={noop}
        onEdit={onEdit}
        onDelete={onDelete}
      />,
    );
    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف' })).not.toBeInTheDocument();
  });

  it('لا يعرض «تعديل» و«حذف» لإنابة لم تعد معلّقة حتى للمالك', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ createdById: 7, status: 'محالة' })]}
        canCreate={false}
        currentUserId={7}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف' })).not.toBeInTheDocument();
  });
});
