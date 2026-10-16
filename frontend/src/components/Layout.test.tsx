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

  it('يعرض شعار الإدارة والهوية وبطاقة الحساب للمحامي دون خروج/كلمة مرور في التذييل', () => {
    stubMatchMedia(false);
    render(<Layout />);

    expect(
      screen.getByAltText('شعار إدارة قضايا الدولة — الجمهورية العربية السورية'),
    ).toBeInTheDocument();
    expect(screen.getByText('منصة إدارة الملفات التنفيذية المدعومة بمولد مستندات آلي')).toBeInTheDocument();
    expect(screen.queryByAltText('علم الجمهورية العربية السورية')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'مسار' })).toBeInTheDocument();
    // بطاقة التذييل المضغوطة تنقل إلى الحساب الشخصي بدل بندي الخروج وكلمة المرور.
    expect(screen.getByRole('link', { name: /الحساب الشخصي/ })).toHaveAttribute('href', '/account');
    expect(screen.getByText('أحمد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('محامي — دمشق')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تسجيل الخروج' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'تغيير كلمة المرور' })).not.toBeInTheDocument();
  });

  it('يعرض للمدير/المشرف بطاقة حساب في التذييل بلا خروج/كلمة مرور (كالمحامي ورئيس القسم)', () => {
    for (const role of ['manager', 'admin'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
        hasFullAccess: true,
      });
      stubMatchMedia(false);
      const { unmount } = render(<Layout />);

      expect(screen.getByRole('link', { name: /الحساب الشخصي/ })).toHaveAttribute('href', '/account');
      expect(screen.queryByRole('button', { name: 'تسجيل الخروج' })).not.toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'تغيير كلمة المرور' })).not.toBeInTheDocument();
      unmount();
    }
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

  it('يعرض للمدير 4 بنود فقط مثل المحامي: اللوحة والملفات والمنتدى والمكتبة (بلا بنود صلاحيات)', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
      hasFullAccess: true,
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
    expect(within(sidebar).queryByRole('link', { name: 'إدارة المستخدمين' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'سجل التدقيق' })).not.toBeInTheDocument();
  });

  it('يعرض للمشرف 4 بنود فقط مثل المحامي (بلا بنود صلاحيات)', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'admin' },
      hasFullAccess: true,
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
  });

  it('لا يعرض «نشاط المستخدمين» المحذوف لأي دور (الصفحة والمسار محذوفان نهائيًا)', async () => {
    for (const role of ['manager', 'admin', 'head', 'lawyer'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
        hasFullAccess: role === 'manager' || role === 'admin',
        isHead: role === 'head',
      });
      stubMatchMedia(false);
      const { unmount } = render(<Layout />);
      const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
      expect(within(sidebar).queryByRole('link', { name: 'نشاط المستخدمين' })).not.toBeInTheDocument();
      unmount();
    }
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

  it('لا يعرض «محامو الفرع» في الشريط لأي دور (يُفتح من بطاقة لوحة المشرف)', async () => {
    for (const role of ['admin', 'manager', 'head', 'lawyer'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
        isHead: role === 'head',
        hasFullAccess: role === 'admin' || role === 'manager',
      });
      stubMatchMedia(false);
      const { unmount } = render(<Layout />);
      const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
      expect(within(sidebar).queryByRole('link', { name: 'محامو الفرع' })).not.toBeInTheDocument();
      unmount();
    }
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

  it('لا يعرض «إدارة المستخدمين» في الشريط لأي دور (تُفتح من بطاقة اللوحة)', async () => {
    for (const role of ['admin', 'manager', 'head', 'lawyer'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
        isHead: role === 'head',
      });
      stubMatchMedia(false);
      const { unmount } = render(<Layout />);
      const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
      expect(within(sidebar).queryByRole('link', { name: 'إدارة المستخدمين' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('لا يعرض «اقتراحات التطوير» في الشريط لأي دور داخلي (تُفتح من بطاقة اللوحة)', async () => {
    for (const role of ['admin', 'manager', 'head', 'lawyer'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
        isHead: role === 'head',
      });
      stubMatchMedia(false);
      const { unmount } = render(<Layout />);
      const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
      expect(
        within(sidebar).queryByRole('link', { name: 'اقتراحات التطوير' }),
      ).not.toBeInTheDocument();
      unmount();
    }
  });

  it('لا يعرض «إدارة الفروع» في الشريط لأي دور (تُفتح من بطاقة اللوحة)', async () => {
    for (const role of ['admin', 'manager'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
        hasFullAccess: true,
      });
      stubMatchMedia(false);
      const { unmount } = render(<Layout />);
      const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
      expect(within(sidebar).queryByRole('link', { name: 'إدارة الفروع' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('يعرض بنود المدير والمشرف الأربعة في الشريط السفلي بلا زر «المزيد» على الجوال', () => {
    for (const role of ['admin', 'manager'] as const) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
      });
      stubMatchMedia(true);
      const { unmount } = render(<Layout />);
      const bottomNav = screen.getByRole('navigation', { name: 'التنقل السفلي' });
      expect(within(bottomNav).getAllByRole('link')).toHaveLength(3);
      expect(within(bottomNav).getAllByRole('button')).toHaveLength(1);
      expect(within(bottomNav).queryByRole('button', { name: /المزيد/ })).not.toBeInTheDocument();
      unmount();
    }
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
