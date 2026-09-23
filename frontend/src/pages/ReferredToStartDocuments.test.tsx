import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import ReferredToStartDocuments from './ReferredToStartDocuments';
import type { DocumentResponse } from '../types';
import { makeDocument } from '../test/factories';
import { formatDate } from '../utils/dates';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

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

import { api } from '../api/client';

function mockPage(items: DocumentResponse[]) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: { page: 1, perPage: 20, totalCount: items.length, totalPages: 1, items },
  });
}

/** ملف «طالبة تنفيذ» محال الى البداية. */
function madeReferred(overrides: Partial<DocumentResponse> = {}) {
  return makeDocument({
    generalEntitySide: 'applicant',
    execStatus: 'محال الى البداية',
    executedStatus: '',
    startReferralDate: '2024-01-02',
    ...overrides,
  });
}

/** ملف قادم من «منفذ جبريا» أُحيل بجزئيته: يبقى «محال الى البداية» ولا يُطوى في «منفذ». */
function madePartialReferred(overrides: Partial<DocumentResponse> = {}) {
  return madeReferred({ execSubStatus: 'منفذ جزئيا', ...overrides });
}

beforeEach(() => {
  vi.clearAllMocks();
  isMobileMock.mockReturnValue(false);
});

describe('ReferredToStartDocuments', () => {
  it('يعرض الملفات المحالة في جدول على المكتبي مع تاريخ الإحالة واسم المنفذ عليه بلا زر إعادة', async () => {
    mockPage([madeReferred({ id: 7 })]);

    render(<ReferredToStartDocuments />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('تاريخ الإحالة')).toBeInTheDocument();
    expect(within(table).getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(within(table).getByText(formatDate('2024-01-02', '—'))).toBeInTheDocument();
    expect(within(table).getByText('المدعي')).toBeInTheDocument();
    expect(within(table).getByText('دمشق')).toBeInTheDocument();
    expect(within(table).getByText('99 حقوق')).toBeInTheDocument();
    expect(within(table).queryByRole('button')).not.toBeInTheDocument();
  });

  it('يعرض بطاقات على الجوال مع شارة «محال الى البداية» وسطر تاريخ الإحالة', async () => {
    isMobileMock.mockReturnValue(true);
    mockPage([madeReferred({ id: 7 })]);

    render(<ReferredToStartDocuments />);

    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText('محال الى البداية')).toBeInTheDocument();
    expect(screen.getByText(/أُحيل في/)).toBeInTheDocument();
  });

  it('يُبقي القادم من «منفذ جبريا» المحال بجزئيته بشارة «محال الى البداية» لا «منفذ»', async () => {
    // الشارة تُعرض على بطاقات الجوال (cardTopRight) لا في أعمدة جدول المكتبي.
    isMobileMock.mockReturnValue(true);
    mockPage([madePartialReferred({ id: 8 })]);

    render(<ReferredToStartDocuments />);

    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.getByText('محال الى البداية')).toBeInTheDocument();
    expect(screen.queryByText('منفذ')).not.toBeInTheDocument();
    expect(screen.queryByText('متداول / منفذ جزئيا')).not.toBeInTheDocument();
  });

  it('يعرض «—» في تاريخ الإحالة عند غياب القيمة', async () => {
    mockPage([madeReferred({ startReferralDate: undefined })]);

    render(<ReferredToStartDocuments />);

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[0].textContent).toBe('—');
  });

  it('يربط اسم الملف بصفحة الملف', async () => {
    mockPage([madeReferred({ id: 7 })]);

    render(<ReferredToStartDocuments />);

    await screen.findByRole('table');
    expect(screen.getByRole('link', { name: 'أحمد خالد الخطيب' })).toHaveAttribute('href', '/documents/7');
  });

  it('يرسل نص البحث إلى نقطة referred-to-start مع الصفحة الأولى', async () => {
    const user = userEvent.setup();
    mockPage([]);

    render(<ReferredToStartDocuments />);
    await screen.findByText('لا توجد ملفات محالة الى البداية');

    await user.type(screen.getByPlaceholderText(/بحث في الملفات المحالة الى البداية/), 'مقترض');

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('/documents/referred-to-start');
    expect(url).toContain('q=' + encodeURIComponent('مقترض'));
    expect(url).toContain('page=1');
  });

  it('يعرض «لا توجد ملفات محالة الى البداية» عند قائمة فارغة', async () => {
    mockPage([]);

    render(<ReferredToStartDocuments />);

    expect(await screen.findByText('لا توجد ملفات محالة الى البداية')).toBeInTheDocument();
  });
});