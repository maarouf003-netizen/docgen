import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import UsersManagement from './UsersManagement';
import type { UserListItem } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
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

function userItem(overrides: Partial<UserListItem> = {}): UserListItem {
  return {
    id: 1,
    username: 'lawyer1',
    fullName: 'محامي دمشق',
    role: 'lawyer',
    branchId: 1,
    branchName: 'دمشق',
    isActive: true,
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

const branches = [{ id: 1, name: 'دمشق', code: 'DAM' }];

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  useAuthMock.mockReturnValue({ user: { id: 9, role: 'admin' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/branches') return Promise.resolve({ data: branches });
    return Promise.resolve({ data: [userItem()] });
  });
});

describe('UsersManagement', () => {
  it('يعرض قائمة المستخدمين مع الأدوار', async () => {
    render(<UsersManagement />);

    expect(await screen.findByText('محامي دمشق')).toBeInTheDocument();
    expect(screen.getByText('محامي')).toBeInTheDocument();
  });

  it('ينشئ مستخدماً بدور رئيس قسم مع فرع', async () => {
    const user = userEvent.setup();
    render(<UsersManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة مستخدم' }));
    await user.type(screen.getByPlaceholderText('مثال: محمد أحمد علي'), 'رئيس جديد');
    await user.selectOptions(screen.getByLabelText('الدور'), 'head');
    await user.selectOptions(screen.getByLabelText('الفرع'), '1');
    await user.type(screen.getByLabelText(/كلمة المرور/), '123456');
    await user.click(screen.getByRole('button', { name: 'إنشاء المستخدم' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/users', {
        username: 'رئيس جديد',
        fullName: 'رئيس جديد',
        role: 'head',
        branchId: 1,
        password: '123456',
      });
    });
  });

  it('يعرض رسالة خطأ عند إنشاء مستخدم بدور فرع دون اختيار فرع', async () => {
    const user = userEvent.setup();
    render(<UsersManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة مستخدم' }));
    await user.type(screen.getByPlaceholderText('مثال: محمد أحمد علي'), 'رئيس جديد');
    await user.selectOptions(screen.getByLabelText('الدور'), 'head');
    await user.selectOptions(screen.getByLabelText('الفرع'), '');
    await user.type(screen.getByLabelText(/كلمة المرور/), '123456');
    await user.click(screen.getByRole('button', { name: 'إنشاء المستخدم' }));

    expect(await screen.findByText(/يجب تحديد الفرع/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعدّل مستخدماً عبر النافذة المنبثقة مع إعادة تعيين كلمة المرور', async () => {
    const user = userEvent.setup();
    render(<UsersManagement />);

    await user.click(await screen.findByRole('button', { name: 'تعديل' }));
    expect(screen.getByRole('dialog', { name: 'تعديل مستخدم' })).toBeInTheDocument();

    await user.clear(screen.getByLabelText(/الاسم الثلاثي/));
    await user.type(screen.getByLabelText(/الاسم الثلاثي/), 'محامي معدل');
    await user.type(screen.getByLabelText(/كلمة مرور جديدة/), '654321');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/users/1', {
        fullName: 'محامي معدل',
        role: 'lawyer',
        branchId: 1,
        isActive: true,
        password: '654321',
      });
    });
  });

  it('يظهر شارة الحالة الموقوف في القائمة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/branches') return Promise.resolve({ data: branches });
      return Promise.resolve({ data: [userItem({ isActive: false })] });
    });
    render(<UsersManagement />);

    expect(await screen.findByText('موقوف')).toBeInTheDocument();
  });
});
