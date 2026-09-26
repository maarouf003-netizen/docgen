import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import App from './App';
import { stubMobile } from './test/stubMobile';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('./auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('./api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api/client')>();
  return {
    ...original,
    api: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      patch: vi.fn(),
    },
  };
});

import { api } from './api/client';

type ApiMock = ReturnType<typeof vi.fn>;

function rejectAllApi() {
  for (const method of [api.get, api.post, api.put, api.delete, api.patch]) {
    (method as unknown as ApiMock).mockRejectedValue(new Error('مرفوض في الاختبار'));
  }
}

function authState(role: string) {
  return {
    user: { id: 7, username: 'u7', fullName: 'مستخدم اختبار', role, branchId: 1, branchName: 'دمشق' },
    loading: false,
    login: vi.fn(),
    logout: vi.fn(),
    hasFullAccess: role === 'manager' || role === 'admin',
    isHead: role === 'head',
  };
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  stubMobile(false);
  rejectAllApi();
});

describe('توجيه الجذر حسب الدور (انحدار: المندوب بلا لوحة تحكم)', () => {
  it('المندوب على / يُحوَّل إلى بوابته ولا يرى «لوحة التحكم» إطلاقًا', async () => {
    useAuthMock.mockReturnValue(authState('entitymanager'));
    renderAt('/');

    // عنوان صفحة البوابة ظهر…
    expect(await screen.findByRole('heading', { name: 'الإحصائيات' })).toBeInTheDocument();
    // …ولوحة التحكم غائبة تمامًا (لا عنوان ولا محتوى).
    expect(screen.queryByRole('heading', { name: 'لوحة التحكم' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /إصدار تنبيه/ })).not.toBeInTheDocument();
    // ولم يُطلب أي مسار داخلي محظور على المندوب — اللوحة لم تُحمَّل أصلًا.
    const urls = (api.get as unknown as ApiMock).mock.calls.map((c) => String(c[0]));
    expect(urls).not.toContain('/stats/manager');
    expect(urls).not.toContain('/stats/periods');
    expect(urls).not.toContain('/alerts');
  });

  it('المحامي على / يرى لوحة التحكم كالمعتاد', async () => {
    useAuthMock.mockReturnValue(authState('lawyer'));
    renderAt('/');

    expect(await screen.findByRole('heading', { name: 'لوحة التحكم' })).toBeInTheDocument();
  });

  it('رفض صلاحية المندوب على مسار داخلي محروس يرتد به إلى بوابته لا إلى اللوحة', async () => {
    useAuthMock.mockReturnValue(authState('entitymanager'));
    renderAt('/correspondence');

    expect(await screen.findByRole('heading', { name: 'الإحصائيات' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'لوحة التحكم' })).not.toBeInTheDocument();
  });

  // كل مسار تشغيلي داخلي محروس بـ allowInternal: المندوب يُرتد إلى بوابته دائمًا.
  it.each([
    '/documents',
    '/reviews',
    '/reviews/5',
    '/appeals',
    '/appeals/5',
    '/documents/deleted',
    '/documents/struck-off',
    '/documents/executed',
    '/documents/referred-to-start',
    '/documents/new',
    '/documents/42',
    '/documents/42/edit',
  ])('المندوب على %s يُرتد إلى بوابته ولا يرى المحتوى الداخلي', async (path) => {
    useAuthMock.mockReturnValue(authState('entitymanager'));
    renderAt(path);

    expect(await screen.findByRole('heading', { name: 'الإحصائيات' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'لوحة التحكم' })).not.toBeInTheDocument();
  });

  it('المحامي على /documents يرى القائمة الداخلية (لا ارتداد)', async () => {
    useAuthMock.mockReturnValue(authState('lawyer'));
    renderAt('/documents');

    expect(await screen.findByRole('heading', { name: 'الملفات التنفيذية' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'الإحصائيات' })).not.toBeInTheDocument();
  });

  it('تغيير كلمة المرور حق شخصي يبقى متاحًا للمندوب', async () => {
    useAuthMock.mockReturnValue(authState('entitymanager'));
    renderAt('/change-password');

    expect(await screen.findByRole('heading', { name: 'تغيير كلمة المرور' })).toBeInTheDocument();
  });
});
