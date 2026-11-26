import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CircuitStatsPage from './CircuitStatsPage';
import { renderWithQueryClient } from '../test/queryClient';

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
    renderWithQueryClient(<CircuitStatsPage />);

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
    renderWithQueryClient(<CircuitStatsPage />);

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
    renderWithQueryClient(<CircuitStatsPage />);

    expect(await screen.findByText('إحصائيات دوائر شعبتك')).toBeInTheDocument();
    expect(screen.queryByLabelText('الفرع')).not.toBeInTheDocument();
    expect(await screen.findByText('مصياف')).toBeInTheDocument();
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
    renderWithQueryClient(<CircuitStatsPage />);

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
    renderWithQueryClient(<CircuitStatsPage />);

    expect(await screen.findByText('بلا دائرة')).toBeInTheDocument();
    expect(screen.getByText('دمشق')).toBeInTheDocument();
    expect(screen.queryByText('كل الفروع')).not.toBeInTheDocument();
  });

  it('يعيد استخدام مخزَّن الاستعلام عند العودة إلى الفرع نفسه (بلا طلب مكرر)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'manager' } });
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    get.mockImplementation((url: string, config?: { params?: { branchId?: number } }) => {
      if (url === '/branches') {
        return Promise.resolve({ data: [{ id: 1, name: 'دمشق', code: 'DAM' }] });
      }
      if (url === '/execution-circuits/stats') {
        const branchId = config?.params?.branchId;
        return Promise.resolve({
          data: branchId
            ? [{ circuitId: 1, circuitName: 'دائرة أ', branchId, branchName: 'دمشق', isActive: true, fileCount: 5, lawyerCount: 2, pendingCount: 1 }]
            : [{ circuitId: 2, circuitName: 'دائرة الكل', branchId: 0, branchName: null, isActive: true, fileCount: 9, lawyerCount: 3, pendingCount: 0 }],
        });
      }
      return Promise.resolve({ data: [] });
    });

    const statsCalls = (branchId?: number) =>
      get.mock.calls.filter(
        ([url, config]) =>
          url === '/execution-circuits/stats' &&
          (config as { params?: { branchId?: number } } | undefined)?.params?.branchId === branchId,
      ).length;

    const user = userEvent.setup();
    renderWithQueryClient(<CircuitStatsPage />);

    expect(await screen.findByText('دائرة الكل')).toBeInTheDocument();
    expect(statsCalls(undefined)).toBe(1);

    await user.selectOptions(screen.getByLabelText('الفرع'), '1');
    expect(await screen.findByText('دائرة أ')).toBeInTheDocument();
    expect(statsCalls(1)).toBe(1);

    // العودة إلى «كل الفروع»: النتيجة مخزَّنة (staleTime 30s) فلا طلب ثانٍ.
    await user.selectOptions(screen.getByLabelText('الفرع'), '');
    expect(await screen.findByText('دائرة الكل')).toBeInTheDocument();
    await waitFor(() => expect(statsCalls(undefined)).toBe(1));
    expect(statsCalls(1)).toBe(1);
  });
});
