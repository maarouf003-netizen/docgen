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
  }: {
    children: ReactNode;
    to: string;
    end?: boolean;
    className?: (props: { isActive: boolean }) => string | undefined;
    onClick?: () => void;
  }) => (
    <a
      href={to}
      className={typeof className === 'function' ? className({ isActive: false }) : className}
      onClick={onClick}
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

  it('يعرض النسر والهوية وبيانات المستخدم في الشريط الجانبي دون العلم', () => {
    stubMatchMedia(false);
    render(<Layout />);

    expect(screen.getByAltText('شعار نسر صلاح الدين')).toBeInTheDocument();
    expect(screen.queryByAltText('علم الجمهورية العربية السورية')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'مسار' })).toBeInTheDocument();
    // شريط بيانات المستخدم ثابت ودائم الظهور في الصفحة الرئيسية.
    expect(screen.getByText('أحمد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('محامي — دمشق')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تسجيل الخروج' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'تغيير كلمة المرور' })).toBeInTheDocument();
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

  it('يقصر الشريط السفلي على أول 4 بنود مع زر «المزيد» على الجوال (أهداف لمس مريحة)', () => {
    useAuthMock.mockReturnValue({ ...baseUser(), hasFullAccess: true, isHead: true });
    stubMatchMedia(true);
    render(<Layout />);

    const bottomNav = screen.getByRole('navigation', { name: 'التنقل السفلي' });
    expect(within(bottomNav).getAllByRole('link')).toHaveLength(4);
    expect(within(bottomNav).getByRole('button', { name: /المزيد/ })).toHaveAttribute(
      'aria-haspopup',
      'dialog',
    );
    // بنود متأخرة لا تظهر كروابط مباشرة في الشريط.
    expect(within(bottomNav).queryByRole('link', { name: 'سجل التدقيق' })).not.toBeInTheDocument();
  });

  it('يبقي كل البنود ظاهرة كروابط في الشريط الجانبي المكتبية', () => {
    useAuthMock.mockReturnValue({ ...baseUser(), hasFullAccess: true, isHead: true });
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).getAllByRole('link').length).toBeGreaterThan(4);
  });

  it('يعرض روابط صلاحية خاصة فقط: نشاط المستخدمين للمدير وسجل التدقيق لرئيس القسم', async () => {
    useAuthMock.mockReturnValue({ ...baseUser(), hasFullAccess: true, isHead: true });
    stubMatchMedia(true);
    render(<Layout />);

    // على الجوال البنود المتأخرة خلف زر «المزيد» الذي يفتح درج التنقل.
    await userEvent.setup().click(screen.getByRole('button', { name: /المزيد/ }));
    const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
    expect(within(dialog).getByRole('link', { name: 'نشاط المستخدمين' })).toBeInTheDocument();
    expect(within(dialog).getByRole('link', { name: 'سجل التدقيق' })).toBeInTheDocument();
  });

  it('لا يعرض رابط «الملفات المحذوفة» في الشريط الجانبي لأي دور (انتقل داخل الملفات التنفيذية)', () => {
    for (const role of ['head', 'lawyer', 'admin']) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
      });
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

  it('يعرض «محامو الفرع» لرئيس القسم والمشرف ولا يعرضها للمدير', () => {
    for (const role of ['head', 'admin']) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
      });
      stubMatchMedia(true);
      const { unmount } = render(<Layout />);
      expect(screen.getByRole('link', { name: 'محامو الفرع' })).toHaveAttribute(
        'href',
        '/branch-lawyers',
      );
      unmount();
    }

    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'manager' },
    });
    render(<Layout />);
    expect(screen.queryByRole('link', { name: 'محامو الفرع' })).not.toBeInTheDocument();
  });

  it('يعرض «طلبات الإنابة» لرئيس القسم فقط', async () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'head' },
    });
    stubMatchMedia(true);
    render(<Layout />);

    await userEvent.setup().click(screen.getByRole('button', { name: /المزيد/ }));
    const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
    expect(within(dialog).getByRole('link', { name: 'طلبات الإنابة' })).toHaveAttribute(
      'href',
      '/delegations/requests',
    );
  });

  it('يخفي «طلبات الإنابة» عن غير رئيس القسم', () => {
    for (const role of ['lawyer', 'admin', 'manager']) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
      });
      stubMatchMedia(true);
      const { unmount } = render(<Layout />);
      expect(screen.queryByRole('link', { name: 'طلبات الإنابة' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('يعرض «إدارة المستخدمين» للمشرف فقط', async () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'admin' },
    });
    stubMatchMedia(true);
    const { unmount: unmountAdmin } = render(<Layout />);

    // بند متأخر على الجوال: خلف زر «المزيد».
    await userEvent.setup().click(screen.getByRole('button', { name: /المزيد/ }));
    const dialog = screen.getByRole('dialog', { name: 'قائمة التنقل' });
    expect(within(dialog).getByRole('link', { name: 'إدارة المستخدمين' })).toHaveAttribute(
      'href',
      '/users/manage',
    );
    unmountAdmin();

    for (const role of ['head', 'manager', 'lawyer']) {
      useAuthMock.mockReturnValue({
        ...baseUser(),
        user: { ...baseUser().user, role },
      });
      const { unmount } = render(<Layout />);
      expect(screen.queryByRole('link', { name: 'إدارة المستخدمين' })).not.toBeInTheDocument();
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

  it('يقصر قائمة مندوب الجهة على بوابته القرائية حصرًا (المرحلة 3)', () => {
    useAuthMock.mockReturnValue({
      ...baseUser(),
      user: { ...baseUser().user, role: 'entitymanager', fullName: 'مندوب الوزارة' },
    });
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).getByRole('link', { name: 'ملفات الجهة' })).toHaveAttribute('href', '/portal');
    expect(within(sidebar).queryByRole('link', { name: 'لوحة التحكم' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'الملفات التنفيذية' })).not.toBeInTheDocument();
    expect(within(sidebar).queryByRole('link', { name: 'سجل التدقيق' })).not.toBeInTheDocument();
  });
});
