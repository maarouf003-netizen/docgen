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
import { stubMobile } from '../test/stubMobile';

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

const branches = [{ id: 1, name: 'دمشق', code: 'DAM' }];

const sections = [{ id: 3, branchId: 1, name: 'مصياف', isActive: true, circuitCount: 1, headName: null }];

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  useAuthMock.mockReturnValue({ user: { id: 9, role: 'admin' } });
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/branches') return Promise.resolve({ data: branches });
    if (url.startsWith('/sections')) return Promise.resolve({ data: sections });
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
        sectionId: null,
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
        sectionId: null,
        isActive: true,
        successorId: null,
        password: '654321',
      });
    });
  });

  it('المدير لا يرى خيار دور المشرف ولا زر تعديل على صفوف المشرفين (`BQ-001د`)', async () => {
    useAuthMock.mockReturnValue({ user: { id: 8, role: 'manager' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/branches') return Promise.resolve({ data: branches });
      return Promise.resolve({ data: [userItem({ id: 7, role: 'admin', fullName: 'مشرف النظام' }), userItem()] });
    });
    const user = userEvent.setup();
    render(<UsersManagement />);

    expect(await screen.findByText('مشرف النظام')).toBeInTheDocument();
    // لا زر تعديل على صف المشرف — زر وحيد لصف المحامي.
    expect(screen.getAllByRole('button', { name: 'تعديل' })).toHaveLength(1);

    await user.click(screen.getByRole('button', { name: '+ إضافة مستخدم' }));
    const roleSelect = screen.getByLabelText('الدور') as HTMLSelectElement;
    const options = Array.from(roleSelect.options).map((o) => o.value);
    expect(options).not.toContain('admin');
    expect(options).toContain('lawyer');
  });

  it('المشرف يرى خيار دور المشرف وزر تعديل على كل الصفوف', async () => {
    render(<UsersManagement />);

    expect(await screen.findByText('محامي دمشق')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تعديل' })).toBeInTheDocument();

    await userEvent.setup().click(screen.getByRole('button', { name: '+ إضافة مستخدم' }));
    const roleSelect = screen.getByLabelText('الدور') as HTMLSelectElement;
    expect(Array.from(roleSelect.options).map((o) => o.value)).toContain('admin');
  });

  it('يظهر شارة الحالة الموقوف في القائمة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/branches') return Promise.resolve({ data: branches });
      if (url.startsWith('/sections')) return Promise.resolve({ data: sections });
      return Promise.resolve({ data: [userItem({ isActive: false })] });
    });
    render(<UsersManagement />);

    expect(await screen.findByText('موقوف')).toBeInTheDocument();
  });

  it('يعرض خيار رئيس الشعبة وحقل الشعبة الإلزامي عند اختياره', async () => {
    const user = userEvent.setup();
    render(<UsersManagement />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة مستخدم' }));
    const roleSelect = screen.getByLabelText('الدور') as HTMLSelectElement;
    expect(Array.from(roleSelect.options).map((o) => o.value)).toContain('subhead');

    await user.selectOptions(screen.getByLabelText('الدور'), 'subhead');
    await user.selectOptions(screen.getByLabelText('الفرع'), '1');
    expect(await screen.findByLabelText(/الشعبة/)).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText('مثال: محمد أحمد علي'), 'رئيس شعبة');
    await user.type(screen.getByLabelText(/كلمة المرور/), '123456');
    await user.click(screen.getByRole('button', { name: 'إنشاء المستخدم' }));
    expect(await screen.findByText(/الشعبة إلزامية/)).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();

    await user.selectOptions(screen.getByLabelText(/الشعبة/), '3');
    await user.click(screen.getByRole('button', { name: 'إنشاء المستخدم' }));
    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/users', {
        username: 'رئيس شعبة',
        fullName: 'رئيس شعبة',
        role: 'subhead',
        branchId: 1,
        sectionId: 3,
        password: '123456',
      });
    });
  });

  it('يعرض اسم الشعبة في القائمة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/branches') return Promise.resolve({ data: branches });
      if (url.startsWith('/sections')) return Promise.resolve({ data: sections });
      return Promise.resolve({ data: [userItem({ role: 'subhead', fullName: 'رئيس مصياف', sectionId: 3, sectionName: 'مصياف' })] });
    });
    render(<UsersManagement />);

    expect(await screen.findByText('مصياف')).toBeInTheDocument();
  });

  it('تعطيل رئيس قسم يتطلب خلفًا إجباريًا', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/branches') return Promise.resolve({ data: branches });
      if (url.startsWith('/sections')) return Promise.resolve({ data: sections });
      return Promise.resolve({
        data: [
          userItem({ id: 5, role: 'head', fullName: 'رئيس القسم' }),
          userItem({ id: 6, role: 'lawyer', fullName: 'محامٍ خليفة' }),
        ],
      });
    });
    const user = userEvent.setup();
    render(<UsersManagement />);

    await user.click((await screen.findAllByRole('button', { name: 'تعديل' }))[0]);
    expect(screen.getByRole('dialog', { name: 'تعديل مستخدم' })).toBeInTheDocument();

    await user.click(screen.getByLabelText(/الحساب مفعّل/));
    expect(await screen.findByLabelText(/الخلف الإجباري/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));
    expect(await screen.findByText(/خلفًا إجباريًا/)).toBeInTheDocument();
    expect(api.put).not.toHaveBeenCalled();

    await user.selectOptions(screen.getByLabelText(/الخلف الإجباري/), '6');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));
    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/users/5', expect.objectContaining({
        isActive: false,
        successorId: 6,
      }));
    });
  });
});
