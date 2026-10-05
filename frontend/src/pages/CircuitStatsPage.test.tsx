import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import CircuitStatsPage from './CircuitStatsPage';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

describe('CircuitStatsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/execution-circuits/stats') {
        return Promise.resolve({
          data: [
            { circuitId: 1, circuitName: 'دائرة أ', branchId: 1, branchName: 'دمشق', isActive: true, fileCount: 5, lawyerCount: 2, pendingCount: 1 },
          ],
        });
      }
      return Promise.resolve({ data: [] });
    });
  });

  it('يُخفي منتقي الفرع عن رئيس القسم (اختياره مُتجاهَل خادميًا)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'head' } });
    render(<CircuitStatsPage />);

    expect(await screen.findByText('دائرة أ')).toBeInTheDocument();
    expect(screen.queryByLabelText('الفرع')).not.toBeInTheDocument();
    expect(screen.getByText('إحصائيات دوائر فرعك')).toBeInTheDocument();
  });

  it('يعرض المنتقي للمدير ويُرسل الفرع المختار', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/branches') {
        return Promise.resolve({ data: [{ id: 1, name: 'دمشق', code: 'DAM' }] });
      }
      if (url === '/execution-circuits/stats') {
        return Promise.resolve({ data: [] });
      }
      return Promise.resolve({ data: [] });
    });
    render(<CircuitStatsPage />);

    expect(await screen.findByLabelText('الفرع')).toBeInTheDocument();
  });
});
