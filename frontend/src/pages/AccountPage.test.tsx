import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
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

/** أرقام بطاقة «ملخص سريع» وحدها — لا يطابق الرقم عرضًا آخر في الصفحة (تاريخ/عدّاد). */
function summary() {
  return within(screen.getByRole('region', { name: 'ملخص إحصائي' }));
}

function mockApi() {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/stats/me') return Promise.resolve({ data: { totalFiles: 12 } });
    if (url === '/stats/manager') return Promise.resolve({ data: { totalFiles: 42 } });
    if (url === '/personal-reminders')
      return Promise.resolve({ data: [{ id: 1 }, { id: 2 }] });
    if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: 3 } });
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
    // رابط التنبيهات الإشرافي للمدير/المشرف وحده (§18:3044) — لا يظهر للمحامي.
    expect(screen.queryByRole('link', { name: 'عرض التنبيهات' })).not.toBeInTheDocument();
  });

  it('يعرض الملخص السريع بالأرقام الحقيقية', async () => {
    render(<AccountPage />);

    await waitFor(() => expect(summary().getByText('12')).toBeInTheDocument());
    expect(summary().getByText('2')).toBeInTheDocument();
    expect(summary().getByText('3')).toBeInTheDocument();
    // المحامي: نطاق الشارة `user` (صفوفه الشخصية) — مطابق لـ`Dashboard`.
    expect(api.get).toHaveBeenCalledWith(
      '/alerts/unread-count',
      expect.objectContaining({ params: { scope: 'user' } }),
    );
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

  it('يعرض لرئيس القسم ملخص الفرع (ملفات + غير المقروء + اقتراحاتي) بلا استعلامات المحامي', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 2, username: 'head1', fullName: 'رئيس القسم', role: 'head', branchName: 'دمشق', branchId: 1 },
      logout: vi.fn(),
    });
    render(<AccountPage />);

    expect(await screen.findByRole('heading', { name: 'الحساب الشخصي' })).toBeInTheDocument();
    expect(screen.getByText('رئيس قسم — دمشق')).toBeInTheDocument();
    expect(screen.queryByText('محامي — دمشق')).not.toBeInTheDocument();

    await waitFor(() => expect(summary().getByText('42')).toBeInTheDocument());
    expect(summary().getByText('ملفات النطاق هذه السنة')).toBeInTheDocument();
    // الشارة من `unread-count?scope=user` (النطاق الشخصي الموحّد — حُسم 2026-11-02)،
    // ووسمها صريح: «غير المقروء في تنبيهاتك» = غير المقروء لي.
    expect(summary().getByText('غير المقروء في تنبيهاتك')).toBeInTheDocument();
    expect(summary().getByText('3')).toBeInTheDocument();
    expect(screen.getAllByText('اقتراحاتي').length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByText('تذكيراتي النشطة')).not.toBeInTheDocument();
    expect(screen.queryByText('ملفاتي هذه السنة')).not.toBeInTheDocument();

    const urls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.map((c) => String(c[0]));
    expect(urls).toContain('/stats/manager');
    expect(urls).toContain('/app-suggestions');
    expect(urls).toContain('/alerts/unread-count');
    // الصفحة لا تجلب قائمة التنبيهات إطلاقًا (`headAlertsQuery` محذوف —
    // القائمة تعيش في `/alerts` وحدها، والملخص يعتمد العدّاد غير المرقّم).
    expect(urls).not.toContain('/alerts');
    expect(urls).not.toContain('/stats/me');
    expect(urls).not.toContain('/personal-reminders');
    expect(api.get).toHaveBeenCalledWith(
      '/alerts/unread-count',
      expect.objectContaining({ params: { scope: 'user' } }),
    );
  });

  it('رئيس بلا فرع: لا يطلب عدّاد `received` (مرفوض خلفيًا) ويعرض «—» بلا خطأ ولا طلب', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 2, username: 'head1', fullName: 'رئيس القسم', role: 'head', branchName: 'دمشق', branchId: null },
      logout: vi.fn(),
    });
    render(<AccountPage />);

    await waitFor(() => expect(summary().getByText('42')).toBeInTheDocument());
    expect(summary().getByText('غير المقروء في تنبيهاتك')).toBeInTheDocument();
    // «—» لا صفر كاذب: العدّاد غير متاح لرئيس بلا فرع (`D11`).
    expect(summary().getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('0')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith('/alerts/unread-count', expect.any(Object));
  });

  it('فشل العدّاد لا يُسقط الملخص: خطأ معلن و«—» في بطاقته وبقية الأرقام ظاهرة', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 2, username: 'head1', fullName: 'رئيس القسم', role: 'head', branchName: 'دمشق', branchId: 1 },
      logout: vi.fn(),
    });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/stats/manager') return Promise.resolve({ data: { totalFiles: 42 } });
      if (url === '/alerts/unread-count') return Promise.reject(new Error('تعذّر جلب العدّاد'));
      if (url === '/app-suggestions') return Promise.resolve({ data: [] });
      return Promise.resolve({ data: {} });
    });

    render(<AccountPage />);

    // الملفات معروفة فتُعرض رغم فشل العدّاد، والخطأ معلن مرة واحدة.
    await waitFor(() => expect(summary().getByText('42')).toBeInTheDocument());
    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر جلب العدّاد');
    expect(summary().getByText('غير المقروء في تنبيهاتك')).toBeInTheDocument();
    expect(summary().getByText('—')).toBeInTheDocument();
  });

  it('يعرض للمدير الملف والملخص بلا قسم اقتراحات (يستقبلها في صندوق المشرف)', async () => {
    useAuthMock.mockReturnValue({
      user: { id: 3, username: 'manager1', fullName: 'مدير النظام', role: 'manager', branchName: 'كل الفروع' },
      logout: vi.fn(),
    });
    render(<AccountPage />);

    expect(await screen.findByRole('heading', { name: 'الحساب الشخصي' })).toBeInTheDocument();
    expect(screen.getByText('مدير النظام')).toBeInTheDocument();
    expect(screen.getByText('مدير — كل الفروع')).toBeInTheDocument();

    await waitFor(() => expect(screen.getByText('ملفات هذه السنة')).toBeInTheDocument());
    // بلا زر إرسال ولا سجل اقتراحات — الإرسال محظور خلفيًا والمشرف يستقبل في `/suggestions`.
    expect(screen.queryByRole('button', { name: 'إرسال اقتراح لتطوير التطبيق' })).not.toBeInTheDocument();
    expect(screen.queryByText('اقتراحاتي')).not.toBeInTheDocument();
    expect(screen.queryByText('تذكيراتي النشطة')).not.toBeInTheDocument();
    expect(screen.queryByText('تنبيهات غير مقروءة')).not.toBeInTheDocument();

    const urls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.map((c) => String(c[0]));
    expect(urls).toContain('/stats/manager');
    expect(urls).not.toContain('/stats/me');
    expect(urls).not.toContain('/personal-reminders');
    expect(urls).not.toContain('/alerts/unread-count');
    expect(urls).not.toContain('/alerts');

    // لكن الوصول الإشرافي إلى القائمة متاح برابط (§18:3044) — بلا طلب بيانات.
    const alertsLink = screen.getByRole('link', { name: 'عرض التنبيهات' });
    expect(alertsLink).toHaveAttribute('href', '/alerts');
    expect(
      within(screen.getByRole('region', { name: 'التنبيهات (قراءة إشرافية)' })).getByText(
        /بلا تعليم مقروء ولا عدّاد/,
      ),
    ).toBeInTheDocument();
    expect(urls).not.toContain('/app-suggestions');
  });
});
