import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import StruckOffDocuments from './StruckOffDocuments';
import type { DocumentResponse } from '../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

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
    generalEntitySide: 'executed',
    executedStatus: 'مشطوب',
    struckOffDate: '2026-08-04T10:00:00',
    guarantors: [],
    realEstates: [],
    executionActions: [],
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [{ name: 'محمود', father: 'علي', family: 'حسن' }],
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

describe('StruckOffDocuments', () => {
  it('يعرض الملفات المشطوبة في جدول على المكتبي مع تاريخ الشطب واسم المنفذ عليه', async () => {
    mockPage([doc({ id: 7, struckOffDate: '2026-08-04T10:00:00' })]);

    render(<StruckOffDocuments />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('تاريخ الشطب')).toBeInTheDocument();
    expect(within(table).getByText('محمود علي حسن')).toBeInTheDocument();
    expect(within(table).getByText('المدعي')).toBeInTheDocument();
    expect(within(table).getByText('دمشق')).toBeInTheDocument();
    expect(within(table).getByText('99 حقوق')).toBeInTheDocument();
    expect(within(table).getByText('إعادة الملف')).toBeInTheDocument();
  });

  it('يعرض بطاقات على الجوال مع شارة «مشطوب» وزر إعادة الملف', async () => {
    isMobileMock.mockReturnValue(true);
    mockPage([doc({ id: 7 })]);

    render(<StruckOffDocuments />);

    expect(await screen.findByText('محمود علي حسن')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText(/المدعي · .* · دمشق/)).toBeInTheDocument();
    expect(screen.getByText('مشطوب')).toBeInTheDocument();
    expect(screen.getByText(/شُطب في/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة الملف' })).toBeInTheDocument();
  });

  it('يعرض اسم الجهة العامة المنفذ عليها عند غياب الشخص الطبيعي', async () => {
    mockPage([doc({ executedNaturalPersons: [], executedPublicEntities: [{ entityName: 'المصرف العقاري' }] })]);

    render(<StruckOffDocuments />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('المصرف العقاري')).toBeInTheDocument();
  });

  it('يعرض اسم طالب التنفيذ كبديل عند غياب المنفذ عليهم', async () => {
    mockPage([doc({ executedNaturalPersons: [], executedPublicEntities: [] })]);

    render(<StruckOffDocuments />);

    await screen.findByRole('table');
    expect(screen.getByRole('link', { name: 'المدعي' })).toHaveAttribute('href', '/documents/1');
  });

  it('يرسل نص البحث إلى الخلفية مع الصفحة الأولى', async () => {
    const user = userEvent.setup();
    mockPage([]);

    render(<StruckOffDocuments />);
    await screen.findByText('لا توجد ملفات مشطوبة');

    await user.type(screen.getByPlaceholderText(/بحث في الملفات المشطوبة/), 'محمود');

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('q=' + encodeURIComponent('محمود'));
    expect(url).toContain('page=1');
  });

  it('يعيد الملف إلى المتداول بعد التأكيد ويعرض رسالة النجاح ويعيد تحميل القائمة', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 7 })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });

    render(<StruckOffDocuments />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'إعادة الملف' }));
    await user.click(within(table).getByRole('button', { name: 'تأكيد الإعادة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/7/restore-struck-off');
    expect(await screen.findByText(/أعيد الملف "محمود علي حسن" إلى المتداول/)).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(expect.stringContaining('/documents/struck-off'));
  });

  it('يلغي التأكيد دون إرسال طلب الإعادة', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 7 })]);

    render(<StruckOffDocuments />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'إعادة الملف' }));
    await user.click(within(table).getByRole('button', { name: 'إلغاء' }));

    expect(api.post).not.toHaveBeenCalled();
    expect(within(table).getByRole('button', { name: 'إعادة الملف' })).toBeInTheDocument();
  });

  it('يعرض رسالة خطأ عند فشل الإعادة', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 7 })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500 },
    });

    render(<StruckOffDocuments />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'إعادة الملف' }));
    await user.click(within(table).getByRole('button', { name: 'تأكيد الإعادة' }));

    expect(await screen.findByText('حدث خطأ في الخادم. حاول مرة أخرى لاحقاً')).toBeInTheDocument();
  });

  it('يعرض «لا توجد ملفات مشطوبة» عند قائمة فارغة', async () => {
    mockPage([]);

    render(<StruckOffDocuments />);

    expect(await screen.findByText('لا توجد ملفات مشطوبة')).toBeInTheDocument();
  });

  it('يعرض «—» في تاريخ الشطب عند غياب القيمة', async () => {
    mockPage([doc({ struckOffDate: undefined })]);

    render(<StruckOffDocuments />);

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[0].textContent).toBe('—');
  });

  it('يخفي زر الإعادة عن رئيس القسم والمشرف (عرض فقط)', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'head' } });
    mockPage([doc({ id: 7 })]);

    render(<StruckOffDocuments />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('محمود علي حسن')).toBeInTheDocument();
    expect(within(table).queryByRole('button', { name: 'إعادة الملف' })).not.toBeInTheDocument();
  });
});
