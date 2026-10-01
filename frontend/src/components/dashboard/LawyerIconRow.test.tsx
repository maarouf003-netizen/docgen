import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LawyerIconRow } from './LawyerIconRow';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: React.ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

describe('LawyerIconRow', () => {
  it('يعرض البطاقات الخمس بروابطها الصحيحة', () => {
    render(<LawyerIconRow counts={{ unseenReplies: 0, urgentCorrespondence: 0, calendarAlerts: 0 }} />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة المحامي' });
    expect(nav.querySelectorAll('a')).toHaveLength(5);
    expect(screen.getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(screen.getByRole('link', { name: 'المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(screen.getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(screen.getByRole('link', { name: 'التقويم' })).toHaveAttribute('href', '/calendar');
    expect(screen.getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute('href', '/account');
  });

  it('يُظهر الأجراس الحمراء بعدّاداتها في التسمية عند وجود تنبيه', () => {
    render(<LawyerIconRow counts={{ unseenReplies: 3, urgentCorrespondence: 0, calendarAlerts: 2 }} />);

    expect(screen.getByRole('link', { name: 'المطالعات — 3 ردود غير مقروءة' })).toBeInTheDocument();
    expect(screen.getByText('3 ردود غير مقروءة')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'التقويم — 2 تذكيرات اليوم أو متأخرة' })).toBeInTheDocument();
    // بلا جرس عند الصفر.
    expect(screen.getByRole('link', { name: 'المراسلات' })).toBeInTheDocument();
  });

  it('يستخدم المفرد التام عند العدّ 1 (لا «1 مراسلات» ولا «1 ردود»)', () => {
    render(<LawyerIconRow counts={{ unseenReplies: 1, urgentCorrespondence: 1, calendarAlerts: 1 }} />);

    expect(screen.getByRole('link', { name: 'المطالعات — رد واحد غير مقروء' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'المراسلات — مراسلة عاجلة واحدة' })).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'التقويم — تذكير واحد اليوم أو متأخر' }),
    ).toBeInTheDocument();
  });
});
