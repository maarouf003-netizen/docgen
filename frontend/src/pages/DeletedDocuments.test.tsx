import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DeletedDocuments from './DeletedDocuments';
import type { DocumentResponse } from '../types';

const isMobileMock = vi.hoisted(() => vi.fn());
const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/client')>();
  return {
    api: { get: vi.fn(), post: vi.fn() },
    getApiErrorMessage: actual.getApiErrorMessage,
  };
});

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../hooks/useMediaQuery', () => ({
  useIsMobile: () => isMobileMock(),
}));

import { api } from '../api/client';

function doc(overrides: Partial<DocumentResponse>): DocumentResponse {
  return {
    id: 1,
    createdAt: '2026-07-31',
    updatedAt: '2026-07-31',
    documentType: 'متداول - مقترض',
    isDraft: false,
    amountNumeric: 0,
    amount2Numeric: 0,
    inclusionAmountNumeric: 0,
    viewCount: 0,
    printCount: 0,
    borrowerName: 'أحمد',
    borrowerFather: 'خالد',
    borrowerFamily: 'الخطيب',
    applicant: 'المدعي',
    court: 'دمشق',
    fileNumber: '99',
    fileType: 'حقوق',
    fileYear: '2026',
    deletedAt: '2026-08-04T10:00:00',
    guarantors: [],
    realEstates: [],
    executionActions: [],
    ...overrides,
  };
}

function mockPage(items: DocumentResponse[]) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: { page: 1, perPage: 20, totalCount: items.length, totalPages: 1, items },
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  isMobileMock.mockReturnValue(false);
  useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
});

describe('DeletedDocuments', () => {
  it('يعرض المستندات المحذوفة في جدول على المكتبي مع تاريخ الحذف', async () => {
    mockPage([doc({ id: 7, deletedAt: '2026-08-04T10:00:00' })]);

    render(<DeletedDocuments />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('تاريخ الحذف')).toBeInTheDocument();
    expect(within(table).getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(within(table).getByText('المدعي')).toBeInTheDocument();
    expect(within(table).getByText('دمشق')).toBeInTheDocument();
    expect(within(table).getByText('99 حقوق')).toBeInTheDocument();
  });

  it('يعرض بطاقات على الجوال مع زر استعادة', async () => {
    isMobileMock.mockReturnValue(true);
    mockPage([doc({ id: 7 })]);

    render(<DeletedDocuments />);

    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText(/المدعي · .* · دمشق/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'استعادة' })).toBeInTheDocument();
  });

  it('يرسل نص البحث إلى الخلفية مع الصفحة الأولى', async () => {
    const user = userEvent.setup();
    mockPage([]);

    render(<DeletedDocuments />);
    await screen.findByText('لا توجد مستندات محذوفة');

    await user.type(screen.getByPlaceholderText(/بحث بالاسم الثنائي/), 'أحمد');

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('q=' + encodeURIComponent('أحمد'));
    expect(url).toContain('page=1');
  });

  it('يستعيد المستند بعد التأكيد ويعرض رسالة النجاح ويعيد تحميل القائمة', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 7 })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });

    render(<DeletedDocuments />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'استعادة' }));
    await user.click(within(table).getByRole('button', { name: 'تأكيد الاستعادة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/7/restore');
    expect(await screen.findByText(/تمت استعادة المستند/)).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(expect.stringContaining('/documents/deleted'));
  });

  it('يلغي التأكيد دون إرسال طلب الاستعادة', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 7 })]);

    render(<DeletedDocuments />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'استعادة' }));
    await user.click(within(table).getByRole('button', { name: 'إلغاء' }));

    expect(api.post).not.toHaveBeenCalled();
    expect(within(table).getByRole('button', { name: 'استعادة' })).toBeInTheDocument();
  });

  it('يعرض رسالة خطأ عند فشل الاستعادة', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 7 })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500 },
    });

    render(<DeletedDocuments />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'استعادة' }));
    await user.click(within(table).getByRole('button', { name: 'تأكيد الاستعادة' }));

    expect(await screen.findByText('حدث خطأ في الخادم. حاول مرة أخرى لاحقاً')).toBeInTheDocument();
  });

  it('يعرض «لا توجد مستندات محذوفة» عند قائمة فارغة', async () => {
    mockPage([]);

    render(<DeletedDocuments />);

    expect(await screen.findByText('لا توجد مستندات محذوفة')).toBeInTheDocument();
  });

  it('يعرض «—» في تاريخ الحذف عند غياب القيمة', async () => {
    mockPage([doc({ deletedAt: undefined })]);

    render(<DeletedDocuments />);

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[0].textContent).toBe('—');
  });

  it('يخفي زر الاستعادة عن رئيس القسم والمشرف (عرض فقط)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'head' } });
    mockPage([doc({ id: 7 })]);

    render(<DeletedDocuments />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(within(table).queryByRole('button', { name: 'استعادة' })).not.toBeInTheDocument();
  });
});
