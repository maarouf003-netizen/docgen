import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import PortalStats from './PortalStats';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../api/client';

const statsPayload = {
  totalFiles: 5,
  draftFiles: 1,
  circulatingFiles: 2,
  executedFiles: 1,
  deferredFiles: 1,
  referredToStartFiles: 1,
  pendingAppeals: 2,
  closedAppeals: 3,
  monthly: Array.from({ length: 12 }, (_, i) => ({
    year: 2026,
    month: ((7 + i) % 12) + 1,
    files: i === 11 ? 3 : i % 4,
  })),
  perEntry: [
    { entryId: 11, governorate: 'اللاذقية', branchName: 'فرع 1', files: 4 },
    { entryId: 12, governorate: 'دمشق', branchName: 'الفرع الرئيسي', files: 1 },
  ],
  topCurrencies: [
    { currency: 'ليرة سورية', files: 3, totalAmount: 4500 },
  ],
  amountTotals: [
    { currency: 'ليرة سورية', files: 3, totalAmount: 4500 },
    { currency: 'دولار أمريكي', files: 1, totalAmount: 90 },
  ],
  amountByStatus: [
    { status: 'متداول', files: 2, totals: [{ currency: 'ليرة سورية', files: 2, totalAmount: 3000 }] },
    { status: 'تريث', files: 1, totals: [{ currency: 'ليرة سورية', files: 1, totalAmount: 1500 }] },
    { status: 'منفذ', files: 1, totals: [{ currency: 'دولار أمريكي', files: 1, totalAmount: 90 }] },
    { status: 'محال الى البداية', files: 1, totals: [] },
    { status: 'تحت رفع', files: 1, totals: [] },
  ],
};

function mockScope(scopeType: string, entries: unknown[]) {
  return {
    scopeType,
    groupId: 5,
    canonicalName: 'المصرف التجاري السوري',
    entityType: 'company',
    entries,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('PortalStats', () => {
  it('يعرض الإجمالي افتراضيًا مع منتقي الفرع لمندوب الهوية ويفصّل المبالغ بلا أعلى العملات', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/my-scope') {
        return Promise.resolve({ data: mockScope('group', [
          { id: 11, governorate: 'اللاذقية', branchName: 'فرع 1', isActive: true },
          { id: 12, governorate: 'دمشق', branchName: 'الفرع الرئيسي', isActive: true },
        ]) });
      }
      if (url === '/portal/stats') return Promise.resolve({ data: statsPayload });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    render(<MemoryRouter><PortalStats /></MemoryRouter>);

    const section = await screen.findByRole('region', { name: 'إحصاءات نطاق جهتك' });
    expect(screen.getByText('الإجمالي').nextElementSibling).toHaveTextContent('5');
    const circulating = within(section).getAllByText(/^متداول$/);
    expect(circulating.length).toBeGreaterThanOrEqual(1);
    expect(circulating[0].nextElementSibling).toHaveTextContent('2');

    // منتقي الفرع ظاهر لمندوب الهوية.
    expect(screen.getByLabelText('اختيار الفرع لعرض إحصائياته')).toBeInTheDocument();

    // تفصيل المبالغ حاضر، وأعلى العملات غائب نهائيًا.
    expect(screen.getByText('مجموع المبالغ (الإجمالي حسب العملة)')).toBeInTheDocument();
    expect(screen.getByText('المبالغ لكل نوع من الملفات')).toBeInTheDocument();
    expect(screen.queryByText('أعلى العملات')).not.toBeInTheDocument();
    expect(screen.getAllByText('ليرة سورية').length).toBeGreaterThanOrEqual(1);
    // حاشيتا التوضيح حُذفتا من العرض نهائيًا.
    expect(screen.queryByText(/ملفات الإنابة.*تُحتسب في الأعداد دون المبالغ/)).not.toBeInTheDocument();
    expect(screen.queryByText(/يُحتسب تحت كل قيد ارتبط به/)).not.toBeInTheDocument();
  });

  it('يطلب إحصائيات الفرع المختار عبر entryId', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/my-scope') {
        return Promise.resolve({ data: mockScope('group', [
          { id: 11, governorate: 'اللاذقية', branchName: 'فرع 1', isActive: true },
          { id: 12, governorate: 'دمشق', branchName: 'الفرع الرئيسي', isActive: true },
        ]) });
      }
      if (url === '/portal/stats') return Promise.resolve({ data: statsPayload });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    const user = userEvent.setup();
    render(<MemoryRouter><PortalStats /></MemoryRouter>);

    const select = await screen.findByLabelText('اختيار الفرع لعرض إحصائياته') as HTMLSelectElement;
    await user.selectOptions(select, '11');

    const calls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (c: unknown[]) => c[0] === '/portal/stats',
    );
    const last = calls[calls.length - 1][1] as { params?: Record<string, unknown> };
    expect(last.params?.entryId).toBe(11);
  });

  it('يخفي منتقي الفرع لمندوب القيد ويعرض نطاق قيده فقط', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/my-scope') {
        return Promise.resolve({ data: mockScope('entry', [
          { id: 11, governorate: 'اللاذقية', branchName: 'فرع 1', isActive: true },
        ]) });
      }
      if (url === '/portal/stats') return Promise.resolve({ data: statsPayload });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    render(<MemoryRouter><PortalStats /></MemoryRouter>);

    await screen.findByRole('region', { name: 'إحصاءات نطاق جهتك' });
    expect(screen.queryByLabelText('اختيار الفرع لعرض إحصائياته')).not.toBeInTheDocument();
  });

  it('يعرض ستة قيود فقط ويصرّح بالباقي بعد الحدّ بدل قطعه بصمت', async () => {
    // L3: القصّ الصامت عند القيود الستة كان يُخفي قيدًا بلا أي مؤشر.
    const perEntry = Array.from({ length: 8 }, (_, i) => ({
      entryId: 20 + i,
      governorate: `محافظة ${i + 1}`,
      branchName: `فرع ${i + 1}`,
      files: 10 - i,
    }));
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/portal/my-scope') {
        return Promise.resolve({ data: mockScope('group', perEntry.map((e) => ({ id: e.entryId, governorate: e.governorate, branchName: e.branchName, isActive: true }))) });
      }
      if (url === '/portal/stats') return Promise.resolve({ data: { ...statsPayload, perEntry } });
      return Promise.reject(new Error(`unexpected GET ${url}`));
    });
    render(<MemoryRouter><PortalStats /></MemoryRouter>);

    const list = await screen.findByRole('list', { name: /القيود/ });
    expect(within(list).getAllByRole('listitem')).toHaveLength(6);
    expect(screen.getByText(/\+2 قيدًا/)).toBeInTheDocument();
  });
});
