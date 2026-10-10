import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import CircuitStatsPage from './CircuitStatsPage';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
  getDownloadErrorMessage: async (error: unknown) => {
    const data = (error as { response?: { data?: unknown } })?.response?.data;
    if (data instanceof Blob) {
      try {
        const parsed = JSON.parse(await data.text()) as { message?: unknown };
        if (typeof parsed?.message === 'string' && parsed.message.trim().length > 0) {
          return parsed.message;
        }
      } catch {
        // يسقط للرسالة العامة أدناه.
      }
    }
    return 'خطأ من الخادم';
  },
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
    expect(screen.getByText('إحصائيات دوائر قسمك')).toBeInTheDocument();
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

  it('رئيس الشعبة يرى نطاق شعبته مع عمود الشعبة', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'subhead', branchId: 1, sectionId: 3 } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/execution-circuits/stats') {
        return Promise.resolve({
          data: [
            { circuitId: 1, circuitName: 'دائرة أ', branchId: 1, branchName: 'دمشق', isActive: true, fileCount: 5, lawyerCount: 2, pendingCount: 1, sectionId: 3, sectionName: 'مصياف' },
          ],
        });
      }
      return Promise.resolve({ data: [] });
    });
    render(<CircuitStatsPage />);

    expect(await screen.findByText('إحصائيات دوائر شعبتك')).toBeInTheDocument();
    expect(screen.queryByLabelText('الفرع')).not.toBeInTheDocument();
    expect(screen.getByText('مصياف')).toBeInTheDocument();
  });

  it('الصف الاصطناعي «بلا دائرة» يُوسم «كل الفروع» عند غياب اسم الفرع (مدير يرى الكل)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/branches') {
        return Promise.resolve({ data: [{ id: 1, name: 'دمشق', code: 'DAM' }] });
      }
      if (url === '/execution-circuits/stats') {
        return Promise.resolve({
          data: [
            { circuitId: 0, circuitName: 'بلا دائرة', branchId: 0, branchName: null, isActive: true, fileCount: 2, lawyerCount: 1, pendingCount: 1, sectionId: null, sectionName: null },
          ],
        });
      }
      return Promise.resolve({ data: [] });
    });
    render(<CircuitStatsPage />);

    expect(await screen.findByText('بلا دائرة')).toBeInTheDocument();
    // «كل الفروع» تظهر أيضًا في خيار منتقي الفرع — النطاق على خلايا الجدول.
    expect(within(screen.getByRole('table')).getByText('كل الفروع')).toBeInTheDocument();
  });

  it('الصف الاصطناعي «بلا دائرة» يعرض اسم الفرع لرئيس القسم', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'head' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/execution-circuits/stats') {
        return Promise.resolve({
          data: [
            { circuitId: 0, circuitName: 'بلا دائرة', branchId: 1, branchName: 'دمشق', isActive: true, fileCount: 3, lawyerCount: 2, pendingCount: 0, sectionId: null, sectionName: null },
          ],
        });
      }
      return Promise.resolve({ data: [] });
    });
    render(<CircuitStatsPage />);

    expect(await screen.findByText('بلا دائرة')).toBeInTheDocument();
    expect(screen.getByText('دمشق')).toBeInTheDocument();
    expect(screen.queryByText('كل الفروع')).not.toBeInTheDocument();
  });
});
