import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
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
});
