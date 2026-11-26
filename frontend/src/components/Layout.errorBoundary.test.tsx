import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import Layout from './Layout';

vi.mock('../auth/useAuth', () => ({
  useAuth: () => ({
    user: { fullName: 'مدير النظام', role: 'admin', branchName: 'دمشق' },
    logout: vi.fn(),
    hasFullAccess: true,
    isHead: false,
    isSubHead: false,
    isHeadOrSubHead: false,
  }),
}));

vi.mock('../api/client', () => ({
  api: {
    get: vi.fn().mockResolvedValue({ data: { count: 0 } }),
  },
}));

vi.mock('./NetworkStatusBanner', () => ({
  default: () => null,
}));

function BrokenPage(): never {
  throw new Error('render failed');
}

function stubDesktop() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
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

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<div>الصفحة الرئيسية</div>} />
          <Route path="/boom" element={<BrokenPage />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.spyOn(console, 'error').mockImplementation(() => {});
  stubDesktop();
});

describe('Layout — حدود خطأ المحتوى', () => {
  it('يبقي التنقل الجانبي عاملًا عند تعطّل الصفحة، والانتقال يغادر شاشة الخطأ', async () => {
    const user = userEvent.setup();
    renderAt('/boom');

    // الصفحة انهارت داخل main، لكن الشريط الجانبي ما زال معروضًا وقابلًا للنقر.
    expect(screen.getByRole('alert')).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'القائمة الرئيسية' });
    expect(nav).toBeInTheDocument();

    await user.click(within(nav).getByRole('link', { name: 'لوحة التحكم' }));

    expect(screen.getByText('الصفحة الرئيسية')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
