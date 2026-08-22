import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import BranchesManagement from './BranchesManagement';
import type { BranchDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
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
import { stubMobile } from '../test/stubMobile';

function branchItem(overrides: Partial<BranchDto> = {}): BranchDto {
  return {
    id: 1,
    name: 'الفرع الرئيسي - دمشق',
    code: 'DAM',
    address: 'دمشق',
    isActive: true,
    userCount: 2,
    documentCount: 5,
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  useAuthMock.mockReturnValue({ user: { id: 9, role: 'admin' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: [branchItem(), branchItem({ id: 2, name: 'فرع حلب', code: 'ALP' })],
  });
});

describe('BranchesManagement', () => {
  it('يعرض قائمة الفروع مع الكود والحالة', async () => {
    render(<BranchesManagement />);

    expect(await screen.findByText('الفرع الرئيسي - دمشق')).toBeInTheDocument();
    expect(screen.getByText('DAM')).toBeInTheDocument();
    expect(screen.getAllByText('مفعّل')).toHaveLength(2);
  });

  it('ينشئ فرعاً جديداً مع الحقول', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة فرع' }));
    await user.type(screen.getByLabelText('اسم الفرع'), 'فرع حمص');
    await user.type(screen.getByLabelText('كود الفرع'), 'HMS');
    await user.type(screen.getByLabelText('العنوان'), 'حمص');
    await user.type(screen.getByLabelText('الهاتف'), '031222333');
    await user.click(screen.getByRole('button', { name: 'إنشاء الفرع' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/branches', {
        name: 'فرع حمص',
        code: 'HMS',
        address: 'حمص',
        phone: '031222333',
      });
    });
  });

  it('يعرض رسالة خطأ عند إرسال فرع دون اسم', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة فرع' }));
    await user.type(screen.getByLabelText('كود الفرع'), 'HMS');
    await user.click(screen.getByRole('button', { name: 'إنشاء الفرع' }));

    expect(await screen.findByText(/اسم الفرع مطلوب/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعدّل فرعاً عبر النافذة المنبثقة', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click((await screen.findAllByRole('button', { name: 'تعديل' }))[0]);
    expect(screen.getByRole('dialog', { name: 'تعديل فرع' })).toBeInTheDocument();

    await user.clear(screen.getByLabelText('اسم الفرع'));
    await user.type(screen.getByLabelText('اسم الفرع'), 'الفرع الرئيسي - دمشق المعدل');
    await user.click(screen.getByRole('checkbox', { name: 'الفرع مفعّل' }));
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/branches/1', {
        name: 'الفرع الرئيسي - دمشق المعدل',
        code: 'DAM',
        address: 'دمشق',
        phone: null,
        isActive: false,
      });
    });
  });

  it('يحذف فرعاً بعد التأكيد', async () => {
    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click((await screen.findAllByRole('button', { name: 'تعديل' }))[0]);
    await user.click(screen.getByRole('button', { name: 'حذف الفرع' }));
    expect(screen.getByText(/هل أنت متأكد/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تأكيد الحذف' }));

    await waitFor(() => {
      expect(api.delete).toHaveBeenCalledWith('/branches/1');
    });
  });

  it('يعرض رسالة الخادم عند رفض حذف فرع مستخدم', async () => {
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { message: 'لا يمكن حذف فرع يحتوي على مستخدمين؛ عطّل الفرع بدلاً من ذلك' } },
    });

    const user = userEvent.setup();
    render(<BranchesManagement />);

    await user.click((await screen.findAllByRole('button', { name: 'تعديل' }))[0]);
    await user.click(screen.getByRole('button', { name: 'حذف الفرع' }));
    await user.click(screen.getByRole('button', { name: 'تأكيد الحذف' }));

    expect(await screen.findByText(/لا يمكن حذف فرع يحتوي على مستخدمين/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'تعديل فرع' })).toBeInTheDocument();
  });
});
