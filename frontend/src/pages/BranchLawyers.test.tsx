import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BranchLawyers from './BranchLawyers';
import type { LawyerListItem } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), patch: vi.fn() },
  getApiErrorMessage: (error: unknown) => {
    const e = error as {
      isAxiosError?: boolean;
      response?: { status?: number; data?: { message?: string } };
    };
    if (e?.isAxiosError) {
      if (e.response?.data?.message) return e.response.data.message;
      if (e.response?.status === 403) return 'لا تملك صلاحية تنفيذ هذا الإجراء';
      return 'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة';
    }
    return 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../api/client';

function lawyer(overrides: Partial<LawyerListItem> = {}): LawyerListItem {
  return {
    id: 1,
    username: 'lawyer_x',
    fullName: 'محامي دمشق',
    isActive: true,
    branchId: 1,
    branchName: 'دمشق',
    ...overrides,
  };
}

function stubMobile(matches: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

function mockGetWithCount(count: number) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url.startsWith('/documents/owner/')) return Promise.resolve({ data: { count } });
    return Promise.resolve({ data: [lawyer(), lawyer({ id: 2, fullName: 'محامي ثانٍ' })] });
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  useAuthMock.mockReturnValue({ user: { role: 'head' } });
  Object.defineProperty(window, 'confirm', { writable: true, value: vi.fn(() => true) });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [lawyer()] });
});

describe('BranchLawyers', () => {
  it('يعرض قائمة محامي الفرع لرئيس القسم دون منتقي الفرع', async () => {
    render(<BranchLawyers />);

    expect(await screen.findByText('محامي دمشق')).toBeInTheDocument();
    expect(screen.queryByLabelText('الفرع')).not.toBeInTheDocument();
  });

  it('يعرض منتقي الفرع للمشرف ويطلب الفرع عند الإضافة', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'admin' } });
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ data: [{ id: 1, name: 'دمشق', code: 'DAM' }] })
      .mockResolvedValueOnce({ data: [lawyer()] });
    const user = userEvent.setup();
    render(<BranchLawyers />);

    expect(await screen.findByLabelText('الفرع')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '+ إضافة محامٍ' }));
    await user.type(screen.getByPlaceholderText('مثال: محمد أحمد علي'), 'محامي جديد');
    await user.type(screen.getByLabelText(/كلمة المرور/), '123456');
    await user.click(screen.getByRole('button', { name: 'حفظ المحامي' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/users/lawyers', {
        username: 'محامي جديد',
        fullName: 'محامي جديد',
        password: '123456',
        branchId: 1,
      });
    });
  });

  it('يرسل بدون فرع عند إضافة محامٍ من قبل رئيس القسم', async () => {
    const user = userEvent.setup();
    render(<BranchLawyers />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة محامٍ' }));
    await user.type(screen.getByPlaceholderText('مثال: محمد أحمد علي'), 'محامي جديد');
    await user.type(screen.getByLabelText(/كلمة المرور/), '123456');
    await user.click(screen.getByRole('button', { name: 'حفظ المحامي' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/users/lawyers', {
        username: 'محامي جديد',
        fullName: 'محامي جديد',
        password: '123456',
        branchId: null,
      });
    });
  });

  it('يوقف محامياً عبر PATCH بعد التأكيد', async () => {
    const user = userEvent.setup();
    render(<BranchLawyers />);

    await user.click(await screen.findByRole('button', { name: 'إيقاف' }));

    await waitFor(() => {
      expect(api.patch).toHaveBeenCalledWith('/users/1/active', { isActive: false });
    });
  });

  it('يمنع الإضافة عند كلمة مرور قصيرة ويعرض رسالة خطأ', async () => {
    const user = userEvent.setup();
    render(<BranchLawyers />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة محامٍ' }));
    await user.type(screen.getByPlaceholderText('مثال: محمد أحمد علي'), 'محامي جديد');
    await user.type(screen.getByLabelText(/كلمة المرور/), '123');
    await user.click(screen.getByRole('button', { name: 'حفظ المحامي' }));

    expect(await screen.findByText(/يجب أن تكون 6 أحرف/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعرض زر النقل الجماعي لرئيس القسم فقط', async () => {
    render(<BranchLawyers />);

    expect(await screen.findByRole('button', { name: 'نقل كامل ملفاته' })).toBeInTheDocument();
  });

  it('لا يعرض زر النقل الجماعي للمشرف', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'admin' } });
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ data: [{ id: 1, name: 'دمشق', code: 'DAM' }] })
      .mockResolvedValueOnce({ data: [lawyer()] });
    render(<BranchLawyers />);

    expect(await screen.findByText('محامي دمشق')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'نقل كامل ملفاته' })).not.toBeInTheDocument();
  });

  it('يمنع متابعة النقل الجماعي دون اختيار الهدف', async () => {
    mockGetWithCount(2);
    const user = userEvent.setup();
    render(<BranchLawyers />);

    const buttons = await screen.findAllByRole('button', { name: 'نقل كامل ملفاته' });
    await user.click(buttons[0]);
    await user.click(await screen.findByRole('button', { name: 'متابعة' }));

    expect(screen.getByText('اختر المحامي المستهدف')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('ينفذ النقل الجماعي بخطوتين ويعرض النتيجة', async () => {
    mockGetWithCount(3);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { transferredCount: 3 } });
    const user = userEvent.setup();
    render(<BranchLawyers />);

    const buttons = await screen.findAllByRole('button', { name: 'نقل كامل ملفاته' });
    await user.click(buttons[0]);

    // الخطوة الأولى: معاينة العدد واختيار المحامي المستهدف.
    expect(await screen.findByText(/سيتم نقل 3 ملفًا من محامي دمشق/)).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('المحامي المستهدف'), '2');
    await user.click(screen.getByRole('button', { name: 'متابعة' }));

    // الخطوة الثانية: التأكيد النهائي ثم التنفيذ.
    expect(screen.getByText(/تأكيد نهائي/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تأكيد النقل النهائي' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/documents/transfer-all', {
        sourceLawyerId: 1,
        targetLawyerId: 2,
      });
    });
    expect(await screen.findByText(/تم نقل 3 ملفًا إلى محامي ثانٍ/)).toBeInTheDocument();
  });
});
