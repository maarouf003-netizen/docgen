import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import AlertsPage from './AlertsPage';
import { makeHeadAlertSummary } from '../test/factories';

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
  api: { get: vi.fn(), post: vi.fn(), patch: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

const get = api.get as unknown as ReturnType<typeof vi.fn>;
const patch = api.patch as unknown as ReturnType<typeof vi.fn>;

function pageBody(items: ReturnType<typeof makeHeadAlertSummary>[], totalCount = items.length) {
  return {
    data: {
      items,
      page: 1,
      perPage: 100,
      totalCount,
      totalPages: totalCount === 0 ? 0 : 1,
    },
  };
}

function receivedAlert() {
  return makeHeadAlertSummary({
    id: 1,
    kind: 'manual_to_branch',
    messagePreview: 'تعميم للفرع بخصوص الدوام',
    isRead: false,
    previewTruncated: true,
  });
}

function sentAlert() {
  return makeHeadAlertSummary({
    id: 5,
    kind: 'manual_to_branch',
    messagePreview: 'تنبيه صادر للفرع',
    isRead: null,
    recipientCount: 2,
    unreadCount: 1,
  });
}

/** يوجّه كل مسار إلى استجابة واقعية؛ أي مسار غير متوقع يعيد صفحة فارغة. */
function mockApi({ received = [receivedAlert()], sent = [sentAlert()] as ReturnType<typeof sentAlert>[] } = {}) {
  get.mockImplementation((url: string) => {
    if (url === '/alerts?archived=active') return Promise.resolve(pageBody(received));
    if (url === '/alerts/sent?archived=active') return Promise.resolve(pageBody(sent));
    if (url.startsWith('/alerts?')) return Promise.resolve(pageBody([]));
    if (url.startsWith('/alerts/sent?')) return Promise.resolve(pageBody([]));
    if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: 3 } });
    if (url === '/alerts/1')
      return Promise.resolve({
        data: {
          ...receivedAlert(),
          message: 'النص الكامل للتعميم الذي لا تحمله القائمة',
        },
      });
    if (url === '/alerts/5/recipients')
      return Promise.resolve({
        data: [
          { id: 11, userId: 2, fullName: 'سامر الأحمد', role: 'lawyer', isRead: false, readAt: null },
        ],
      });
    if (url === '/users/lawyers') return Promise.resolve({ data: [] });
    return Promise.resolve({ data: {} });
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue({
    user: { id: 1, username: 'lawyer1', fullName: 'أحمد الخطيب', role: 'lawyer', branchId: 3 },
  });
});

describe('AlertsPage — المحامي', () => {
  it('يعرض تنبيهاته المستلمة بطلب النطاق النشط دون تبويب «مرسلة»', async () => {
    mockApi();
    render(<AlertsPage />);

    expect(await screen.findByText('تعميم للفرع بخصوص الدوام')).toBeInTheDocument();
    expect(get).toHaveBeenCalledWith(
      '/alerts?archived=active',
      expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
    );
    expect(screen.queryByRole('tab', { name: /مرسلة/ })).not.toBeInTheDocument();
  });

  it('لا يجلب النص الكامل قبل فتح التنبيه، ثم يجلبه من `GET /alerts/{id}`', async () => {
    mockApi();
    const user = userEvent.setup();
    render(<AlertsPage />);

    await screen.findByText('تعميم للفرع بخصوص الدوام');
    // القائمة لا تحمل النص الكامل أصلًا، ولا طلب تفاصيل قبل الفتح.
    expect(get).not.toHaveBeenCalledWith('/alerts/1', expect.anything());

    await user.click(screen.getByRole('button', { name: 'عرض التنبيه' }));

    expect(
      await screen.findByText('النص الكامل للتعميم الذي لا تحمله القائمة'),
    ).toBeInTheDocument();
    expect(get).toHaveBeenCalledWith('/alerts/1', expect.anything());
  });

  it('تعليم المقروء يرسل `PATCH` ويُعلن الخطأ بدل الابتلاع عند الفشل', async () => {
    mockApi();
    patch.mockRejectedValueOnce(new Error('تعذّر تعليم التنبيه مقروءًا'));
    const user = userEvent.setup();
    render(<AlertsPage />);

    await screen.findByText('تعميم للفرع بخصوص الدوام');
    await user.click(screen.getByRole('button', { name: 'تمت القراءة' }));

    expect(patch).toHaveBeenCalledWith('/alerts/1/read');
    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر تعليم التنبيه مقروءًا');
  });

  it('تصفية الأرشيف تغيّر النطاق المطلوب (لا تُبقي القائمة النشطة)', async () => {
    mockApi();
    const user = userEvent.setup();
    render(<AlertsPage />);

    await screen.findByText('تعميم للفرع بخصوص الدوام');
    await user.click(screen.getByRole('button', { name: 'المؤرشف' }));

    await waitFor(() =>
      expect(get).toHaveBeenCalledWith(
        '/alerts?archived=only',
        expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
      ),
    );
  });
});

