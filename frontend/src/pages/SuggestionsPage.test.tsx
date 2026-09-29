import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SuggestionsPage from './SuggestionsPage';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), patch: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function mockList() {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation(
    (_url: string, config?: { params?: Record<string, unknown> }) => {
      if (config?.params?.page === 2) {
        return Promise.resolve({
          data: {
            items: [{ id: 3, message: 'ثالث', createdAt: '2026-08-06T10:00:00', isRead: false, senderName: 'م' }],
            page: 2,
            perPage: 20,
            totalCount: 3,
            totalPages: 2,
          },
        });
      }
      return Promise.resolve({
        data: {
          items: [
            { id: 1, message: 'أول اقتراح', createdAt: '2026-08-08T10:00:00', isRead: false, senderName: 'محامي دمشق' },
            { id: 2, message: 'ثانٍ مقروء', createdAt: '2026-08-07T10:00:00', isRead: true, senderName: 'محامي حلب' },
          ],
          page: 1,
          perPage: 20,
          totalCount: 3,
          totalPages: 2,
        },
      });
    },
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('SuggestionsPage', () => {
  it('يعرض الاقتراحات مع المرسل وعدّاد غير المقروء', async () => {
    mockList();
    render(<SuggestionsPage />);

    expect(await screen.findByText('أول اقتراح')).toBeInTheDocument();
    expect(screen.getByText('محامي دمشق')).toBeInTheDocument();
    expect(screen.getByText('ثانٍ مقروء')).toBeInTheDocument();
    expect(screen.getByText('1 غير مقروء')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'تعليم كمقروء' })).toBeInTheDocument();
  });

  it('يعلّم المقروء ويحدّث الشارة', async () => {
    const user = userEvent.setup();
    mockList();
    (api.patch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    render(<SuggestionsPage />);

    await user.click(await screen.findByRole('button', { name: 'تعليم كمقروء' }));

    expect(api.patch).toHaveBeenCalledWith('/app-suggestions/1/read');
    expect(screen.queryByText('1 غير مقروء')).not.toBeInTheDocument();
    expect(screen.getAllByText('مقروء').length).toBeGreaterThanOrEqual(1);
  });

  it('يعرض الفراغ عند غياب الاقتراحات', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], page: 1, perPage: 20, totalCount: 0, totalPages: 0 },
    });
    render(<SuggestionsPage />);

    expect(await screen.findByText('لا اقتراحات بعد.')).toBeInTheDocument();
  });

  it('يتنقل بين الصفحات ويطلب الصفحة التالية', async () => {
    const user = userEvent.setup();
    mockList();
    render(<SuggestionsPage />);

    expect(await screen.findByText('صفحة 1 من 2')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'التالي' }));

    expect(await screen.findByText('صفحة 2 من 2')).toBeInTheDocument();
    expect(screen.getByText('ثالث')).toBeInTheDocument();
    expect(api.get).toHaveBeenLastCalledWith(
      '/app-suggestions',
      expect.objectContaining({ params: expect.objectContaining({ page: 2 }) }),
    );
  });
});
