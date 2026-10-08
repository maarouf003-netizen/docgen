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

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

import { api } from '../api/client';

function authHead() {
  useAuthMock.mockReturnValue({ user: { id: 5, role: 'head', branchId: 1 } });
}

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

describe('DelegationRequests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authHead();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/sections') return Promise.resolve({ data: [] });
      return Promise.resolve({ data: [] });
    });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
  });

  it('يعرض طلبات الإنابة المعلّقة مع زر الاعتماد', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [pendingDelegation(), pendingDelegation({ id: 10, sourceDocumentLabel: 'فاطمة علي' })],
    });
    render(<DelegationRequests />);

    expect(api.get).toHaveBeenCalledWith('/delegations/pending', expect.any(Object));
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
    getMock
      .mockResolvedValueOnce({ data: [pendingDelegation()] })
      .mockResolvedValueOnce({ data: [] })
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

  it('رئيس القسم يوجّه الخارجية لشعبة ثم تُحدَّث القائمة', async () => {
    const user = userEvent.setup();
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockImplementation((url: string) => {
      if (url === '/sections')
        return Promise.resolve({ data: [{ id: 3, branchId: 1, name: 'مصياف', isActive: true, circuitCount: 1, headName: 'رئيس الشعبة' }] });
      return Promise.resolve({
        data: [pendingDelegation({ isExternal: true, externalBranchName: 'اللاذقية' })],
      });
    });
    render(<DelegationRequests />);

    await user.click(await screen.findByRole('button', { name: 'توجيه للشعبة' }));
    expect(screen.getByRole('dialog', { name: 'توجيه الإنابة لشعبة' })).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('الشعبة'), '3');
    await user.click(screen.getByRole('button', { name: 'توجيه' }));

    expect(api.post).toHaveBeenCalledWith('/delegations/9/redirect', { sectionId: 3 });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('رئيس القسم يتراجع عن التوجيه قبل الإسناد', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [pendingDelegation({ isExternal: true, redirectedToSectionId: 3, redirectedToSectionName: 'مصياف' })],
    });
    render(<DelegationRequests />);

    expect(await screen.findByText('موجَّه لشعبة مصياف')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تراجع عن التوجيه' }));
    expect(api.post).toHaveBeenCalledWith('/delegations/9/recall-redirect', {});
  });

  it('الرفض يتطلب سببًا ويُعيد المحامي للتصحيح', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [pendingDelegation()] });
    render(<DelegationRequests />);

    await user.click(await screen.findByRole('button', { name: 'رفض' }));
    expect(screen.getByRole('dialog', { name: 'رفض الإنابة' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'رفض وإعادة للمحامي' })).toBeDisabled();
    await user.type(screen.getByLabelText(/سبب الرفض/), 'الدائرة ليست ضمن نطاقك');
    await user.click(screen.getByRole('button', { name: 'رفض وإعادة للمحامي' }));

    expect(api.post).toHaveBeenCalledWith('/delegations/9/reject', { reason: 'الدائرة ليست ضمن نطاقك' });
  });

  it('فلتر مرفوض بانتظار التصحيح يمرر rejectedOnly', async () => {
    const user = userEvent.setup();
    const getMock = api.get as unknown as ReturnType<typeof vi.fn>;
    getMock.mockResolvedValue({ data: [] });
    render(<DelegationRequests />);

    await user.click(await screen.findByRole('button', { name: 'مرفوض بانتظار التصحيح' }));
    expect(getMock).toHaveBeenCalledWith('/delegations/pending', expect.objectContaining({
      params: { rejectedOnly: true },
    }));
  });

  it('رئيس الشعبة لا يرى زري التوجيه والتراجع (توجيه الخارجية للقسم فقط)', async () => {
    useAuthMock.mockReturnValue({ user: { id: 6, role: 'subhead', branchId: 1, sectionId: 3 } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [pendingDelegation({ isExternal: true, externalBranchName: 'اللاذقية' })],
    });
    render(<DelegationRequests />);

    await screen.findByRole('button', { name: 'اعتماد واختيار محامٍ' });
    expect(screen.queryByRole('button', { name: 'توجيه للشعبة' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تراجع عن التوجيه' })).not.toBeInTheDocument();
  });
});