import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import UsersActivity from './UsersActivity';

vi.mock('../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

beforeEach(() => {
  vi.clearAllMocks();
});

describe('UsersActivity', () => {
  it('يعرض صفوف النشاط بقيمها داخل جدول قابل للتمرير أفقياً على الشاشات الضيقة', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [
        { username: 'lawyer1', fullName: 'أحمد محمد الخطيب', documentCount: 12, viewCount: 340 },
        { username: 'head1', fullName: 'رئيس القسم', documentCount: 3, viewCount: 98 },
      ],
    });

    const { container } = render(<UsersActivity />);

    expect(await screen.findByText('أحمد محمد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('lawyer1')).toBeInTheDocument();

    const wrapper = container.querySelector('.overflow-x-auto');
    expect(wrapper).not.toBeNull();
    const table = screen.getByRole('table');
    expect(table.className).toContain('min-w-[36rem]');
    expect(wrapper!.querySelectorAll('tbody tr').length).toBe(2);
    expect(screen.getAllByRole('columnheader')).toHaveLength(4);
  });

  it('يعرض حالة الفراغ عند عدم وجود بيانات', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });

    render(<UsersActivity />);

    expect(await screen.findByText('لا توجد بيانات')).toBeInTheDocument();
  });

  it('يعرض رسالة الخطأ عند فشل الجلب', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('تعذر الاتصال بالخادم'),
    );

    render(<UsersActivity />);

    expect(await screen.findByText('تعذر الاتصال بالخادم')).toBeInTheDocument();
  });
});
