import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

  it('يكتب عدة أحرف متتابعة فيطلق طلبًا واحدًا فقط بعد انتهاء التأجيل', async () => {
    const user = userEvent.setup();
    mockLogs([]);
    render(<AuditLogs />);

    expect(api.get).toHaveBeenCalledTimes(1);

    await user.type(screen.getByLabelText('اسم المستخدم'), 'abc');
    expect(api.get).toHaveBeenCalledTimes(1);

    await act(() => new Promise((resolve) => setTimeout(resolve, 400)));

    expect(api.get).toHaveBeenCalledTimes(2);
    const lastUrl = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.at(-1)?.[0] as string | undefined;
    expect(lastUrl).toContain('page=1');
    expect(lastUrl).toContain('userName=abc');
  }, 8000);

  it('يعيد الصفحة إلى 1 عند تغيير نوع الحدث بعد التنقل بين الصفحات', async () => {
    const user = userEvent.setup();
    mockLogs(Array.from({ length: 25 }, (_, i) => ({
      id: i + 1,
      timestamp: '2026-08-01',
      userName: 'head1',
      actionType: 'login',
      details: null,
      documentId: null,
      documentType: null,
    })));
    render(<AuditLogs />);

    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(1));

    await user.click(screen.getByRole('button', { name: 'التالي' }));
    await waitFor(() => {
      const url = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.at(-1)?.[0] as string | undefined;
      expect(url).toBeDefined();
      expect(url).toContain('page=2');
    });

    await user.selectOptions(screen.getByLabelText('نوع الحدث'), 'create');
    await waitFor(() => {
      const url = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.at(-1)?.[0] as string | undefined;
      expect(url).toBeDefined();
      expect(url).toContain('page=1');
      expect(url).toContain('actionType=create');
    });
  });
});
