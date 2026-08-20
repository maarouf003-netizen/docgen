import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RegisterDelegationModal from './RegisterDelegationModal';
import type { DelegationDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as { response?: { data?: { message?: string } } };
    return e?.response?.data?.message ?? 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../../api/client';

function assignedDelegation(): DelegationDto {
  return {
    id: 9,
    sourceDocumentId: 10,
    sourceDocumentLabel: 'أحمد خالد الخطيب',
    targetDocumentId: 5,
    delegatedCourt: 'دائرة تنفيذ حلب',
    isExternal: false,
    externalBranchId: null,
    externalBranchName: null,
    delegationDate: '2026-08-01',
    delegationText: '',
    depositBookNumber: '',
    depositBookDate: '',
    assignedLawyerId: 2,
    assignedLawyerName: 'المحامي خالد',
    returnDate: '',
    status: 'محالة',
    createdAt: '2026-08-01',
    createdByName: 'سامر',
    createdById: 7,
    assets: [{ id: 100, assetKind: 'عقار', assetLabel: 'عقار — المزة 77', snapshotAdjusted: false }],
  };
}

const noop = () => {};

describe('RegisterDelegationModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('يعرض حقول التسجيل مع تفاصيل الإنابة', () => {
    render(
      <RegisterDelegationModal delegation={assignedDelegation()} onClose={noop} onRegistered={noop} />,
    );

    expect(screen.getByRole('dialog', { name: 'تسجيل الإنابة أصولًا' })).toBeInTheDocument();
    expect(screen.getByLabelText('رقم أساس الإنابة')).toBeInTheDocument();
    expect(screen.getByLabelText('سنة قيد الإنابة')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ قيد الإنابة')).toBeInTheDocument();
    expect(screen.getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('المحامي خالد')).toBeInTheDocument();
  });

  it('يتطلب كل الحقول الثلاثة', async () => {
    const user = userEvent.setup();
    render(
      <RegisterDelegationModal delegation={assignedDelegation()} onClose={noop} onRegistered={noop} />,
    );

    await user.click(screen.getByRole('button', { name: 'تسجيل أصولًا' }));
    expect(screen.getByRole('alert').textContent).toBe('رقم أساس الإنابة مطلوب');

    await user.type(screen.getByLabelText('رقم أساس الإنابة'), '890');
    await user.click(screen.getByRole('button', { name: 'تسجيل أصولًا' }));
    expect(screen.getByRole('alert').textContent).toBe('سنة قيد الإنابة مطلوبة');

    await user.type(screen.getByLabelText('سنة قيد الإنابة'), '2026');
    await user.click(screen.getByRole('button', { name: 'تسجيل أصولًا' }));
    expect(screen.getByRole('alert').textContent).toBe('تاريخ قيد الإنابة مطلوب');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرسل التسجيل بأرقام عربية محوَّلة ويُبلغ النجاح', async () => {
    const user = userEvent.setup();
    const onRegistered = vi.fn();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(
      <RegisterDelegationModal
        delegation={assignedDelegation()}
        onClose={noop}
        onRegistered={onRegistered}
      />,
    );

    await user.type(screen.getByLabelText('رقم أساس الإنابة'), '٨٩٠');
    await user.type(screen.getByLabelText('سنة قيد الإنابة'), '٢٠٢٦');
    await user.type(screen.getByLabelText('تاريخ قيد الإنابة'), '٥/٨/٢٠٢٦');
    await user.click(screen.getByRole('button', { name: 'تسجيل أصولًا' }));

    expect(api.post).toHaveBeenCalledWith('/delegations/9/register', {
      fileNumber: '890',
      fileYear: '2026',
      fileRegistrationDate: '5/8/2026',
    });
    expect(onRegistered).toHaveBeenCalled();
  });

  it('يعرض رسالة خطأ الخادم عند فشل التسجيل', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      response: { data: { message: 'لا يمكن تسجيل إنابة لم تُعتمد' } },
    });
    render(
      <RegisterDelegationModal delegation={assignedDelegation()} onClose={noop} onRegistered={noop} />,
    );

    await user.type(screen.getByLabelText('رقم أساس الإنابة'), '890');
    await user.type(screen.getByLabelText('سنة قيد الإنابة'), '2026');
    await user.type(screen.getByLabelText('تاريخ قيد الإنابة'), '5/8/2026');
    await user.click(screen.getByRole('button', { name: 'تسجيل أصولًا' }));

    expect(
      await screen.findByRole('alert'),
    ).toHaveTextContent('لا يمكن تسجيل إنابة لم تُعتمد');
  });
});