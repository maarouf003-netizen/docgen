import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import ArchivedDocumentsList, { type ArchivedDocumentsListConfig } from './ArchivedDocumentsList';
import type { DocumentResponse } from '../types';
import { makeDocument } from '../test/factories';

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

function baseConfig(overrides: Partial<ArchivedDocumentsListConfig> = {}): ArchivedDocumentsListConfig {
  return {
    title: 'الملفات المحذوفة',
    searchPlaceholder: 'بحث...',
    emptyText: 'لا توجد ملفات',
    showBackLink: false,
    fetchEndpoint: '/documents/deleted',
    restoreEndpoint: (id) => `/documents/${id}/restore`,
    restoreButtonLabel: 'استعادة',
    confirmRestoreLabel: 'تأكيد',
    restoringLabel: 'جارِ...',
    successMessage: (name) => `تم "${name}"`,
    dateColumnHeader: 'تاريخ الحذف',
    dateCell: () => '—',
    cardTopRight: () => <span>أعلى</span>,
    displayName: (d) => d.borrowerName ?? '',
    linkToDocument: false,
    canRestore: true,
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

describe('ArchivedDocumentsList', () => {
  it('يعرض العنوان وتسمية عمود التاريخ وقيمة خلية التاريخ المخصصة', async () => {
    mockPage([makeDocument({ borrowerName: 'أحمد' })]);

    render(<ArchivedDocumentsList config={baseConfig({ dateCell: () => 'أمس' })} />);

    const table = await screen.findByRole('table');
    expect(screen.getByRole('heading', { name: 'الملفات المحذوفة' })).toBeInTheDocument();
    expect(within(table).getByText('تاريخ الحذف')).toBeInTheDocument();
    expect(within(table).getByText('أمس')).toBeInTheDocument();
  });

  it('يعرض ويخفي وصلة «الملفات التنفيذية» وفق showBackLink', async () => {
    mockPage([]);
    const { rerender } = render(<ArchivedDocumentsList config={baseConfig({ showBackLink: true })} />);
    expect(await screen.findByText('لا توجد ملفات')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← الملفات التنفيذية' })).toHaveAttribute('href', '/documents');

    rerender(<ArchivedDocumentsList config={baseConfig({ showBackLink: false })} />);
    expect(screen.queryByRole('link', { name: '← الملفات التنفيذية' })).not.toBeInTheDocument();
  });

  it('يجعل اسم الملف رابطًا عند linkToDocument ونصًا عاديًا عند غيابه', async () => {
    mockPage([makeDocument({ id: 3, borrowerName: 'أحمد' })]);
    const { rerender } = render(<ArchivedDocumentsList config={baseConfig({ linkToDocument: true })} />);
    expect(await screen.findByRole('link', { name: 'أحمد' })).toHaveAttribute('href', '/documents/3');

    rerender(<ArchivedDocumentsList config={baseConfig({ linkToDocument: false })} />);
    expect(screen.queryByRole('link', { name: 'أحمد' })).not.toBeInTheDocument();
    expect(screen.getByText('أحمد')).toBeInTheDocument();
  });

  it('يستقبل نقطتي الجلب والإعادة والنصوص من الإعدادات', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 3, borrowerName: 'أحمد' })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const cfg = baseConfig({
      fetchEndpoint: '/documents/struck-off',
      restoreEndpoint: (id) => `/documents/${id}/restore-struck-off`,
      restoreButtonLabel: 'إعادة الملف',
      confirmRestoreLabel: 'تأكيد الإعادة',
      restoringLabel: 'جارِ الإعادة...',
      successMessage: (name) => `أعيد "${name}" إلى المتداول`,
    });

    render(<ArchivedDocumentsList config={cfg} />);
    const table = await screen.findByRole('table');
    expect(api.get).toHaveBeenCalledWith(
      expect.stringContaining('/documents/struck-off'),
      expect.any(Object),
    );

    await user.click(within(table).getByRole('button', { name: 'إعادة الملف' }));
    await user.click(within(table).getByRole('button', { name: 'تأكيد الإعادة' }));
    expect(api.post).toHaveBeenCalledWith('/documents/3/restore-struck-off');
    expect(await screen.findByText(/أعيد "أحمد" إلى المتداول/)).toBeInTheDocument();
  });

  it('يعرض عنصري البطاقة العلوي والسفلي المخصصين على الجوال', async () => {
    isMobileMock.mockReturnValue(true);
    mockPage([makeDocument({ borrowerName: 'أحمد' })]);

    render(
      <ArchivedDocumentsList
        config={baseConfig({
          cardTopRight: () => <span>شارة</span>,
          cardBottomExtra: (d) => <div>سطر: {d.court}</div>,
        })}
      />,
    );

    expect(await screen.findByText('أحمد')).toBeInTheDocument();
    expect(screen.getByText('شارة')).toBeInTheDocument();
    expect(screen.getByText('سطر: دمشق')).toBeInTheDocument();
  });

  it('يعرض حقول التجديد عند تأكيد الإعادة حين requiresRenewal ويرسل رقم الملف الجديد', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 3, borrowerName: 'أحمد' })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });

    render(<ArchivedDocumentsList config={baseConfig({ requiresRenewal: true })} />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'استعادة' }));

    expect(screen.getByLabelText(/رقم الملف الجديد/)).toBeInTheDocument();
    expect(screen.getByLabelText(/تاريخ التجديد/)).toBeInTheDocument();
    expect(screen.getByLabelText(/رقم الملف الجديد/)).toHaveAttribute('id', 'restore-renewalFileNumber');

    await user.click(within(table).getByRole('button', { name: 'تأكيد' }));

    expect(api.post).not.toHaveBeenCalled();
    expect(screen.getByText('رقم الملف الجديد مطلوب عند إعادة الملف المشطوب')).toBeInTheDocument();
  });

  it('يتطلب سنة الإعادة عند إعادة ملف «طالبة تنفيذ» المشطوب', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 3, borrowerName: 'أحمد' })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });

    render(<ArchivedDocumentsList config={baseConfig({ requiresRenewal: true })} />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'استعادة' }));
    await user.type(screen.getByLabelText(/رقم الملف الجديد/), '999');
    await user.click(within(table).getByRole('button', { name: 'تأكيد' }));

    expect(api.post).not.toHaveBeenCalled();
    expect(screen.getByText('سنة الإعادة مطلوبة عند إعادة ملف «طالبة تنفيذ» المشطوب')).toBeInTheDocument();
  });

  it('يرسل رقم وسنة الإعادة عند إكمال إعادة ملف طالبة تنفيذ', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 3, borrowerName: 'أحمد' })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });

    render(<ArchivedDocumentsList config={baseConfig({ requiresRenewal: true })} />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'استعادة' }));
    await user.type(screen.getByLabelText(/رقم الملف الجديد/), '999');
    await user.type(screen.getByLabelText(/سنة الإعادة/), '2024');
    await user.click(within(table).getByRole('button', { name: 'تأكيد' }));

    expect(api.post).toHaveBeenCalledWith('/documents/3/restore', {
      renewalFileReceiptNumber: null,
      renewalFileReceiptDate: null,
      renewalFileNumber: '999',
      renewalFileType: null,
      renewalYear: 2024,
      renewalDate: null,
    });
  });

  it('لا يطلب حقول التجديد عند غياب requiresRenewal ويستعيد دون جسم إضافي', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 3, borrowerName: 'أحمد' })]);
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });

    render(<ArchivedDocumentsList config={baseConfig()} />);
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'استعادة' }));
    expect(screen.queryByLabelText(/رقم الملف الجديد/)).not.toBeInTheDocument();

    await user.click(within(table).getByRole('button', { name: 'تأكيد' }));
    expect(api.post).toHaveBeenCalledWith('/documents/3/restore');
  });
});
