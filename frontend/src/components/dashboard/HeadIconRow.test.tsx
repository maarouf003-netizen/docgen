import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HeadIconRow } from './HeadIconRow';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: React.ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

const zero = { reviewsPending: 0, urgentCorrespondence: 0, delegationsPending: 0, entityPending: 0 };

describe('HeadIconRow', () => {
  it('يعرض البطاقات التسع بروابطها الصحيحة', () => {
    render(<HeadIconRow counts={zero} />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة رئيس القسم' });
    expect(nav.querySelectorAll('a')).toHaveLength(9);
    expect(screen.getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(screen.getByRole('link', { name: 'المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(screen.getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(screen.getByRole('link', { name: 'محامو الفرع' })).toHaveAttribute('href', '/branch-lawyers');
    expect(screen.getByRole('link', { name: 'طلبات الإنابة' })).toHaveAttribute(
      'href',
      '/delegations/requests',
    );
    expect(screen.getByRole('link', { name: 'مراجعة سجل الجهات' })).toHaveAttribute(
      'href',
      '/entities/review',
    );
    expect(screen.getByRole('link', { name: 'مندوبو الجهات' })).toHaveAttribute('href', '/delegates');
    expect(screen.getByRole('link', { name: 'سجل التدقيق' })).toHaveAttribute('href', '/audit-logs');
    expect(screen.getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute('href', '/account');
  });

  it('يُظهر الأجراس الأربعة بعدّاداتها في التسمية عند وجود تنبيه فقط', () => {
    render(
      <HeadIconRow
        counts={{ reviewsPending: 2, urgentCorrespondence: 0, delegationsPending: 5, entityPending: 1 }}
      />,
    );

    expect(
      screen.getByRole('link', { name: 'المطالعات — 2 كتب مطالعة بانتظار الرد' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'طلبات الإنابة — 5 طلبات إنابة معلّقة' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'مراجعة سجل الجهات — جهة واحدة بانتظار المراجعة' }),
    ).toBeInTheDocument();
    // بلا جرس عند الصفر — ولا على البطاقات الخمس الأخرى.
    expect(screen.getByRole('link', { name: 'المراسلات' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'الإحصائيات' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'محامو الفرع' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'مندوبو الجهات' })).toHaveAttribute('href', '/delegates');
    expect(screen.getByRole('link', { name: 'سجل التدقيق' })).toHaveAttribute('href', '/audit-logs');
  });

  it('يستخدم المفرد التام عند العدّ 1 (لا «1 جهات» ولا «1 كتب»)', () => {
    render(
      <HeadIconRow
        counts={{ reviewsPending: 1, urgentCorrespondence: 0, delegationsPending: 1, entityPending: 0 }}
      />,
    );

    expect(
      screen.getByRole('link', { name: 'المطالعات — كتاب مطالعة واحد بانتظار الرد' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'طلبات الإنابة — طلب إنابة معلّق واحد' }),
    ).toBeInTheDocument();
  });
});
