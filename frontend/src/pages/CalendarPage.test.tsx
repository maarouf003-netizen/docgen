import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import CalendarPage from './CalendarPage';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function mockApi() {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/reminders')
      return Promise.resolve({
        data: [
          {
            actionId: 1,
            documentId: 5,
            borrowerName: 'سامر',
            borrowerFather: 'محمد',
            borrowerFamily: 'حسن',
            actionText: 'مراجعة دائرة التنفيذ',
            dueDate: '2026-08-08',
            dueDateSuspect: false,
          },
        ],
      });
    if (url === '/appeals/reminders') return Promise.resolve({ data: [] });
    if (url === '/personal-reminders')
      return Promise.resolve({
        data: [
          {
            id: 7,
            title: 'تذكير شخصي',
            notes: null,
            dueDate: '2026-08-08',
            color: 'زمردي',
            recurrence: 'يومي',
            recurrenceEnd: null,
            isArchived: false,
            completedOccurrenceKeys: [],
            createdAt: '2026-08-01',
          },
        ],
      });
    return Promise.resolve({ data: {} });
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  useAuthMock.mockReturnValue({
    user: { id: 1, username: 'lawyer1', fullName: 'محامي', role: 'lawyer', branchId: 1 },
  });
});

describe('CalendarPage', () => {
  it('تعرض التقويم وكل التذكيرات (ملف + شخصي) مع الوقوع التالي', async () => {
    mockApi();
    render(<CalendarPage />);

    expect(await screen.findByRole('heading', { name: 'التقويم' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'تذكيرات الملفات والاستئنافات' })).toBeInTheDocument();
    expect(screen.getByText('سامر محمد حسن')).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'التذكيرات الشخصية' })).toBeInTheDocument();
    expect(screen.getByText('تذكير شخصي')).toBeInTheDocument();
    expect(screen.getByText(/الوقوع التالي:/)).toBeInTheDocument();
  });

  it('يحذف التذكير الشخصي بتأكيد صريح ويحدّث القائمة', async () => {
    const user = userEvent.setup();
    mockApi();
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    render(<CalendarPage />);

    await user.click(await screen.findByRole('button', { name: 'حذف' }));
    expect(screen.getByRole('alert')).toHaveTextContent('لا يمكن التراجع');
    await user.click(screen.getByRole('button', { name: 'تأكيد الحذف' }));

    expect(api.delete).toHaveBeenCalledWith('/personal-reminders/7');
  });

  it('يعلّم الوقوع التالي منجزًا', async () => {
    const user = userEvent.setup();
    mockApi();
    (api.patch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(<CalendarPage />);

    await user.click(await screen.findByRole('button', { name: 'تم' }));
    expect(api.patch).toHaveBeenCalledWith(
      '/personal-reminders/7/occurrences',
      expect.objectContaining({ occurrenceDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/), done: true }),
    );
  });

  it('يفتح نافذة اليوم عند نقر يوم في التقويم', async () => {
    const user = userEvent.setup();
    mockApi();
    const { container } = render(<CalendarPage />);

    await screen.findByRole('heading', { name: 'التقويم' });
    const marked = container.querySelector('.has-reminders, .has-urgent') as HTMLElement;
    expect(marked).toBeInTheDocument();
    await user.click(marked);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
