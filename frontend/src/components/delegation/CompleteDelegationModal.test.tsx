import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CompleteDelegationModal from './CompleteDelegationModal';
import type { DelegationDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as { response?: { data?: { message?: string } } };
    return e?.response?.data?.message ?? 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../../api/client';

function registeredDelegation(): DelegationDto {
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
    status: 'مسجلة أصولًا',
    createdAt: '2026-08-01',
    createdByName: 'سامر',
    createdById: 7,
    assets: [
      { id: 100, assetKind: 'عقار', assetLabel: 'عقار — المزة 77', snapshotAdjusted: false },
      { id: 101, assetKind: 'مركبة', assetLabel: 'مركبة — لوحة 123', snapshotAdjusted: false },
    ],
  };
}

const noop = () => {};

describe('CompleteDelegationModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('يعرض حقلي تاريخ الإعادة وتاريخ قرار الإحالة القطعية وحقل بدل مبيع لكل أصل', () => {
    render(
      <CompleteDelegationModal delegation={registeredDelegation()} onClose={noop} onCompleted={noop} />,
    );

    expect(screen.getByRole('dialog', { name: 'إتمام الإنابة' })).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ إعادة الملف للدائرة المنيبة')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ قرار الإحالة القطعية')).toBeInTheDocument();
    expect(screen.getByText('عقار — المزة 77')).toBeInTheDocument();
    expect(screen.getByText('مركبة — لوحة 123')).toBeInTheDocument();
    expect(screen.getAllByLabelText('بدل المبيع بالليرة السورية')).toHaveLength(2);
  });

  it('يعيد بدل المبيع المحفوظ مسبقًا في حقل أصله', () => {
    render(
      <CompleteDelegationModal
        delegation={{
          ...registeredDelegation(),
          assets: [
            { id: 100, assetKind: 'عقار', assetLabel: 'عقار — المزة 77', salePrice: 750000, snapshotAdjusted: false },
          ],
        }}
        onClose={noop}
        onCompleted={noop}
      />,
    );

    expect(screen.getByLabelText('بدل المبيع بالليرة السورية')).toHaveValue('750000');
  });

  it('يتطلب بدل المبيع لكل أصل', async () => {
    const user = userEvent.setup();
    const onCompleted = vi.fn();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(
      <CompleteDelegationModal
        delegation={registeredDelegation()}
        onClose={noop}
        onCompleted={onCompleted}
      />,
    );

    await user.type(screen.getByLabelText('تاريخ إعادة الملف للدائرة المنيبة'), '10/8/2026');
    await user.type(screen.getByLabelText('تاريخ قرار الإحالة القطعية'), '12/8/2026');
    await user.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));

    expect(screen.getByRole('alert').textContent).toContain('بدل المبيع مطلوب');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يتطلب تاريخ الإعادة قبل أي شيء', async () => {
    const user = userEvent.setup();
    render(
      <CompleteDelegationModal delegation={registeredDelegation()} onClose={noop} onCompleted={noop} />,
    );

    await user.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));
    expect(screen.getByRole('alert').textContent).toBe(
      'تاريخ إعادة الملف للدائرة المنيبة مطلوب',
    );
  });

  it('يتطلب تاريخ قرار الإحالة القطعية عند اكتمال باقي الحقول', async () => {
    const user = userEvent.setup();
    render(
      <CompleteDelegationModal delegation={registeredDelegation()} onClose={noop} onCompleted={noop} />,
    );

    await user.type(screen.getByLabelText('تاريخ إعادة الملف للدائرة المنيبة'), '10/8/2026');
    await user.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));
    expect(screen.getByRole('alert').textContent).toBe('تاريخ قرار الإحالة القطعية مطلوب');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرفض بدل المبيع الصفري أو غير الرقمي', async () => {
    const user = userEvent.setup();
    render(
      <CompleteDelegationModal delegation={registeredDelegation()} onClose={noop} onCompleted={noop} />,
    );

    const priceInputs = screen.getAllByLabelText('بدل المبيع بالليرة السورية');
    await user.type(screen.getByLabelText('تاريخ إعادة الملف للدائرة المنيبة'), '10/8/2026');
    await user.type(screen.getByLabelText('تاريخ قرار الإحالة القطعية'), '12/8/2026');
    await user.type(priceInputs[0], '0');
    await user.type(priceInputs[1], 'abc');
    await user.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));

    expect(screen.getByRole('alert').textContent).toContain('بدل المبيع غير صالح');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرسل الإتمام بتاريخ الإعادة وبدل مبيع كل أصل (بأرقام عربية محوَّلة) ويُبلغ النجاح', async () => {
    const user = userEvent.setup();
    const onCompleted = vi.fn();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(
      <CompleteDelegationModal
        delegation={registeredDelegation()}
        onClose={noop}
        onCompleted={onCompleted}
      />,
    );

    const priceInputs = screen.getAllByLabelText('بدل المبيع بالليرة السورية');
    await user.type(screen.getByLabelText('تاريخ إعادة الملف للدائرة المنيبة'), '١٠/٨/٢٠٢٦');
    await user.type(screen.getByLabelText('تاريخ قرار الإحالة القطعية'), '١٢/٨/٢٠٢٦');
    await user.type(priceInputs[0], '٧٥٠٠٠٠');
    await user.type(priceInputs[1], '1,250,000');
    await user.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));

    expect(api.post).toHaveBeenCalledWith('/delegations/9/complete', {
      returnDate: '10/8/2026',
      sales: [
        { delegationAssetId: 100, salePrice: 750000 },
        { delegationAssetId: 101, salePrice: 1250000 },
      ],
      forcedExecutionDate: '12/8/2026',
    });
    expect(onCompleted).toHaveBeenCalled();
  });

  it('يعرض رسالة خطأ الخادم عند فشل الإتمام', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      response: { data: { message: 'لا يمكن إتمام إنابة لم تُسجَّل أصولًا' } },
    });
    render(
      <CompleteDelegationModal delegation={registeredDelegation()} onClose={noop} onCompleted={noop} />,
    );

    const priceInputs = screen.getAllByLabelText('بدل المبيع بالليرة السورية');
    await user.type(screen.getByLabelText('تاريخ إعادة الملف للدائرة المنيبة'), '10/8/2026');
    await user.type(screen.getByLabelText('تاريخ قرار الإحالة القطعية'), '12/8/2026');
    await user.type(priceInputs[0], '750000');
    await user.type(priceInputs[1], '1250000');
    await user.click(screen.getByRole('button', { name: 'إتمام الإنابة' }));

    expect(
      await screen.findByRole('alert'),
    ).toHaveTextContent('لا يمكن إتمام إنابة لم تُسجَّل أصولًا');
  });
});