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
    isSubHead: role === 'subhead',
    isHeadOrSubHead: role === 'head' || role === 'subhead',
    sectionId: null,
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
  localStorage.clear();
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

  it('المحامي على / يرى لوحته الجديدة (ترحيب + أقسام) بدل عنوان «لوحة التحكم»', async () => {
    useAuthMock.mockReturnValue(authState('lawyer'));
    renderAt('/');

    // أول تركيب للوحة يحوّل الكتلة الكسولة — مهلة موسعة ضد التقطع الحدي (قيس 1710ms محليًا).
    expect(await screen.findByRole('heading', { name: 'مرحبًا، مستخدم' }, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'أقسام لوحة المحامي' })).toBeInTheDocument();
  });

  it('المحامي ورئيس القسم على /account يريان حسابيهما، وغيرهما يُرتد إلى وطنه', async () => {
    useAuthMock.mockReturnValue(authState('lawyer'));
    const { unmount } = renderAt('/account');
    expect(await screen.findByRole('heading', { name: 'الحساب الشخصي' })).toBeInTheDocument();
    unmount();

    useAuthMock.mockReturnValue(authState('head'));
    const { unmount: unmount2 } = renderAt('/account');
    expect(await screen.findByRole('heading', { name: 'الحساب الشخصي' })).toBeInTheDocument();
    unmount2();

    useAuthMock.mockReturnValue(authState('manager'));
    renderAt('/account');
    expect(await screen.findByRole('heading', { name: 'لوحة التحكم' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'الحساب الشخصي' })).not.toBeInTheDocument();
  });

  it('المحامي على /stats و/calendar يرى الصفحتين، ورئيس القسم على /stats يرى فرعه، وغيرهما يُرتد', async () => {
    useAuthMock.mockReturnValue(authState('lawyer'));
    const { unmount } = renderAt('/stats');
    expect(await screen.findByRole('heading', { name: 'الإحصائيات' })).toBeInTheDocument();
    unmount();

    useAuthMock.mockReturnValue(authState('lawyer'));
    const { unmount: unmount2 } = renderAt('/calendar');
    expect(await screen.findByRole('heading', { name: 'التقويم' })).toBeInTheDocument();
    unmount2();

    useAuthMock.mockReturnValue(authState('head'));
    const { unmount: unmount3 } = renderAt('/stats');
    expect(await screen.findByRole('heading', { name: 'الإحصائيات' })).toBeInTheDocument();
    unmount3();

    useAuthMock.mockReturnValue(authState('manager'));
    renderAt('/stats');
    expect(await screen.findByRole('heading', { name: 'لوحة التحكم' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'الإحصائيات' })).not.toBeInTheDocument();
  });

  it('المشرف والمدير على /suggestions يريان الصندوق، والمحامي يُرتد إلى لوحته (`BQ-001`)', async () => {
    for (const role of ['admin', 'manager'] as const) {
      useAuthMock.mockReturnValue(authState(role));
      const { unmount } = renderAt('/suggestions');
      expect(await screen.findByRole('heading', { name: 'اقتراحات التطوير' })).toBeInTheDocument();
      unmount();
    }

    useAuthMock.mockReturnValue(authState('lawyer'));
    renderAt('/suggestions');
    expect(await screen.findByRole('heading', { name: 'مرحبًا، مستخدم' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'اقتراحات التطوير' })).not.toBeInTheDocument();
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

  it.each([
    ['/account', 'الحساب الشخصي'],
    ['/stats', 'الإحصائيات'],
    ['/branch-lawyers', 'محامو الفرع'],
    ['/delegations/requests', 'طلبات الإنابة'],
    ['/execution-circuits', 'إدارة دوائر التنفيذ'],
    ['/circuit-stats', 'إحصائيات الدوائر'],
    ['/entities/review', 'مراجعة سجل الجهات العامة الممثلة'],
    ['/delegates', 'مندوبو الجهات'],
  ])('رئيس الشعبة على %s يرى الصفحة (نطاقه)', async (path, heading) => {
    useAuthMock.mockReturnValue(authState('subhead'));
    renderAt(path);

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
  });

  it('رئيس الشعبة على /audit-logs يرى الصفحة (النطاق الدائري §2.23 مطبَّق خلفيًا)', async () => {
    useAuthMock.mockReturnValue(authState('subhead'));
    renderAt('/audit-logs');

    expect(await screen.findByRole('heading', { name: 'سجل التدقيق' })).toBeInTheDocument();
  });

  it('المدير على /branches/manage يرى إدارة الفروع (توسيع §2.15)', async () => {
    useAuthMock.mockReturnValue(authState('manager'));
    renderAt('/branches/manage');

    expect(await screen.findByRole('heading', { name: 'إدارة الفروع' })).toBeInTheDocument();
  });

  // مصفوفة حراسة المسارات الكاملة (المرحلة 9 — بند 2): كل مسار × كل دور.
  // المسموح يرى عنوان الصفحة؛ المرفوض يُرتد لوطنه (لوحته/بوابته) بلا عنوانها.
  // أي مسار جديد أو بوابة معدلة يجب تسجيلها هنا — وإلا فشلت المصفوفة عمدًا.
  // مستثنى عمدًا: صفحات التفاصيل المفردة وصفحات تُخفي عنوانها عند فشل الجلب
  // (`/documents/:id`، `/documents/:id/edit`، `/documents/:id/correspondence`،
  // `/users`) — بواباتها نفس آلية `RequireRole` المغطاة هنا، وعناوينها
  // غير صالحة كمؤشر سماح تحت `rejectAllApi`.
  const ALL_ROLES = ['lawyer', 'head', 'subhead', 'manager', 'admin', 'entitymanager'];
  const INTERNAL = ['lawyer', 'head', 'subhead', 'manager', 'admin'];
  const MATRIX: Array<{ path: string; allowed: string[]; heading: (role: string) => string }> = [
    { path: '/documents', allowed: INTERNAL, heading: () => 'الملفات التنفيذية' },
    { path: '/reviews', allowed: INTERNAL, heading: (r) => (r === 'lawyer' ? 'المطالعات' : 'كتب المطالعات') },
    { path: '/correspondence', allowed: INTERNAL, heading: () => 'المراسلات' },
    { path: '/appeals', allowed: INTERNAL, heading: () => 'الاستئنافات' },
    { path: '/documents/deleted', allowed: INTERNAL, heading: () => 'الملفات المحذوفة' },
    { path: '/documents/struck-off', allowed: INTERNAL, heading: () => 'الملفات المشطوبة' },
    { path: '/documents/executed', allowed: INTERNAL, heading: () => 'الملفات المنفذة' },
    { path: '/documents/referred-to-start', allowed: INTERNAL, heading: () => 'الملفات المحالة الى البداية' },
    { path: '/documents/new', allowed: INTERNAL, heading: () => 'إدخال ملف جديد' },
    { path: '/branch-lawyers', allowed: ['head', 'subhead', 'admin'], heading: () => 'محامو الفرع' },
    { path: '/delegations/requests', allowed: ['head', 'subhead'], heading: () => 'طلبات الإنابة' },
    { path: '/execution-circuits', allowed: ['head', 'subhead'], heading: () => 'إدارة دوائر التنفيذ' },
    { path: '/circuit-stats', allowed: ['head', 'subhead', 'manager', 'admin'], heading: () => 'إحصائيات الدوائر' },
    { path: '/users/manage', allowed: ['manager', 'admin'], heading: () => 'إدارة المستخدمين' },
    { path: '/branches/manage', allowed: ['manager', 'admin'], heading: () => 'إدارة الفروع' },
    { path: '/entities/review-management', allowed: ['manager', 'admin'], heading: () => 'مراجعة سجل الجهات العامة' },
    { path: '/entities/review', allowed: ['head', 'subhead', 'manager', 'admin'], heading: () => 'مراجعة سجل الجهات العامة الممثلة' },
    { path: '/delegates', allowed: ['head', 'subhead', 'manager', 'admin'], heading: () => 'مندوبو الجهات' },
    { path: '/audit-logs', allowed: ['head', 'subhead', 'manager', 'admin'], heading: () => 'سجل التدقيق' },
    { path: '/account', allowed: ['lawyer', 'head', 'subhead'], heading: () => 'الحساب الشخصي' },
    { path: '/stats', allowed: ['lawyer', 'head', 'subhead'], heading: () => 'الإحصائيات' },
    { path: '/calendar', allowed: ['lawyer'], heading: () => 'التقويم' },
    { path: '/documents/rotate', allowed: ['lawyer'], heading: () => 'تدوير أرقام الأساس' },
    { path: '/pending-registrations', allowed: ['lawyer'], heading: () => 'ملفات محالة حديثًا' },
    { path: '/suggestions', allowed: ['manager', 'admin'], heading: () => 'اقتراحات التطوير' },
    { path: '/change-password', allowed: ALL_ROLES, heading: () => 'تغيير كلمة المرور' },
    { path: '/portal/stats', allowed: ['entitymanager'], heading: () => 'الإحصائيات' },
    { path: '/portal/files', allowed: ['entitymanager'], heading: () => 'الملفات التنفيذية' },
  ];

  // عنوان الوطن لكل دور عند الارتداد (لوحة الدور نفسه لا عنوان الصفحة).
  function homeMarker(role: string): string {
    if (role === 'entitymanager') return 'الإحصائيات';
    if (role === 'manager' || role === 'admin') return 'لوحة التحكم';
    return 'مرحبًا، مستخدم';
  }

  it.each(
    MATRIX.flatMap((row) => ALL_ROLES.map((role) => ({ ...row, role }))),
  )('$role على $path: مسموح=$allowed', async ({ path, allowed, heading, role }) => {
    useAuthMock.mockReturnValue(authState(role));
    const { unmount } = renderAt(path);
    try {
      if (allowed.includes(role)) {
        expect(await screen.findByRole('heading', { name: heading(role) })).toBeInTheDocument();
      } else {
        // انتظر استقرار الارتداد ثم تأكد من غياب عنوان الصفحة المحروسة.
        // (استثناء: وطن المندوب عنوانه «الإحصائيات» نفسه فيطابق العنوان — يكفي حضوره.)
        expect(await screen.findByRole('heading', { name: homeMarker(role) })).toBeInTheDocument();
        if (homeMarker(role) !== heading(role)) {
          expect(screen.queryByRole('heading', { name: heading(role) })).not.toBeInTheDocument();
        }
      }
    } finally {
      unmount();
    }
  });
});
