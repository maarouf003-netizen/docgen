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

const zero = { unseenReplies: 0, urgentCorrespondence: 0, calendarAlerts: 0, pendingRegistrations: 0 };

describe('LawyerIconRow', () => {
  it('يعرض البطاقات الست بروابطها الصحيحة', () => {
    render(<LawyerIconRow counts={zero} showReferredFiles />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة المحامي' });
    expect(nav.querySelectorAll('a')).toHaveLength(6);
    expect(screen.getByRole('link', { name: 'الإحصائيات' })).toHaveAttribute('href', '/stats');
    expect(screen.getByRole('link', { name: 'المطالعات' })).toHaveAttribute('href', '/reviews');
    expect(screen.getByRole('link', { name: 'المراسلات' })).toHaveAttribute('href', '/correspondence');
    expect(screen.getByRole('link', { name: 'التقويم' })).toHaveAttribute('href', '/calendar');
    expect(screen.getByRole('link', { name: 'الحساب الشخصي' })).toHaveAttribute('href', '/account');
    expect(screen.getByRole('link', { name: 'ملفات معلقة' })).toHaveAttribute('href', '/pending-registrations');
  });

  it('يُظهر الأجراس الحمراء بعدّاداتها في التسمية عند وجود تنبيه', () => {
    render(
      <LawyerIconRow
        counts={{ unseenReplies: 3, urgentCorrespondence: 0, calendarAlerts: 2, pendingRegistrations: 0 }}
        showReferredFiles
      />,
    );

    expect(screen.getByRole('link', { name: 'المطالعات — 3 ردود غير مقروءة' })).toBeInTheDocument();
    expect(screen.getByText('3 ردود غير مقروءة')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'التقويم — 2 تذكيرات اليوم أو متأخرة' })).toBeInTheDocument();
    // بلا جرس عند الصفر.
    expect(screen.getByRole('link', { name: 'المراسلات' })).toBeInTheDocument();
  });

  it('يُظهر شارة الملفات المحالة بعدّادها عند وجود معلقات', () => {
    render(
      <LawyerIconRow
        counts={{ unseenReplies: 0, urgentCorrespondence: 0, calendarAlerts: 0, pendingRegistrations: 2 }}
        showReferredFiles
      />,
    );

    expect(
      screen.getByRole('link', { name: 'ملفات معلقة — 2 ملفات بانتظار تحديث بياناتها' }),
    ).toBeInTheDocument();
  });

  it('يستخدم المفرد التام عند العدّ 1 (لا «1 مراسلات» ولا «1 ردود»)', () => {
    render(
      <LawyerIconRow
        counts={{ unseenReplies: 1, urgentCorrespondence: 1, calendarAlerts: 1, pendingRegistrations: 1 }}
        showReferredFiles
      />,
    );

    expect(screen.getByRole('link', { name: 'المطالعات — رد واحد غير مقروء' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'المراسلات — مراسلة عاجلة واحدة' })).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'التقويم — تذكير واحد اليوم أو متأخر' }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'ملفات معلقة — ملف واحد بانتظار تحديث بياناته' }),
    ).toBeInTheDocument();
  });

  it('يحجب بطاقة الملفات المحالة بعد نجاح الجلب والصفر المؤكد', () => {
    render(<LawyerIconRow counts={zero} showReferredFiles={false} />);

    const nav = screen.getByRole('navigation', { name: 'أقسام لوحة المحامي' });
    expect(nav.querySelectorAll('a')).toHaveLength(5);
    expect(screen.queryByRole('link', { name: 'ملفات معلقة' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'لا معلقات' })).not.toBeInTheDocument();
  });

  it('يُظهر البطاقة بسطر تحميل أثناء الجلب (لا وميض ولا «لا معلقات» كاذبة)', () => {
    render(<LawyerIconRow counts={zero} showReferredFiles pendingState="loading" />);

    expect(screen.getByRole('link', { name: 'ملفات معلقة' })).toHaveAttribute(
      'href',
      '/pending-registrations',
    );
    expect(screen.getByText('جارِ التحميل…')).toBeInTheDocument();
  });

  it('يُبقي البطاقة عند فشل الجلب مع سطر صادق (fail-open)', () => {
    render(<LawyerIconRow counts={zero} showReferredFiles pendingState="error" />);

    expect(screen.getByRole('link', { name: 'ملفات معلقة' })).toBeInTheDocument();
    expect(screen.getByText('تعذّر الجلب — افتح للتحقق')).toBeInTheDocument();
  });
});
