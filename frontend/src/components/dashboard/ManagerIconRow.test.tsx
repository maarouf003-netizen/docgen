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
  it('يعرض للمدير 11 بطاقة بلا «محامو الفرع» ومع «الحساب الشخصي»', () => {
    render(<ManagerIconRow isAdmin={false} />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة المدير' });
    expect(within(nav).getAllByRole('link')).toHaveLength(11);
    expect(within(nav).getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(within(nav).getByRole('link', { name: 'كتب المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(within(nav).getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    // وصول إشرافي للتنبيهات — §18:3044 (كان مفقودًا للمدير/المشرف).
    expect(within(nav).getByRole('link', { name: 'التنبيهات' })).toHaveAttribute('href', '/alerts');
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

  it('يعرض شارة حمراء على بطاقة «اقتراحات التطوير» بعدد غير المقروء', () => {
    render(<ManagerIconRow suggestionsUnread={3} />);

    expect(
      screen.getByRole('link', { name: 'اقتراحات التطوير — 3 اقتراحات غير مقروءة' }),
    ).toHaveAttribute('href', '/suggestions');
  });

  it('يعرض للمشرف 12 بطاقة مع «محامو الفرع» و«الحساب الشخصي»', () => {
    render(<ManagerIconRow isAdmin />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة مشرف النظام' });
    expect(within(nav).getAllByRole('link')).toHaveLength(12);
    expect(within(nav).getByRole('link', { name: 'التنبيهات' })).toHaveAttribute('href', '/alerts');
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
