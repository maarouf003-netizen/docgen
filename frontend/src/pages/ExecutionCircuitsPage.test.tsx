import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ExecutionCircuitsPage from './ExecutionCircuitsPage';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (e: unknown) => {
    const err = e as { response?: { data?: { message?: string } } };
    return err?.response?.data?.message ?? 'حدث خطأ غير متوقع';
  },
}));

import { api } from '../api/client';

function circuit(id: number, name: string, fileCount = 0) {
  return { id, branchId: 1, name, isActive: true, fileCount, pendingCount: 0, version: 3 };
}

describe('ExecutionCircuitsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/execution-circuits/mine') {
        return Promise.resolve({ data: [circuit(1, 'دائرة أ', 2), circuit(2, 'دائرة ب', 0)] });
      }
      if (url === '/documents') {
        return Promise.resolve({ data: { items: [] } });
      }
      if (url === '/users/lawyers') {
        return Promise.resolve({ data: [] });
      }
      return Promise.reject(new Error('unknown'));
    });
  });

  it('يعرض الجدول مع زر الإدخال ويُدخل دائرة جديدة', async () => {
    const user = userEvent.setup();
    render(<ExecutionCircuitsPage />);

    expect(await screen.findByRole('heading', { name: 'إدارة دوائر التنفيذ' })).toBeInTheDocument();
    expect(await screen.findByText('دائرة أ')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '➕ إدخال دائرة' }));
    await user.type(screen.getByLabelText('اسم الدائرة'), 'دائرة جديدة');
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    await user.click(screen.getByRole('button', { name: 'إدخال' }));
    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/execution-circuits', { name: 'دائرة جديدة' }),
    );
  });

  it('يعرض «إفراغ ونقل» للدائرة المشغولة و«حذف» للفارغة (تصميميًا لا زر حذف مع ملفات)', async () => {
    render(<ExecutionCircuitsPage />);
    await screen.findByText('دائرة أ');
    expect(screen.getByRole('button', { name: 'إفراغ دائرة أ' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف دائرة أ' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'حذف دائرة ب' })).toBeInTheDocument();
  });
});
