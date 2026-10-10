import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { api } from '../api/client';
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

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: () => 'تعذر تنفيذ الطلب، حاول مجدداً',
}));

const apiGet = api.get as unknown as ReturnType<typeof vi.fn>;

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

function authFor(role: string, hasFullAccess = false) {
  return {
    user: { id: 7, fullName: 'مستخدم', role, branchName: 'دمشق' },
    logout: vi.fn(),
    hasFullAccess,
    isHead: role === 'head',
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  apiGet.mockImplementation((url: string) => {
    if (url === '/forum/unread-count') return Promise.resolve({ data: { count: 0 } });
    return Promise.resolve({ data: { count: 0 } });
  });
});

describe('شارة المنتدى في التنقل', () => {
  it('تعرض رابط المنتدى بشارة العدّاد و`aria-label` للمحامي', async () => {
    apiGet.mockResolvedValue({ data: { count: 5 } });
    useAuthMock.mockReturnValue(authFor('lawyer'));
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    const link = within(sidebar).getByRole('link', { name: /المنتدى/ });
    expect(link).toHaveAttribute('href', '/forum');
    await waitFor(() => {
      expect(within(sidebar).getByLabelText('المنتدى: 5 تحتاج انتباهك')).toHaveTextContent('5');
    });
  });

  it('تُسقّف الشارة بـ`+99` وتظهر للمدير أيضًا', async () => {
    apiGet.mockResolvedValue({ data: { count: 150 } });
    useAuthMock.mockReturnValue(authFor('manager', true));
    stubMatchMedia(false);
    render(<Layout />);

    const sidebar = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(within(sidebar).getByRole('link', { name: /المنتدى/ })).toHaveAttribute('href', '/forum');
    await waitFor(() => {
      expect(within(sidebar).getByLabelText('المنتدى: 150 تحتاج انتباهك')).toHaveTextContent('+99');
    });
  });

  it('لا تعرض المنتدى ولا تستطلع عدّاده للمندوب', async () => {
    useAuthMock.mockReturnValue(authFor('entitymanager'));
    stubMatchMedia(false);
    render(<Layout />);

    // انتظار اكتمال التركيب (آثار تُدفَّق) ثم الجزم بعدم الاستطلاع.
    await screen.findByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(screen.queryByRole('link', { name: /المنتدى/ })).not.toBeInTheDocument();
    expect(apiGet.mock.calls.filter((c) => c[0] === '/forum/unread-count')).toHaveLength(0);
  });
});
