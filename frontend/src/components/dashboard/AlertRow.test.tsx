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
  it('تنبيه إعادة القيد يفتح صفحة المعلقات لا الملف المفرد', () => {
    render(<AlertRow alert={pendingAlert()} />);

    // رغم وجود documentId، تنبيه (محامٍ × دائرة) المدمج يفتح صفحة معلقاته كلها.
    expect(
      screen.getByRole('link', { name: pendingAlert().message }),
    ).toHaveAttribute('href', '/pending-registrations');
  });
});
