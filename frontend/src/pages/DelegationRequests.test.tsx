import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DelegationRequests from './DelegationRequests';
import type { DelegationDto } from '../types';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as { response?: { data?: { message?: string } } };
    return e?.response?.data?.message ?? 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../api/client';

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
    sendBookNumber: '',
    sendBookDate: '',
    assignedLawyerId: null,
    assignedLawyerName: null,
    returnDate: '',
    status: 'بانتظار رئيس القسم',
    createdAt: '2026-08-01',
    createdByName: 'سامر',
    createdById: 7,
    assets: [{ id: 100, assetKind: 'عقار', assetLabel: 'عقار — المزة 77' }],
    ...overrides,
  };
}

describe('DelegationRequests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
  });

  it('يعرض طلبات الإنابة المعلّقة مع زر الاعتماد', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [pendingDelegation(), pendingDelegation({ id: 10, sourceDocumentLabel: 'فاطمة علي' })],
    });
    render(<DelegationRequests />);

    expect(api.get).toHaveBeenCalledWith('/delegations/pending');
    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('فاطمة علي')).toBeInTheDocument();
    expect(screen.getByText('2 طلبات معلّقة')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'اعتماد واختيار محامٍ' })).toHaveLength(2);
  });

  it('يعرض حالة فارغة عند عدم وجود طلبات', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    render(<DelegationRequests />);

    expect(await screen.findByText('لا توجد طلبات إنابة معلّقة لفرعك')).toBeInTheDocument();
  });

  it('يعرض خطأ التحميل مع إعادة المحاولة', async () => {
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockRejectedValueOnce({ response: { data: { message: 'تعذر التحميل' } } });
    render(<DelegationRequests />);

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذر التحميل');
    getMock.mockResolvedValueOnce({ data: [pendingDelegation()] });
    await userEvent.setup().click(screen.getByRole('button', { name: 'إعادة المحاولة' }));
    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
  });

  it('يفتح نافذة الاعتماد ثم يحدّث القائمة ويُظهر رسالة النجاح بعد الاعتماد', async () => {
    const user = userEvent.setup();
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockResolvedValueOnce({ data: [pendingDelegation()] });
    getMock
      .mockResolvedValueOnce({
        data: [{ id: 8, username: 'lawyer2', fullName: 'المحامية سلمى', isActive: true, branchId: 1 }],
      })
      .mockResolvedValueOnce({ data: [] });
    render(<DelegationRequests />);

    await user.click(await screen.findByRole('button', { name: 'اعتماد واختيار محامٍ' }));
    expect(screen.getByRole('dialog', { name: 'اعتماد الإنابة' })).toBeInTheDocument();

    await screen.findByRole('option', { name: 'المحامية سلمى' });
    await user.selectOptions(screen.getByLabelText('المحامي المختص'), String(8));
    await user.click(screen.getByRole('button', { name: 'اعتماد وتكليف المحامي' }));

    expect(api.post).toHaveBeenCalledWith('/delegations/9/assign', { assignedLawyerId: 8 });
    expect(await screen.findByRole('status')).toHaveTextContent(
      'تم اعتماد الإنابة وتكليف المحامي المحامية سلمى',
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(await screen.findByText('لا توجد طلبات إنابة معلّقة لفرعك')).toBeInTheDocument();
  });
});