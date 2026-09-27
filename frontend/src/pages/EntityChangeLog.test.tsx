import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import EntityChangeLog from './EntityChangeLog';

// العقد المصدَر `‎{message}‎`: الدالة الحقيقية (لا محاكاة) — أي كسر في
// `getApiErrorMessage` يكسر هذا الملف، وهذا مقصود (حارس العقد من جهة العرض).
vi.mock('../api/client', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/client')>();
  return { ...actual, api: { get: vi.fn() } };
});
vi.mock('../utils/dates', () => ({ formatDateTime: (v: string) => v }));

import { api } from '../api/client';

const mockGet = api.get as unknown as ReturnType<typeof vi.fn>;

function row(overrides: Record<string, unknown> = {}) {
  return {
    id: 1,
    entryId: 10,
    groupId: 20,
    actionKind: 'move',
    actionKindLabel: 'نقل قيد',
    decreeKind: 'قرار',
    decreeNumber: '7',
    decreeDate: '2026-08-01',
    summaryAr: 'تم نقل قيد من «جهة أ» إلى «جهة ب» بموجب قرار رقم 7',
    summaryDegraded: false,
    actorUserId: 3,
    actorName: 'رئيس قسم دمشق',
    createdAtUtc: '2026-08-01T10:00:00Z',
    governorate: 'دمشق',
    canonicalName: 'جهة ب',
    ...overrides,
  };
}

function mockRows(items: unknown[]) {
  mockGet.mockResolvedValue({ data: { items, page: 1, perPage: 20, totalCount: items.length } });
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('EntityChangeLog', () => {
  it('يعرض العنوان وزر التصدير', async () => {
    mockRows([]);
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);
    expect(await screen.findByText('سجل تغييرات الجهات')).toBeInTheDocument();
    expect(screen.getByLabelText('تصدير سجل التغييرات إلى Excel')).toBeInTheDocument();
  });

  it('يعرض الملخص العربي والتسمية من الخادم لا JSON خاما', async () => {
    mockRows([row()]);
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);

    const summaries = await screen.findAllByText('تم نقل قيد من «جهة أ» إلى «جهة ب» بموجب قرار رقم 7');
    expect(summaries.length).toBeGreaterThan(0);
    expect(screen.getAllByText('نقل قيد').length).toBeGreaterThan(0);
    expect(screen.queryByText(/{/)).not.toBeInTheDocument();
  });

  it('اسم المستخدم يرسل نصا كما هو بعد التأجيل', async () => {
    const user = userEvent.setup();
    mockRows([]);
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);
    await screen.findByText('سجل تغييرات الجهات');

    await user.type(screen.getByLabelText('المستخدم'), 'محمد');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await act(() => new Promise((resolve) => setTimeout(resolve, 400)));
    const lastUrl = mockGet.mock.calls.at(-1)?.[0] as string | undefined;
    expect(lastUrl).toBeDefined();
    expect(new URLSearchParams(lastUrl!.split('?')[1]).get('actorUserId')).toBe('محمد');
  }, 8000);

  it('المدخل الرقمي (بأرقام عربية أيضا) يرسل مطبعا بعد التأجيل', async () => {
    const user = userEvent.setup();
    mockRows([]);
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);
    await screen.findByText('سجل تغييرات الجهات');

    await user.type(screen.getByLabelText('المستخدم'), '١٢٣');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await act(() => new Promise((resolve) => setTimeout(resolve, 400)));
    const lastUrl = mockGet.mock.calls.at(-1)?.[0] as string | undefined;
    expect(lastUrl).toBeDefined();
    expect(lastUrl).toContain('actorUserId=123');
  }, 8000);

  it('رسالة الخادم العربية تظهر كما هي لا كخطأ عام', async () => {
    mockGet.mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { message: 'تعذر تحميل السجل — حاول مرة أخرى' } },
    });
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);

    expect(await screen.findByText('تعذر تحميل السجل — حاول مرة أخرى')).toBeInTheDocument();
    expect(screen.queryByText('حدث خطأ غير متوقع')).not.toBeInTheDocument();
  });

  it('يعرض شارة «ملخص منقوص» للسطر الموسوم فقط (جدول المكتبي وبطاقة الجوال)', async () => {
    mockRows([row({ id: 1, summaryDegraded: true }), row({ id: 2 })]);
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);

    // الشارة في التخطيطين معًا (جدول `md:block` وبطاقات `md:hidden`) — موسوم واحد × تخطيطين.
    expect(await screen.findAllByText('ملخص منقوص')).toHaveLength(2);
  });

  it('لا يعرض أي شارة تدهور بلا وسم', async () => {
    mockRows([row()]);
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);

    await screen.findAllByText('تم نقل قيد من «جهة أ» إلى «جهة ب» بموجب قرار رقم 7');
    expect(screen.queryByText('ملخص منقوص')).not.toBeInTheDocument();
  });

  it('فلتر المستخدم بالرموز فقط يوضح أن الفراغ قد يعني عدم المطابقة', async () => {
    const user = userEvent.setup();
    mockRows([]);
    render(<MemoryRouter><EntityChangeLog /></MemoryRouter>);
    await screen.findByText('سجل تغييرات الجهات');

    await user.type(screen.getByLabelText('المستخدم'), '...');
    await act(() => new Promise((resolve) => setTimeout(resolve, 400)));

    // التلميح في التخطيطين معًا (جدول `md:block` وبطاقات `md:hidden`).
    expect(await screen.findAllByText(/الرموز وحدها قد لا تطابق أي اسم/)).toHaveLength(2);
  }, 8000);
});
