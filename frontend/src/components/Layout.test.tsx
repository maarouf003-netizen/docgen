import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import Layout from './Layout';

vi.mock('react-router-dom', () => ({
  NavLink: ({
    children,
    to,
    end: _end,
    className,
    onClick,
    ...rest
  }: {
    children: ReactNode;
    to: string;
    end?: boolean;
    className?: (props: { isActive: boolean }) => string | undefined;
    onClick?: () => void;
  } & Record<string, unknown>) => (
    <a
      href={to}
      className={typeof className === 'function' ? className({ isActive: false }) : className}
      onClick={onClick}
      {...rest}
    >
      {children}
    </a>
  ),
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
  Outlet: () => <div>محتوى الصفحة</div>,
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('./NetworkStatusBanner', () => ({
  default: () => null,
}));

function stubMatchMedia(matches: boolean) {
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

function baseUser() {
  return {
    user: {
      fullName: 'أحمد الخطيب',
      role: 'lawyer',
      branchName: 'دمشق',
    },
    logout: vi.fn(),
    hasFullAccess: false,
    isHead: false,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  useAuthMock.mockReturnValue(baseUser());
});

describe('Layout', () => {
  it('يعرض الشريط الجانبي على الشاشة المكتبية دون زر القائمة', () => {
    stubMatchMedia(false);
    render(<Layout />);

    expect(screen.getByRole('navigation', { name: 'القائمة الرئيسية' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'التنقل السفلي' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'فتح القائمة' })).not.toBeInTheDocument();
  });

  it('يعرض النسر والهوية وبطاقة الحساب للمحامي دون خروج/كلمة مرور في التذييل', () => {
    stubMatchMedia(false);
    render(<Layout />);

    expect(screen.getByAltText('شعار نسر صلاح الدين')).toBeInTheDocument();
    expect(screen.queryByAltText('علم الجمهورية العربية السورية')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'مسار' })).toBeInTheDocument();
    // بطاقة التذييل المضغوطة تنقل إلى الحساب الشخصي بدل بندي الخروج وكلمة المرور.
    expect(screen.getByRole('link', { name: /الحساب الشخصي/ })).toHaveAttribute('href', '/account');
    expect(screen.getByText('أحمد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('محامي — دمشق')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسجيل الخروج' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'تغيير كلمة المرور' })).not.toBeInTheDocument();
  });

  it('يبقي تسجيل الخروج وتغيير كلمة المرور في التذييل للمدير/المشرف لا لرئيس القسم', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
      hasFullAccess: true,
    });
    stubMatchMedia(false);
    render(<Layout />);

    expect(screen.getByRole('button', { name: 'تسجيل الخروج' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'تغيير كلمة المرور' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /الحساب الشخصي/ })).not.toBeInTheDocument();
  });

  it('يعرض لرئيس القسم بطاقة حساب في التذييل بلا خروج/كلمة مرور (كالمحامي)', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'head' },
      isHead: true,
    });
    stubMatchMedia(false);
    render(<Layout />);

    expect(screen.getByRole('link', { name: /الحساب الشخصي/ })).toHaveAttribute('href', '/account');
    expect(screen.getByText('رئيس قسم — دمشق')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسجيل الخروج' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'تغيير كلمة المرور' })).not.toBeInTheDocument();
  });

  it('يعرض للمحامي 4 بنود فقط: اللوحة والملفات والمنتدى والمكتبة (بلا مطالعات/مراسلات)', () => {
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).getByRole('link', { name: 'لوحة التحكم' })).toHaveAttribute('href', '/');
    expect(within(sidebar).getByRole('link', { name: 'الملفات التنفيذية' })).toHaveAttribute(
      'href',
      '/documents',
    );
    expect(within(sidebar).getByRole('link', { name: /المنتدى/ })).toHaveAttribute('href', '/forum');
    expect(within(sidebar).getByRole('button', { name: /المكتبة/ })).toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'المطالعات' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'المراسلات' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('button', { name: 'المزيد' })).not.toBeInTheDocument();
  });

  it('يعرض بنود المحامي الأربعة في الشريط السفلي بلا زر «المزيد» على الجوال', () => {
    stubMatchMedia(true);
    render(<Layout />);

    const bottomNav = screen.getByRole('navigation', { name: 'التنقل السفلي' });
    expect(within(bottomNav).getAllByRole('link')).toHaveLength(3);
    expect(within(bottomNav).getAllByRole('button')).toHaveLength(1);
    expect(within(bottomNav).queryByRole('button', { name: /المزيد/ })).not.toBeInTheDocument();
  });

  it('يفتح تنبيه «قيد البناء» عند نقر المكتبة ويغلقه زر الإغلاق', async () => {
    const user = userEvent.setup();
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    await user.click(within(sidebar).getByRole('button', { name: /المكتبة/ }));
    expect(screen.getByRole('status')).toHaveTextContent('الميزة قيد البناء حاليا');

    await user.click(screen.getByRole('button', { name: 'إغلاق التنبيه' }));
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('يعرض زر القائمة والتنقل السفلي على شاشة الموبايل', () => {
    stubMatchMedia(true);
    render(<Layout />);

    expect(screen.queryByRole('navigation', { name: 'القائمة الرئيسية' })).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'التنقل السفلي' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'فتح القائمة' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('يفتح الدرج عند النقر على زر القائمة ويغلقه عند النقر على رابط', async () => {
    const user = userEvent.setup();
    stubMatchMedia(true);
    render(<Layout />);

    await user.click(screen.getByRole('button', { name: 'فتح القائمة' }));
    const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
    expect(dialog).toBeInTheDocument();

    await user.click(within(dialog).getByRole('link', { name: 'الملفات التنفيذية' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('يعرض لرئيس القسم 4 بنود فقط: اللوحة والملفات والمنتدى والمكتبة (بلا بنود الصلاحيات)', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'head' },
      isHead: true,
    });
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).getByRole('link', { name: 'لوحة التحكم' })).toHaveAttribute('href', '/');
    expect(within(sidebar).getByRole('link', { name: 'الملفات التنفيذية' })).toHaveAttribute(
      'href',
      '/documents',
    );
    expect(within(sidebar).getByRole('link', { name: /المنتدى/ })).toHaveAttribute('href', '/forum');
    expect(within(sidebar).getByRole('button', { name: /المكتبة/ })).toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'كتب المطالعات' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'المراسلات' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'طلبات الإنابة' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'سجل التدقيق' })).not.toBeInTheDocument();
  });

  it('يعرض بنود رئيس القسم الأربعة في الشريط السفلي بلا زر «المزيد» على الجوال', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'head' },
      isHead: true,
    });
    stubMatchMedia(true);
    render(<Layout />);

    const bottomNav = screen.getByRole('navigation', { name: 'التنقل السفلي' });
    expect(within(bottomNav).getAllByRole('link')).toHaveLength(3);
    expect(within(bottomNav).getAllByRole('button')).toHaveLength(1);
    expect(within(bottomNav).queryByRole('button', { name: /المزيد/ })).not.toBeInTheDocument();
  });

  it('يبقي كل بنود الصلاحيات ظاهرة كروابط في الشريط الجانبي المكتبية للمدير', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
      hasFullAccess: true,
    });
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).getAllByRole('link').length).toBeGreaterThan(4);
  });

  it('يعرض روابط صلاحية خاصة فقط: نشاط المستخدمين للمدير (لا بنود صلاحيات لرئيس القسم)', async () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
      hasFullAccess: true,
    });
    stubMatchMedia(true);
    render(<Layout />);

    // على الجوال البنود المتأخرة خلف زر «المزيد» الذي يفتح درج التنقل.
    await userEvent.setup().click(screen.getByRole('button', { name: /المزيد/ }));
    const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
    expect(within(dialog).getByRole('link', { name: 'نشاط المستخدمين' })).toBeInTheDocument();
  });

  it('لا يعرض رابط «الملفات المحذوفة» في الشريط الجانبي لأي دور (انتقل داخل الملفات التنفيذية)', () => {
    for (const [role, flags] of [
      ['head', { isHead: true }],
      ['lawyer', {}],
      ['admin', { hasFullAccess: true }],
    ] as const) {
      useAuthMock.mockReturnValue({ ...baseUser(), user: { ...baseUser().user, role }, ...flags });
      stubMatchMedia(true);
      const { unmount } = render(<Layout />);
      expect(screen.queryByRole('link', { name: 'الملفات المحذوفة' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('لا يعرض رابط «الملفات المحذوفة» عن المدير في الشريط الجانبي', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
    });
    stubMatchMedia(true);
    render(<Layout />);

    expect(screen.queryByRole('link', { name: 'الملفات المحذوفة' })).not.toBeInTheDocument();
  });

  it('يخفي روابط الصلاحيات الخاصة عن المحامي', () => {
    stubMatchMedia(true);
    render(<Layout />);

    expect(screen.queryByRole('link', { name: 'نشاط المستخدمين' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'سجل التدقيق' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'محامو الفرع' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'إدارة المستخدمين' })).not.toBeInTheDocument();
  });

  it('يعرض «محامو الفرع» للمشرف في الشريط ولا يعرضه لرئيس القسم (بطاقة لوحة) ولا للمدير', async () => {
    // المشرف: البند في الشريط (خلف «المزيد» على الجوال).
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'admin' },
    });
    stubMatchMedia(true);
    const { unmount } = render(<Layout />);
    await userEvent.setup().click(screen.getByRole('button', { name: /المزيد/ }));
    const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
    expect(within(dialog).getByRole('link', { name: 'محامو الفرع' })).toHaveAttribute(
      'href',
      '/branch-lawyers',
    );
    unmount();

    // رئيس القسم: لا بند في الشريط إطلاقًا — يُفتح من بطاقة اللوحة.
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'head' },
      isHead: true,
    });
    stubMatchMedia(false);
    const second = render(<Layout />);
    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).queryByRole('link', { name: 'محامو الفرع' })).not.toBeInTheDocument();
    second.unmount();

    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
    });
    render(<Layout />);
    expect(screen.queryByRole('link', { name: 'محامو الفرع' })).not.toBeInTheDocument();
  });

  it('يُخفي بند «المراسلات» عن المحامي ورئيس القسم (تُفتح من بطاقة اللوحة) ويُبقيه للمندوب', async () => {
    // محامي (مكتبي): لا بند مراسلات في القائمة الرئيسية.
    stubMatchMedia(false);
    const { unmount } = render(<Layout />);
    expect(screen.queryByRole('link', { name: 'المراسلات' })).not.toBeInTheDocument();
    unmount();

    // رئيس قسم (مكتبي): لا بند مراسلات في الشريط — يُفتح من بطاقة اللوحة.
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'head' },
      isHead: true,
    });
    stubMatchMedia(false);
    const second = render(<Layout />);
    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).queryByRole('link', { name: 'المراسلات' })).not.toBeInTheDocument();
    second.unmount();

    // مندوب جهة: بند «المراسلات» في الشريط السفلي للبوابة.
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'entitymanager' },
    });
    stubMatchMedia(true);
    render(<Layout />);
    const portalNav = screen.getByRole('navigation', { name: 'التنقل السفلي' });
    expect(within(portalNav).getByRole('link', { name: 'المراسلات' })).toHaveAttribute(
      'href',
      '/portal/correspondence',
    );
  });

  it('لا يعرض «طلبات الإنابة» في الشريط لأي دور (تُفتح من بطاقة اللوحة)', () => {
    for (const [role, flags] of [
      ['head', { isHead: true }],
      ['manager', { hasFullAccess: true }],
      ['admin', { hasFullAccess: true }],
      ['lawyer', {}],
    ] as const) {
      useAuthMock.mockReturnValue({ ...baseUser(), user: { ...baseUser().user, role }, ...flags });
      stubMatchMedia(false);
      const { unmount } = render(<Layout />);
      const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
      expect(within(sidebar).queryByRole('link', { name: 'طلبات الإنابة' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('يعرض «إدارة المستخدمين» للمشرف والمدير (`BQ-001د`) لا لغيرهما', async () => {
    for (const role of ['admin', 'manager'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
      });
      stubMatchMedia(true);
      const { unmount } = render(<Layout />);

      // بند متأخر على الجوال: خلف زر «المزيد».
      await userEvent.setup().click(screen.getByRole('button', { name: /المزيد/ }));
      const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
      expect(within(dialog).getByRole('link', { name: 'إدارة المستخدمين' })).toHaveAttribute(
        'href',
        '/users/manage',
      );
      unmount();
    }

    for (const [role, flags] of [
      ['head', { isHead: true }],
      ['lawyer', {}],
    ] as const) {
      useAuthMock.mockReturnValue({ ...baseUser(), user: { ...baseUser().user, role }, ...flags });
      const { unmount } = render(<Layout />);
      expect(screen.queryByRole('link', { name: 'إدارة المستخدمين' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('يعرض «إدارة الفروع» للمشرف فقط (لا للمدير)', async () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'admin' },
    });
    stubMatchMedia(true);
    const { unmount: unmountAdmin } = render(<Layout />);
    await userEvent.setup().click(screen.getByRole('button', { name: /المزيد/ }));
    expect(
      within(screen.getByRole('dialog', { name: 'قائمة التنقل' })).getByRole('link', { name: 'إدارة الفروع' }),
    ).toHaveAttribute('href', '/branches/manage');
    unmountAdmin();

    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
      hasFullAccess: true,
    });
    const { unmount } = render(<Layout />);
    expect(screen.queryByRole('link', { name: 'إدارة الفروع' })).not.toBeInTheDocument();
    unmount();
  });

  it('يبقي الشريط الجانبي على الموبايل داخل الدرج عند فتحه', async () => {
    const user = userEvent.setup();
    stubMatchMedia(true);
    render(<Layout />);

    await user.click(screen.getByRole('button', { name: 'فتح القائمة' }));
    const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
    expect(dialog).toBeInTheDocument();
  });

  it('يقصر قائمة مندوب الجهة على بوابته القرائية حصرًا (الإحصائيات + الملفات + المراسلات)', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'entitymanager', fullName: 'مندوب الوزارة' },
    });
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/portal/stats');
    expect(within(sidebar).getByRole('link', { name: 'الملفات التنفيذية' })).toHaveAttribute('href', '/portal/files');
    expect(within(sidebar).getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/portal/correspondence');
    expect(within(sidebar).queryByRole('link', { name: 'لوحة التحكم' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'سجل التدقيق' })).not.toBeInTheDocument();
  });

  it('يعرض تسمية دور المندوب «مندوب جهة» في الشريط الجانبي', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'entitymanager', fullName: 'مندوب الوزارة' },
    });
    stubMatchMedia(false);
    render(<Layout />);

    expect(screen.getByText('مندوب جهة — دمشق')).toBeInTheDocument();
  });

  it('لا يعرض أي جرس مطالعات/مراسلات لرئيس القسم (مكتبي ولا جوال) — الشارات في بطاقات اللوحة', () => {
    const hrefs = () =>
      screen.getAllByRole('link').map((a) => (a as HTMLAnchorElement).getAttribute('href'));
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'head' },
      isHead: true,
    });

    stubMatchMedia(false);
    const { unmount } = render(<Layout />);
    expect(hrefs()).not.toContain('/reviews');
    expect(hrefs()).not.toContain('/correspondence');
    unmount();

    stubMatchMedia(true);
    render(<Layout />);
    expect(hrefs()).not.toContain('/reviews');
    expect(hrefs()).not.toContain('/correspondence');
  });

  it('يُبقي جرس المراسلات للمحامي في شريط الجوال العلوي', () => {
    stubMatchMedia(true);
    render(<Layout />);

    // الشريط السفلي للمحامي رابطان فقط (/ + /documents) — رابط /correspondence الوحيد هو جرس الشريط العلوي.
    const bells = screen
      .getAllByRole('link')
      .filter((a) => (a as HTMLAnchorElement).getAttribute('href') === '/correspondence');
    expect(bells).toHaveLength(1);
  });
});
