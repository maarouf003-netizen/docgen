import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import userEvent from '@testing-library/user-event';
import StrikeOffRemindersPage from './StrikeOffRemindersPage';
import { formatDate } from '../utils/dates';
import type { DocumentResponse } from '../types';
import { makeDocument } from '../test/factories';

const isMobileMock = vi.hoisted(() => vi.fn());

vi.mock('../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/client')>();
  return {
    api: { get: vi.fn(), post: vi.fn() },
    getApiErrorMessage: actual.getApiErrorMessage,
  };
});

vi.mock('../hooks/useMediaQuery', () => ({
  useIsMobile: () => isMobileMock(),
}));

vi.mock('../components/ExecutionActionsModal', () => ({
  default: ({ documentId }: { documentId: number }) => <div>نافذة الإجراءات {documentId}</div>,
}));

import { api } from '../api/client';

const renderPage = () =>
  render(
    <MemoryRouter>
      <StrikeOffRemindersPage />
    </MemoryRouter>,
  );

function makeReminderDoc(overrides: Partial<DocumentResponse> = {}): DocumentResponse {
  return makeDocument({
    id: 7,
    displayFileNumber: '77',
    fileNumber: '77',
    fileType: 'حقوق',
    court: 'دمشق',
    applicant: 'المدعي',
    borrowerName: 'أحمد',
    borrowerFather: 'خالد',
    borrowerFamily: 'الخطيب',
    lastActivityDate: '2026-05-01T00:00:00Z',
    lastActivitySource: 'action',
    ...overrides,
  });
}

function mockPage(items: DocumentResponse[]) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: { page: 1, perPage: 20, totalCount: items.length, totalPages: 1, items },
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  isMobileMock.mockReturnValue(false);
});

describe('StrikeOffRemindersPage', () => {
  it('يعرض الأعمدة المطلوبة على المكتبي مع تاريخ آخر إجراء', async () => {
    mockPage([makeReminderDoc()]);

    renderPage();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('رقم الملف')).toBeInTheDocument();
    expect(within(table).getByText('نوعه')).toBeInTheDocument();
    expect(within(table).getByText('دارة التنفيذ')).toBeInTheDocument();
    expect(within(table).getByText('طالب التنفيذ')).toBeInTheDocument();
    expect(within(table).getByText('المنفذ عليه')).toBeInTheDocument();
    expect(within(table).getByText('تاريخ آخر إجراء')).toBeInTheDocument();
    expect(within(table).getByText('77')).toBeInTheDocument();
    expect(within(table).getByText('حقوق')).toBeInTheDocument();
    expect(within(table).getByText('المدعي')).toBeInTheDocument();
    expect(within(table).getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    // التاريخ يُعرض بصيغة العرض المعتمدة (لا نص ISO خام).
    expect(within(table).getByText(formatDate('2026-05-01T00:00:00Z'))).toBeInTheDocument();
  });

  it('يُعلن مصدر التاريخ حين لا يكون إجراءً مسجّلًا (تاريخ القيد/الإدخال)', async () => {
    mockPage([makeReminderDoc({ lastActivitySource: 'registration', lastActivityDate: '2026-04-01T00:00:00Z' })]);

    renderPage();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('تاريخ القيد')).toBeInTheDocument();
  });

  it('يعرض بطاقات على الجوال بلا جدول', async () => {
    isMobileMock.mockReturnValue(true);
    mockPage([makeReminderDoc()]);

    renderPage();

    expect(await screen.findByText('دارة التنفيذ:')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('يفتح نافذة الإجراءات والملاحظات للملف', async () => {
    const user = userEvent.setup();
    isMobileMock.mockReturnValue(true);
    mockPage([makeReminderDoc()]);

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'عرض الإجراءات والملاحظات' }));

    expect(screen.getByText('نافذة الإجراءات 7')).toBeInTheDocument();
  });

  it('يُلغي التذكير بعد تأكيد ثم يعيد الجلب ويُظهر رسالة', async () => {
    const user = userEvent.setup();
    isMobileMock.mockReturnValue(true);
    mockPage([makeReminderDoc()]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'إلغاء التذكير' }));
    // لا إرسال قبل التأكيد.
    expect(api.post).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'تأكيد إلغاء التذكير' }));

    expect(api.post).toHaveBeenCalledWith('/documents/7/strike-off-reminder/dismiss');
    expect(await screen.findByText(/أُلغي تذكير الشطب/)).toBeInTheDocument();
    // إعادة جلب القائمة بعد النجاح.
    expect((api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.length).toBeGreaterThan(1);
  });

  it('يعرض رسالة خطأ إن فشل الإلغاء', async () => {
    const user = userEvent.setup();
    isMobileMock.mockReturnValue(true);
    mockPage([makeReminderDoc()]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('فشل'));

    renderPage();
    await user.click(await screen.findByRole('button', { name: 'إلغاء التذكير' }));
    await user.click(screen.getByRole('button', { name: 'تأكيد إلغاء التذكير' }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  it('يعرض الحالة الفارغة', async () => {
    mockPage([]);

    renderPage();

    expect(await screen.findByText('لا ملفات مقاربة للشطب حاليًا.')).toBeInTheDocument();
  });
});
