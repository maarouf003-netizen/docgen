import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import ExecutedDocuments from './ExecutedDocuments';
import type { DocumentResponse } from '../types';
import { makeExecutedDocument } from '../test/factories';
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

beforeEach(() => {
  vi.clearAllMocks();
  isMobileMock.mockReturnValue(false);
});

describe('ExecutedDocuments', () => {
  it('يعرض الملفات المنفذة في جدول على المكتبي مع تاريخ التنفيذ واسم المنفذ عليه بلا زر إعادة', async () => {
    mockPage([makeExecutedDocument({ id: 7 })]);

    render(<ExecutedDocuments />);

    const table = await screen.findByRole('table');
    expect(within(table).getByText('تاريخ التنفيذ')).toBeInTheDocument();
    expect(within(table).getByText('محمود علي حسن')).toBeInTheDocument();
    expect(within(table).getByText('المدعي')).toBeInTheDocument();
    expect(within(table).getByText('دمشق')).toBeInTheDocument();
    expect(within(table).getByText('99 حقوق')).toBeInTheDocument();
    expect(within(table).queryByRole('button')).not.toBeInTheDocument();
  });

  it('يعرض بطاقات على الجوال مع شارة «منفذ» وسطر تاريخ التنفيذ', async () => {
    isMobileMock.mockReturnValue(true);
    mockPage([makeExecutedDocument({ id: 7 })]);

    render(<ExecutedDocuments />);

    expect(await screen.findByText('محمود علي حسن')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText('منفذ')).toBeInTheDocument();
    expect(screen.getByText(/نُفذ في/)).toBeInTheDocument();
  });

  it('يعرض تاريخ الإيداع في عمود التاريخ لملف «عرض وايداع»', async () => {
    mockPage([
      makeExecutedDocument({
        generalEntitySide: 'deposit',
        executedExecutionDate: undefined,
        executedDepositDate: '2026-07-20',
      }),
    ]);

    render(<ExecutedDocuments />);

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[0].textContent).toBe(formatDate('2026-07-20', '—'));
  });

  it('يعرض تاريخ براءة الذمة لملف «طالبة تنفيذ» منفذ بالتسوية', async () => {
    mockPage([
      makeExecutedDocument({
        generalEntitySide: 'applicant',
        execStatus: 'منفذ بالتسوية',
        executedStatus: '',
        baraetDate: '15/6/2026',
        executedExecutionDate: undefined,
      }),
    ]);

    render(<ExecutedDocuments />);

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[0].textContent).toBe('15/6/2026');
  });

  it('يعرض تاريخ قرار الإحالة القطعية لملف «طالبة تنفيذ» منفذ جبريا كاملا', async () => {
    mockPage([
      makeExecutedDocument({
        generalEntitySide: 'applicant',
        execStatus: 'منفذ جبريا',
        execSubStatus: 'منفذ كاملا',
        executedStatus: '',
        forcedExecutionDate: '10/5/2026',
        executedExecutionDate: undefined,
      }),
    ]);

    render(<ExecutedDocuments />);

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[0].textContent).toBe('10/5/2026');
  });

  it('يعرض «—» في تاريخ التنفيذ عند غياب القيمة', async () => {
    mockPage([makeExecutedDocument({ executedExecutionDate: undefined })]);

    render(<ExecutedDocuments />);

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[0].textContent).toBe('—');
  });

  it('يربط اسم الملف بصفحة الملف', async () => {
    mockPage([makeExecutedDocument({ id: 7 })]);

    render(<ExecutedDocuments />);

    await screen.findByRole('table');
    expect(screen.getByRole('link', { name: 'محمود علي حسن' })).toHaveAttribute('href', '/documents/7');
  });

  it('يرسل نص البحث إلى الخلفية مع الصفحة الأولى', async () => {
    const user = userEvent.setup();
    mockPage([]);

    render(<ExecutedDocuments />);
    await screen.findByText('لا توجد ملفات منفذة');

    await user.type(screen.getByPlaceholderText(/بحث في الملفات المنفذة/), 'محمود');

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('/documents/executed');
    expect(url).toContain('q=' + encodeURIComponent('محمود'));
    expect(url).toContain('page=1');
  });

  it('يعرض «لا توجد ملفات منفذة» عند قائمة فارغة', async () => {
    mockPage([]);

    render(<ExecutedDocuments />);

    expect(await screen.findByText('لا توجد ملفات منفذة')).toBeInTheDocument();
  });
});
