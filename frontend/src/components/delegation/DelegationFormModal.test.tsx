import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DelegationFormModal from './DelegationFormModal';
import type { AssetDto, BranchDto, DelegationDto } from '../../types';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as { response?: { data?: { message?: string } } };
    return e?.response?.data?.message ?? 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../../api/client';

function vehicleAsset(id: number, plate: string): AssetDto {
  return { id, assetKind: 'مركبة', plateNumber: plate } as AssetDto;
}

function branch(id: number, name: string): BranchDto {
  return { id, name, code: String(id) };
}

function pendingDelegation(): DelegationDto {
  return {
    id: 9,
    sourceDocumentId: 10,
    sourceDocumentLabel: 'أحمد',
    targetDocumentId: null,
    delegatedCourt: 'محكمة التنفيذ الأولى',
    isExternal: false,
    externalBranchId: null,
    externalBranchName: null,
    delegationDate: '2026-08-01',
    delegationText: 'نص سابق',
    depositBookNumber: '',
    depositBookDate: '',
    sendBookNumber: '',
    sendBookDate: '',
    assignedLawyerId: null,
    assignedLawyerName: null,
    returnDate: '',
    status: 'بانتظار رئيس القسم',
    createdAt: '2026-08-01',
    createdByName: 'سامر',
    createdById: 7,
    assets: [
      { id: 100, assetKind: 'مركبة', assetLabel: 'مركبة لوحة 77' },
      { id: 101, assetKind: 'متجر', assetLabel: 'متجر سجل رقم 3' },
    ],
  };
}

const noop = () => {};

