import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AlertRow } from './AlertRow';
import type { HeadAlertDto } from '../../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: React.ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

function pendingAlert(): HeadAlertDto {
  return {
    id: 5,
    message: 'أحال لك رئيس القسم ملفات من دائرة دمشق الأولى — يرجى تحديث معلوماتها',
    targetType: 'lawyer',
    documentId: 7,
    documentTitle: null,
    targetLawyerId: 3,
    targetLawyerName: undefined,
    isRead: false,
    recipientCount: undefined,
    unreadCount: undefined,
    createdAt: '2026-08-02T10:00:00Z',
    createdByName: 'رئيس القسم',
  };
}

describe('AlertRow', () => {
  it('تنبيه إعادة القيد يفتح صفحة المعلقات لا الملف المفرد', async () => {
    render(<AlertRow alert={pendingAlert()} />);

    // رغم وجود documentId، تنبيه (محامٍ × دائرة) المدمج يفتح صفحة معلقاته كلها.
    expect(
      screen.getByRole('link', { name: pendingAlert().message }),
    ).toHaveAttribute('href', '/pending-registrations');
  });

  it('عرض الصادر: عدّاد صفري يظهر «مقروء من الجميع» بدل «غير مقروء: 0»', () => {
    const sent = { ...pendingAlert(), isRead: undefined, recipientCount: 1, unreadCount: 0 };
    const { rerender } = render(<AlertRow alert={sent} sentView />);
    expect(screen.getByText('مقروء من الجميع')).toBeInTheDocument();
    expect(screen.queryByText(/غير مقروء/)).not.toBeInTheDocument();

    rerender(<AlertRow alert={{ ...sent, unreadCount: 1 }} sentView />);
    expect(screen.getByText(/غير مقروء/)).toBeInTheDocument();
    expect(screen.queryByText('مقروء من الجميع')).not.toBeInTheDocument();
  });

  it('العرض الافتراضي (مستلمات): العدّاد الصفري يبقى عدّادًا بلا شارة مقروء', () => {
    render(<AlertRow alert={{ ...pendingAlert(), recipientCount: 2, unreadCount: 0 }} />);
    expect(screen.getByText(/غير مقروء/)).toBeInTheDocument();
    expect(screen.queryByText('مقروء من الجميع')).not.toBeInTheDocument();
  });
});
