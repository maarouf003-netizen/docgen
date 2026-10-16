import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { ReactNode } from 'react';
import { ManagerIconRow } from './ManagerIconRow';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

describe('ManagerIconRow', () => {
  it('يعرض للمدير 10 بطاقات بلا «محامو الفرع» ومع «الحساب الشخصي»', () => {
    render(<ManagerIconRow isAdmin={false} />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة المدير' });
    expect(within(nav).getAllByRole('link')).toHaveLength(10);
    expect(within(nav).getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(within(nav).getByRole('link', { name: 'كتب المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(within(nav).getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(within(nav).getByRole('link', { name: 'اقتراحات التطوير' })).toHaveAttribute(
      'href',
      '/suggestions',
    );
    expect(within(nav).getByRole('link', { name: 'مراجعة سجل الجهات العامة' })).toHaveAttribute(
      'href',
      '/entities/review-management',
    );
    expect(within(nav).getByRole('link', { name: 'مندوبو الجهات' })).toHaveAttribute(
      'href',
      '/delegates',
    );
    expect(within(nav).getByRole('link', { name: 'إدارة المستخدمين' })).toHaveAttribute(
      'href',
      '/users/manage',
    );
    expect(within(nav).getByRole('link', { name: 'إدارة الفروع' })).toHaveAttribute(
      'href',
      '/branches/manage',
    );
    expect(within(nav).getByRole('link', { name: 'سجل التدقيق' })).toHaveAttribute(
      'href',
      '/audit-logs',
    );
    expect(within(nav).getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute(
      'href',
      '/account',
    );
    expect(within(nav).queryByRole('link', { name: 'محامو الفرع' })).not.toBeInTheDocument();
  });

  it('يعرض للمشرف 11 بطاقة مع «محامو الفرع» و«الحساب الشخصي»', () => {
    render(<ManagerIconRow isAdmin />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة مشرف النظام' });
    expect(within(nav).getAllByRole('link')).toHaveLength(11);
    expect(within(nav).getByRole('link', { name: 'محامو الفرع' })).toHaveAttribute(
      'href',
      '/branch-lawyers',
    );
    expect(within(nav).getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute(
      'href',
      '/account',
    );
  });
});
