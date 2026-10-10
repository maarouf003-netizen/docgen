import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import AccountPage from './AccountPage';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function mockApi() {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/stats/me') return Promise.resolve({ data: { totalFiles: 12 } });
    if (url === '/stats/manager') return Promise.resolve({ data: { totalFiles: 42 } });
    if (url === '/personal-reminders')
      return Promise.resolve({ data: [{ id: 1 }, { id: 2 }] });
    if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: 3 } });
    if (url === '/alerts')
      return Promise.resolve({
        data: [
          { id: 1, recipientCount: 2, unreadCount: 2 },
          { id: 2, recipientCount: 3, unreadCount: 1 },
        ],
      });
    if (url === '/app-suggestions')
      return Promise.resolve({
        data: [{ id: 9, message: 'اقتراحي', createdAt: '2026-08-08T10:00:00', isRead: false }],
      });
    return Promise.resolve({ data: {} });
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  useAuthMock.mockReturnValue({
    user: { id: 1, username: 'lawyer1', fullName: 'أحمد الخطيب', role: 'lawyer', branchName: 'دمشق' },
    logout: vi.fn(),
  });
  mockApi();
});

describe('AccountPage', () => {
  it('يعرض الملف الشخصي مع زر تسجيل الخروج ونموذج كلمة المرور', async () => {
    render(<AccountPage />);

    expect(await screen.findByRole('heading', { name: 'الحساب الشخصي' })).toBeInTheDocument();
    expect(screen.getByText('أحمد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('محامي — دمشق')).toBeInTheDocument();
    expect(screen.getByLabelText('كلمة المرور الحالية')).toBeInTheDocument();

    await userEvent.setup().click(screen.getByRole('button', { name: 'تسجيل الخروج' }));
    expect(useAuthMock().logout).toHaveBeenCalledTimes(1);
  });

  it('يعرض الملخص السريع بالأرقام الحقيقية', async () => {
    render(<AccountPage />);

    await waitFor(() => expect(screen.getByText('12')).toBeInTheDocument());
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('يرسل الاقتراح من الحوار ويعرض سجل القراءة', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(<AccountPage />);

    await user.click(screen.getByRole('button', { name: 'إرسال اقتراح لتطوير التطبيق' }));
    expect(screen.getByRole('dialog', { name: 'إرسال اقتراح لتطوير التطبيق' })).toBeInTheDocument();

    await user.type(screen.getByLabelText('نص الاقتراح *'), 'اقتراح جديد');
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    expect(api.post).toHaveBeenCalledWith('/app-suggestions', { message: 'اقتراح جديد' });
    expect(await screen.findByRole('status')).toHaveTextContent('تم إرسال اقتراحك');
    expect(screen.getByText('اقتراحي')).toBeInTheDocument();
    expect(screen.getByText('بانتظار القراءة')).toBeInTheDocument();
  });

  it('يرفض الاقتراح الفارغ ويركّز الحقل', async () => {
    const user = userEvent.setup();
    render(<AccountPage />);

    await user.click(screen.getByRole('button', { name: 'إرسال اقتراح لتطوير التطبيق' }));
    await user.click(screen.getByRole('button', { name: 'إرسال الاقتراح' }));

    expect(screen.getByRole('alert')).toHaveTextContent('نص الاقتراح مطلوب');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعرض لرئيس القسم ملخص الفرع (ملفات + غير مقروءة + اقتراحاتي) بلا استعلامات المحامي', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 2, username: 'head1', fullName: 'رئيس القسم', role: 'head', branchName: 'دمشق' },
      logout: vi.fn(),
    });
    render(<AccountPage />);

    expect(await screen.findByRole('heading', { name: 'الحساب الشخصي' })).toBeInTheDocument();
    expect(screen.getByText('رئيس قسم — دمشق')).toBeInTheDocument();
    expect(screen.queryByText('محامي — دمشق')).not.toBeInTheDocument();

    await waitFor(() => expect(screen.getByText('42')).toBeInTheDocument());
    expect(screen.getByText('ملفات النطاق هذه السنة')).toBeInTheDocument();
    // الدلالة مجموع المستلمين غير القارئين عبر التنبيهات (2+1=3) لا عدد التنبيهات (2)
    // ولا عدد الأشخاص المميزين — الوسم يصرّح بالمجموع عمدًا.
    expect(screen.getByText('مجموع مستلمي تنبيهات النطاق غير القارئين')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getAllByText('اقتراحاتي').length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByText('تذكيراتي النشطة')).not.toBeInTheDocument();
    expect(screen.queryByText('ملفاتي هذه السنة')).not.toBeInTheDocument();

    const urls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.map((c) => String(c[0]));
    expect(urls).toContain('/stats/manager');
    expect(urls).toContain('/alerts');
    expect(urls).toContain('/app-suggestions');
    expect(urls).not.toContain('/stats/me');
    expect(urls).not.toContain('/personal-reminders');
    expect(urls).not.toContain('/alerts/unread-count');
  });
});
