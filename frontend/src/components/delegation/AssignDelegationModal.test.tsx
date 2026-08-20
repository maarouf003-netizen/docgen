import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AssignDelegationModal from './AssignDelegationModal';
import type { DelegationDto, LawyerListItem } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as { response?: { data?: { message?: string } } };
    return e?.response?.data?.message ?? 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../../api/client';

function pendingDelegation(overrides: Partial<DelegationDto> = {}): DelegationDto {
  return {
    id: 9,
    sourceDocumentId: 10,
    sourceDocumentLabel: 'أحمد خالد الخطيب',
    targetDocumentId: null,
    delegatedCourt: 'دائرة تنفيذ حلب',
    isExternal: false,
    externalBranchId: null,
    externalBranchName: null,
    delegationDate: '2026-08-01',
    delegationText: 'الإنابة على العقار المذكور',
    depositBookNumber: '',
    depositBookDate: '',
    assignedLawyerId: null,
    assignedLawyerName: null,
    returnDate: '',
    status: 'بانتظار رئيس القسم',
    createdAt: '2026-08-01',
    createdByName: 'سامر',
    createdById: 7,
    assets: [{ id: 100, assetKind: 'عقار', assetLabel: 'عقار — المزة 77', snapshotAdjusted: false }],
    ...overrides,
  };
}

function lawyer(id: number, fullName: string): LawyerListItem {
  return { id, username: `lawyer${id}`, fullName, isActive: true, branchId: 1, branchName: 'دمشق' };
}

const noop = () => {};

describe('AssignDelegationModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [lawyer(1, 'المحامي أحمد'), lawyer(2, 'المحامية سلمى')],
    });
  });

  it('يعرض تفاصيل الطلب وقائمة محامي الفرع ويحمّلها من النهاية', async () => {
    render(
      <AssignDelegationModal delegation={pendingDelegation()} onClose={noop} onAssigned={noop} />,
    );

    expect(screen.getByRole('dialog', { name: 'اعتماد الإنابة' })).toBeInTheDocument();
    expect(screen.getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('دائرة تنفيذ حلب')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/users/lawyers');
    expect(
      await screen.findByRole('option', { name: 'المحامي أحمد' }),
    ).toBeInTheDocument();
  });

  it('يُظهر رسالة الفرع المناب للإنابة الخارجية', async () => {
    render(
      <AssignDelegationModal
        delegation={pendingDelegation({
          isExternal: true,
          externalBranchName: 'فرع اللاذقية',
        })}
        onClose={noop}
        onAssigned={noop}
      />,
    );

    expect(
      screen.getByText(/إنابة خارجية — سيُنشأ الملف المناب في فرع اللاذقية/),
    ).toBeInTheDocument();
  });

  it('يمنع الاعتماد دون اختيار محامٍ', async () => {
    const user = userEvent.setup();
    render(
      <AssignDelegationModal delegation={pendingDelegation()} onClose={noop} onAssigned={noop} />,
    );

    await user.click(screen.getByRole('button', { name: 'اعتماد وتكليف المحامي' }));
    expect(screen.getByRole('alert').textContent).toBe('اختر المحامي المختص');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرسل الاعتماد بالمحامي المختص ويبلغ النجاح باسمه', async () => {
    const user = userEvent.setup();
    const onAssigned = vi.fn();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(
      <AssignDelegationModal delegation={pendingDelegation()} onClose={noop} onAssigned={onAssigned} />,
    );

    await screen.findByRole('option', { name: 'المحامية سلمى' });
    await user.selectOptions(
      screen.getByLabelText('المحامي المختص'),
      String(2),
    );
    await user.click(screen.getByRole('button', { name: 'اعتماد وتكليف المحامي' }));

    expect(api.post).toHaveBeenCalledWith('/delegations/9/assign', { assignedLawyerId: 2 });
    expect(onAssigned).toHaveBeenCalledWith('المحامية سلمى');
  });

  it('يعرض رسالة خطأ الخادم عند فشل الاعتماد', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      response: { data: { message: 'لا يمكنك اعتماد هذه الإنابة — ليست ضمن فرعك' } },
    });
    render(
      <AssignDelegationModal delegation={pendingDelegation()} onClose={noop} onAssigned={noop} />,
    );

    await screen.findByRole('option', { name: 'المحامي أحمد' });
    await user.selectOptions(screen.getByLabelText('المحامي المختص'), String(1));
    await user.click(screen.getByRole('button', { name: 'اعتماد وتكليف المحامي' }));

    expect(
      await screen.findByRole('alert'),
    ).toHaveTextContent('لا يمكنك اعتماد هذه الإنابة — ليست ضمن فرعك');
  });
});