describe('DelegationFormModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [branch(2, 'فرع حمص'), branch(3, 'فرع اللاذقية')],
    });
  });

  it('يعرض حقول النموذج مع الملف المنيب ويحمّل الفروع للاختيار الخارجي', async () => {
    render(
      <DelegationFormModal
        documentId={10}
        documentTitle="أحمد محمد خالد"
        assets={[vehicleAsset(1, '77')]}
        onClose={noop}
        onSaved={noop}
      />,
    );

    expect(screen.getByRole('dialog', { name: 'تسطير إنابة' })).toBeInTheDocument();
    expect(screen.getByText('أحمد محمد خالد')).toBeInTheDocument();
    expect(screen.getByLabelText('الدائرة المنابة')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ الإنابة')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/branches');
  });

  it('يرفض الإرسال دون الدائرة المنابة دون الاتصال بالخادم', async () => {
    const user = userEvent.setup();
    render(
      <DelegationFormModal
        documentId={10}
        assets={[vehicleAsset(1, '77')]}
        onClose={noop}
        onSaved={noop}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'تسطير الإنابة' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('الدائرة المنابة مطلوبة');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرفض الإرسال دون تاريخ الإنابة', async () => {
    const user = userEvent.setup();
    render(
      <DelegationFormModal
        documentId={10}
        assets={[vehicleAsset(1, '77')]}
        onClose={noop}
        onSaved={noop}
      />,
    );

    await user.type(screen.getByLabelText('الدائرة المنابة'), 'محكمة التنفيذ الأولى');
    await user.click(screen.getByRole('button', { name: 'تسطير الإنابة' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('تاريخ الإنابة مطلوب');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرسل إنابة داخلية بالأرقام الموحّدة (عربية→لاتينية) والأصول المختارة', async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    render(
      <DelegationFormModal
        documentId={10}
        assets={[vehicleAsset(1, '77'), vehicleAsset(2, '99')]}
        onClose={noop}
        onSaved={onSaved}
      />,
    );

    await user.type(screen.getByLabelText('الدائرة المنابة'), 'محكمة التنفيذ الأولى');
    await user.type(screen.getByLabelText('تاريخ الإنابة'), '١/٨/٢٠٢٦');
    await user.type(screen.getByLabelText('نص الإنابة'), 'لبيع الأموال بالمزاد');
    await user.click(screen.getByLabelText('مركبة لوحة 77'));
    await user.click(screen.getByRole('button', { name: 'تسطير الإنابة' }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/documents/10/delegations', {
        delegatedCourt: 'محكمة التنفيذ الأولى',
        isExternal: false,
        externalBranchId: null,
        delegationDate: '1/8/2026',
        delegationText: 'لبيع الأموال بالمزاد',
        depositBookNumber: null,
        depositBookDate: null,
        sendBookNumber: null,
        sendBookDate: null,
        assetIds: [1],
      }),
    );
    expect(onSaved).toHaveBeenCalledTimes(1);
  });

  it('يفرض اختيار الفرع المناب للإنابة الخارجية ثم يرسله', async () => {
    const user = userEvent.setup();
    render(
      <DelegationFormModal
        documentId={10}
        assets={[vehicleAsset(1, '77')]}
        onClose={noop}
        onSaved={noop}
      />,
    );

    await user.type(screen.getByLabelText('الدائرة المنابة'), 'محكمة التنفيذ الأولى');
    await user.type(screen.getByLabelText('تاريخ الإنابة'), '1/8/2026');
    await user.click(screen.getByLabelText('إنابة إلى فرع في محافظة أخرى'));
    await user.click(screen.getByRole('button', { name: 'تسطير الإنابة' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'الإنابة الخارجية تتطلب تحديد الفرع المناب',
    );

    await user.selectOptions(screen.getByLabelText('الفرع المناب'), '2');
    await user.click(screen.getByRole('button', { name: 'تسطير الإنابة' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('يجب اختيار الأموال موضوع الإنابة');

    await user.click(screen.getByLabelText('مركبة لوحة 77'));
    await user.click(screen.getByRole('button', { name: 'تسطير الإنابة' }));
    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith(
        '/documents/10/delegations',
        expect.objectContaining({ isExternal: true, externalBranchId: 2 }),
      ),
    );
  });

  it('يرفض الإرسال دون اختيار الأموال موضوع الإنابة', async () => {
    const user = userEvent.setup();
    render(
      <DelegationFormModal
        documentId={10}
        assets={[vehicleAsset(1, '77')]}
        onClose={noop}
        onSaved={noop}
      />,
    );

    await user.type(screen.getByLabelText('الدائرة المنابة'), 'محكمة التنفيذ الأولى');
    await user.type(screen.getByLabelText('تاريخ الإنابة'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: 'تسطير الإنابة' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'يجب اختيار الأموال موضوع الإنابة',
    );
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يختار تلقائيًا الأموال المطابقة عند التعديل ويعرض غير المتاحة كتحذير', async () => {
    render(
      <DelegationFormModal
        documentId={10}
        assets={[vehicleAsset(1, '77')]}
        initial={pendingDelegation()}
        onClose={noop}
        onSaved={noop}
      />,
    );

    expect(screen.getByRole('dialog', { name: 'تعديل إنابة' })).toBeInTheDocument();
    expect(screen.getByLabelText('الدائرة المنابة')).toHaveValue('محكمة التنفيذ الأولى');
    expect(screen.getByLabelText('مركبة لوحة 77')).toBeChecked();
    expect(screen.getByText('متجر سجل رقم 3')).toBeInTheDocument();
    expect(
      screen.getByText(/أموال كانت محددة سابقًا ولم تعد متاحة/),
    ).toBeInTheDocument();
  });

  it('يحدّث إنابة معلّقة عبر PUT مع تواريخ نصية حرة', async () => {
    const user = userEvent.setup();
    render(
      <DelegationFormModal
        documentId={10}
        assets={[vehicleAsset(1, '77')]}
        initial={pendingDelegation()}
        onClose={noop}
        onSaved={noop}
      />,
    );

    await user.clear(screen.getByLabelText('الدائرة المنابة'));
    await user.type(screen.getByLabelText('الدائرة المنابة'), 'محكمة ثانية');
    await user.type(screen.getByLabelText('رقم كتاب إيداع رئيس القسم'), 'K-7');
    await user.type(screen.getByLabelText('تاريخ كتاب الإيداع'), '١/٨/٢٠٢٦');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));

    await waitFor(() =>
      expect(api.put).toHaveBeenCalledWith('/delegations/9', {
        delegatedCourt: 'محكمة ثانية',
        isExternal: false,
        externalBranchId: null,
        delegationDate: '2026-08-01',
        delegationText: 'نص سابق',
        depositBookNumber: 'K-7',
        depositBookDate: '1/8/2026',
        sendBookNumber: null,
        sendBookDate: null,
        assetIds: [1],
      }),
    );
  });
});