describe('AlertsPage — رئيس القسم/الشعبة', () => {
  beforeEach(() => {
    useAuthMock.mockReturnValue({
      user: { id: 2, username: 'head1', fullName: 'رئيس القسم', role: 'head', branchId: 3 },
    });
  });

  it('يعرض التبويبين ويجلب «مرسلة» بنطاقه المستقل', async () => {
    mockApi();
    const user = userEvent.setup();
    render(<AlertsPage />);

    await screen.findByText('تعميم للفرع بخصوص الدوام');
    await user.click(screen.getByRole('tab', { name: /مرسلة/ }));

    expect(await screen.findByText('تنبيه صادر للفرع')).toBeInTheDocument();
    expect(get).toHaveBeenCalledWith(
      '/alerts/sent?archived=active',
      expect.objectContaining({ params: { page: 1, pageSize: 100 } }),
    );
  });

  it('المستلمون متاحون في «مرسلة» فقط وبالعقد القائم', async () => {
    mockApi();
    const user = userEvent.setup();
    render(<AlertsPage />);

    await screen.findByText('تعميم للفرع بخصوص الدوام');
    // في «مستلمة» لا زر مستلمين (الحالة حالة المستخدم نفسه، لا قائمة مُرسلة).
    expect(screen.queryByRole('button', { name: 'المستلمون' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: /مرسلة/ }));
    await screen.findByText('تنبيه صادر للفرع');
    await user.click(screen.getByRole('button', { name: 'المستلمون' }));

    const dialog = await screen.findByRole('dialog', { name: 'مستلمو التنبيه' });
    expect(within(dialog).getByText('سامر الأحمد')).toBeInTheDocument();
    expect(within(dialog).getByText('غير مقروء')).toBeInTheDocument();
    expect(get).toHaveBeenCalledWith('/alerts/5/recipients', expect.anything());
  });

  it('رئيس بلا فرع: حالة صريحة بلا أي طلب تنبيهات', () => {
    useAuthMock.mockReturnValue({
      user: { id: 3, username: 'head2', fullName: 'رئيس بلا فرع', role: 'head', branchId: null },
    });
    render(<AlertsPage />);

    expect(screen.getByText(/حسابك غير مرتبط بفرع/)).toBeInTheDocument();
    const alertCalls = get.mock.calls.filter(([url]) => String(url).startsWith('/alerts'));
    expect(alertCalls).toHaveLength(0);
  });

  it('تعليم صف الرئيس/الشعبة مقروءًا يُنقص شارة التبويب وعدّاد الصف معًا', async () => {
    get.mockImplementation((url: string) => {
      if (url === '/alerts?archived=active') {
        return Promise.resolve(
          pageBody([
            makeHeadAlertSummary({
              id: 4,
              kind: 'manual_to_branch',
              messagePreview: 'تعميم إلى رؤساء الفرع',
              targetType: 'head',
              recipientCount: 2,
              unreadCount: 2,
              isRead: false,
            }),
          ]),
        );
      }
      if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: 2 } });
      if (url === '/users/lawyers') return Promise.resolve({ data: [] });
      return Promise.resolve(pageBody([]));
    });
    patch.mockResolvedValue({});
    const user = userEvent.setup();
    render(<AlertsPage />);

    const tab = await screen.findByRole('tab', { name: /مستلمة/ });
    await within(tab).findByText('2', { exact: false });
    expect(await screen.findByText('غير مقروء: 2 / 2')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'تمت القراءة' }));

    expect(patch).toHaveBeenCalledWith('/alerts/4/read');
    // عدّاد الصف ينقص واحدًا وشارة `received` تنقص بالمقدار نفسه.
    expect(await screen.findByText('غير مقروء: 1 / 2')).toBeInTheDocument();
    expect(within(screen.getByRole('tab', { name: /مستلمة/ })).getByText('1')).toBeInTheDocument();
  });

  it('يصدر تعميماً للفرع: `POST /alerts` ثم رسالة نجاح وإعادة تحميل الصادر', async () => {
    get.mockImplementation((url: string) => {
      if (url === '/alerts?archived=active') return Promise.resolve(pageBody([receivedAlert()]));
      if (url === '/alerts/sent?archived=active') return Promise.resolve(pageBody([]));
      if (url === '/alerts/unread-count') return Promise.resolve({ data: { count: 0 } });
      if (url === '/users/lawyers') return Promise.resolve({ data: [] });
      return Promise.resolve(pageBody([]));
    });
    const post = api.post as unknown as ReturnType<typeof vi.fn>;
    post.mockResolvedValue({ data: { id: 9 } });
    const user = userEvent.setup();
    render(<AlertsPage />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));
    await user.type(await screen.findByLabelText('نص التنبيه'), 'اجتماع الفرع يوم الأحد');
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    expect(post).toHaveBeenCalledWith('/alerts', {
      targetType: 'branch',
      documentId: null,
      targetLawyerId: null,
      message: 'اجتماع الفرع يوم الأحد',
    });
    expect(await screen.findByRole('status')).toHaveTextContent(/تم إصدار التنبيه/);
    expect(screen.queryByLabelText('نص التنبيه')).not.toBeInTheDocument();
  });

  it('إصدار بلا نص: خطأ منسوب للحقل وتركيز عليه بلا إرسال', async () => {
    mockApi();
    const user = userEvent.setup();
    render(<AlertsPage />);

    await user.click(await screen.findByRole('button', { name: '+ إصدار تنبيه' }));
    await user.click(screen.getByRole('button', { name: 'إرسال التنبيه' }));

    const error = await screen.findByRole('alert');
    expect(error).toHaveTextContent('نص التنبيه مطلوب');
    const textarea = screen.getByLabelText('نص التنبيه');
    expect(textarea).toHaveAttribute('aria-invalid', 'true');
    expect(document.activeElement).toBe(textarea);
    expect(api.post).not.toHaveBeenCalled();
  });

  it('ربط التبويب بلوحته: `aria-controls` يشير إلى لوحة موجودة باسمها', async () => {
    mockApi();
    render(<AlertsPage />);

    const receivedTab = await screen.findByRole('tab', { name: /مستلمة/ });
    expect(receivedTab).toHaveAttribute('id', 'alerts-tab-received');
    expect(receivedTab).toHaveAttribute('aria-controls', 'alerts-panel-received');
    const panel = document.getElementById('alerts-panel-received');
    expect(panel).toHaveAttribute('role', 'tabpanel');
    expect(panel).toHaveAttribute('aria-labelledby', 'alerts-tab-received');
  });
});
