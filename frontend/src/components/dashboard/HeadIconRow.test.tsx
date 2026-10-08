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

const zero = { reviewsPending: 0, urgentCorrespondence: 0, delegationsPending: 0, entityPending: 0, circuitsPending: 0 };

describe('HeadIconRow', () => {
  it('يعرض البطاقات العشر بروابطها الصحيحة', () => {
    render(<HeadIconRow counts={zero} />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة رئيس القسم' });
    expect(nav.querySelectorAll('a')).toHaveLength(10);
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
    expect(screen.getByRole('link', { name: 'إدارة دوائر التنفيذ' })).toHaveAttribute(
      'href',
      '/execution-circuits',
    );
    expect(screen.getByText('سجل دوائر التنفيذ')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'سجل التدقيق' })).toHaveAttribute('href', '/audit-logs');
    expect(screen.getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute('href', '/account');
  });

  it('يُظهر الأجراس الأربعة بعدّاداتها في التسمية عند وجود تنبيه فقط', () => {
    render(
      <HeadIconRow
        counts={{ reviewsPending: 2, urgentCorrespondence: 0, delegationsPending: 5, entityPending: 1, circuitsPending: 0 }}
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
    // بلا جرس عند الصفر — ولا على البطاقات الأخرى.
    expect(screen.getByRole('link', { name: 'المراسلات' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'الإحصائيات' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'محامو الفرع' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'إدارة دوائر التنفيذ' })).toHaveAttribute('href', '/execution-circuits');
    expect(screen.getByRole('link', { name: 'مندوبو الجهات' })).toHaveAttribute('href', '/delegates');
    expect(screen.getByRole('link', { name: 'سجل التدقيق' })).toHaveAttribute('href', '/audit-logs');
  });

  it('يُظهر شارة معلقات الدوائر على بطاقة الإدارة عند وجود انتظار', () => {
    render(
      <HeadIconRow
        counts={{ reviewsPending: 0, urgentCorrespondence: 0, delegationsPending: 0, entityPending: 0, circuitsPending: 3 }}
      />,
    );

    expect(
      screen.getByRole('link', { name: 'إدارة دوائر التنفيذ — 3 ملفات محالة' }),
    ).toHaveAttribute('href', '/execution-circuits');
  });

  it('يستخدم المفرد التام عند العدّ 1 (لا «1 جهات» ولا «1 كتب»)', () => {
    render(
      <HeadIconRow
        counts={{ reviewsPending: 1, urgentCorrespondence: 0, delegationsPending: 1, entityPending: 0, circuitsPending: 0 }}
      />,
    );

    expect(
      screen.getByRole('link', { name: 'المطالعات — كتاب مطالعة واحد بانتظار الرد' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'طلبات الإنابة — طلب إنابة معلّق واحد' }),
    ).toBeInTheDocument();
  });

  it('رئيس الشعبة: بلا بطاقة تدقيق (النطاق الدائري مغلق) وتسمية الشعبة', () => {
    render(<HeadIconRow counts={zero} scopeLabel="مؤشرات الشعبة" hideAudit />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة رئيس الشعبة' });
    expect(nav.querySelectorAll('a')).toHaveLength(9);
    expect(screen.queryByRole('link', { name: 'سجل التدقيق' })).not.toBeInTheDocument();
    expect(screen.getByText('مؤشرات الشعبة')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
  });
});
