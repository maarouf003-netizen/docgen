import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import AuditLogs from './AuditLogs';

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function mockLogs(items: unknown[]) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: { items, page: 1, perPage: 20, totalCount: items.length },
  });
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('AuditLogs', () => {
  it('يعرض تسميات عربية للأحداث الجديدة: نقل ملف وإنشاء/تعديل مستخدم', async () => {
    mockLogs([
      { id: 1, timestamp: '2026-08-01', userName: 'head1', actionType: 'transfer', details: 'نقل الملف', documentId: 5, documentType: 'بيان دعوى' },
      { id: 2, timestamp: '2026-08-01', userName: 'admin', actionType: 'create_user', details: 'أنشأ محامياً', documentId: null, documentType: null },
      { id: 3, timestamp: '2026-08-01', userName: 'admin', actionType: 'update_user', details: 'عدّل المستخدم', documentId: null, documentType: null },
    ]);
    render(<AuditLogs />);

    expect((await screen.findAllByText('نقل ملف')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('إنشاء مستخدم').length).toBeGreaterThan(0);
    expect(screen.getAllByText('تعديل مستخدم').length).toBeGreaterThan(0);
  });
});
