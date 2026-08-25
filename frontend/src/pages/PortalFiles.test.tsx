import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import PortalFiles from './PortalFiles';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../api/client';

function fileItem(overrides: Record<string, unknown> = {}) {
  return {
    id: 1,
    documentType: 'متداول - شركة المباني',
    isDraft: false,
    borrowerName: 'شركة المباني',
    applicant: 'وزارة التعليم - محافظة دمشق',
    executedEntitiesSummary: '',
    amountNumeric: 1500,
    currency: 'ليرة سورية',
    execStatus: null,
    createdAt: '2026-08-01',
    updatedAt: '2026-08-20',
    ...overrides,
  };
}

const statsPayload = {
  totalFiles: 5,
  draftFiles: 1,
  circulatingFiles: 2,
  executedFiles: 1,
  deferredFiles: 1,
  pendingAppeals: 2,
  closedAppeals: 3,
  monthly: Array.from({ length: 12 }, (_, i) => ({
    year: 2026,
    month: ((7 + i) % 12) + 1, // شباط…كانون الثاني تقريبًا — الأهم أن 12 دلوًا
    files: i === 11 ? 3 : i % 4,
  })),
  perEntry: [
    { entryId: 11, governorate: 'دمشق', branchName: 'الفرع الرئيسي', files: 4 },
    { entryId: 12, governorate: 'حلب', branchName: 'فرع حلب', files: 1 },
  ],
  topCurrencies: [
    { currency: 'ليرة سورية', files: 3, totalAmount: 4500 },
    { currency: 'دولار أمريكي', files: 1, totalAmount: 90 },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/portal/my-scope') {
      return Promise.resolve({
        data: {
          scopeType: 'group',
          groupId: 5,
          canonicalName: 'وزارة التعليم',
          entityType: 'ministry',
          entries: [{ id: 11, governorate: 'دمشق', branchName: 'الفرع الرئيسي', isActive: true }],
        },
      });
    }
    if (url === '/portal/files') {
      return Promise.resolve({
        data: {
          items: [fileItem(), fileItem({ id: 2, borrowerName: 'مصنع النور', applicant: null, isDraft: true })],
          page: 1,
          perPage: 20,
          totalCount: 2,
          totalPages: 1,
        },
      });
    }
    if (url === '/portal/stats') {
      return Promise.resolve({ data: statsPayload });
    }
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
});

describe('PortalFiles', () => {
  it('يعرض نطاق الجهة وقائمة الملفات القرائية', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    expect(await screen.findByText(/نطاقك:/)).toHaveTextContent('وزارة التعليم');
    expect(await screen.findByText('شركة المباني')).toBeInTheDocument();
    expect(screen.getByText('مصنع النور')).toBeInTheDocument();
    // «تحت رفع» تظهر كشارة للملف المسودة وكخيار في فلتر الحالة.
    expect(screen.getAllByText('تحت رفع').length).toBeGreaterThanOrEqual(1);
  });

  it('لا يعرض أي زر تعديل — البوابة قرائية (د10)', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await screen.findByText('شركة المباني');
    expect(screen.queryByRole('button', { name: /تعديل/ })).not.toBeInTheDocument();
    expect(screen.getByText(/بوابة اطلاع قرائية/)).toBeInTheDocument();
  });

  it('يصدّر إكسل بنفس الفلاتر عبر تنزيل ملف', async () => {
    const blobUrlSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    const user = userEvent.setup();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await user.click(await screen.findByRole('button', { name: 'تصدير إكسل' }));

    await new Promise((r) => setTimeout(r, 0));
    expect(api.get).toHaveBeenCalledWith('/portal/export',
      expect.objectContaining({ params: { q: undefined, status: undefined }, responseType: 'blob' }));
    blobUrlSpy.mockRestore();
    revokeSpy.mockRestore();
  });

  it('يفلتر بالحالة ويحدّث الاستعلام', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    await screen.findByText('شركة المباني');
    await user.selectOptions(screen.getByLabelText('فلتر الحالة'), 'منفذ');

    // الاستعلام يُعاد بفلتر الحالة (آخر نداء).
    const calls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (c: unknown[]) => c[0] === '/portal/files',
    );
    const last = calls[calls.length - 1][1];
    expect(last.params.status).toBe('منفذ');
  });

  it('يعرض بطاقة الإحصاءات: عدادات الحالة والاستئنافات وسلسلة 12 شهرًا (المرحلة 4)', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    const section = await screen.findByRole('region', { name: 'إحصاءات نطاق جهتك' });

    // عدادات الحالة الخمسة بقيم الـmock (محدودة ببطاقة الإحصاءات).
    expect(screen.getByText('الإجمالي').nextElementSibling).toHaveTextContent('5');
    expect(within(section).getByText(/^متداول$/).nextElementSibling).toHaveTextContent('2');
    expect(within(section).getByText(/^منفذ$/).nextElementSibling).toHaveTextContent('1');
    expect(within(section).getByText(/^تحت رفع$/).nextElementSibling).toHaveTextContent('1');
    expect(screen.getByText(/الاستئنافات:/)).toHaveTextContent('2 معلّقًا · 3 مغلقًا');

    // 12 دلوًا شهريًا + توزيع القيود بأعمدته.
    expect(screen.getByRole('img', { name: /ملفات آخر 12 شهرًا/ })).toBeInTheDocument();
    expect(screen.getByText('توزيع الارتباط على القيود')).toBeInTheDocument();
    expect(screen.getByText('دمشق/الفرع الرئيسي')).toBeInTheDocument();
    expect(screen.getByText('حلب/فرع حلب')).toBeInTheDocument();
  });

  it('يعرض أعلى العملات بمجاميعها داخل العملة نفسها دون خلط', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    const liraRow = await screen.findByText('ليرة سورية');
    expect(liraRow.parentElement).toHaveTextContent('3 ملفًا · ٤٬٥٠٠');
    const dollarRow = screen.getByText('دولار أمريكي');
    expect(dollarRow.parentElement).toHaveTextContent('1 ملفًا · ٩٠');
  });

  it('يظهر تنبيه الاحتساب المتعدد فقط لنطاق الهوية الأم', async () => {
    render(<MemoryRouter><PortalFiles /></MemoryRouter>);

    expect(await screen.findByText(/يُحتسب تحت كل قيد ارتبط به/)).toBeInTheDocument();
  });
});
