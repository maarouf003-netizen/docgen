import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentsList from './DocumentsList';
import type { DocumentResponse, PagedResult } from '../types';
import { makeDocument } from '../test/factories';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function mockPage(items: DocumentResponse[]) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url.startsWith('/documents/filter-options')) {
      return Promise.resolve({
        data: {
          applicants: ['المدعي'],
          courts: ['دمشق'],
          lawyers: ['المحامي سامر'],
          administrativeBranches: ['الفرع الرئيسي - دمشق'],
          branches: ['فرع المزة'],
          publicEntityBranches: ['فرع المزة'],
        },
      });
    }
    return Promise.resolve({
      data: { page: 1, perPage: 20, totalCount: items.length, totalPages: 1, items },
    });
  });
}

/** تحميل القائمة مع تصدير إكسل عبر axios (blob) كما يفعل المكوّن الفعلي. */
function mockPageWithExport(items: DocumentResponse[]) {
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url.startsWith('/documents/export')) {
      return Promise.resolve({ data: new Blob(['xlsx']) });
    }
    if (url.startsWith('/documents/filter-options')) {
      return Promise.resolve({
        data: {
          applicants: ['المدعي'],
          courts: ['دمشق'],
          lawyers: ['المحامي سامر'],
          administrativeBranches: ['الفرع الرئيسي - دمشق'],
          branches: ['فرع المزة'],
          publicEntityBranches: ['فرع المزة'],
        },
      });
    }
    return Promise.resolve({
      data: { page: 1, perPage: 20, totalCount: items.length, totalPages: 1, items },
    });
  });
}

function expectExportRequestedWithStatus() {
  expect(api.get).toHaveBeenCalledWith(
    '/documents/export',
    expect.objectContaining({
      params: expect.objectContaining({ status: 'متداول' }),
      responseType: 'blob',
    }),
  );
}

function renderList() {
  return render(<DocumentsList />);
}

function stubMobile() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: true,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

function stubDesktop() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  stubDesktop();
  Element.prototype.scrollIntoView = vi.fn();
  useAuthMock.mockReturnValue({
    hasFullAccess: true,
    isHead: false,
    user: { role: 'manager' },
  });
});

describe('DocumentsList', () => {
  it('يعرض الأعمدة الجديدة بالترتيب المعتمد: فرع الإدارة، الحالة، طالب التنفيذ، فرع الجهة العامة، المنفذ عليه، دائرة التنفيذ، رقم الملف، المحامي المختص، الإجراءات والملاحظات، عدد المشاهدات', async () => {
    const page: PagedResult<DocumentResponse> = {
      page: 1,
      perPage: 20,
      totalCount: 1,
      totalPages: 1,
      items: [
        makeDocument({ id: 1, isDraft: true, execStatus: '', documentType: 'تحت رفع - س' }),
      ],
    };
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: page });

    renderList();

    const table = await screen.findByRole('table');
    const headers = within(table).getAllByRole('columnheader').map((h) => h.textContent ?? '');
    const expected = [
      'فرع الإدارة',
      'الحالة',
      'طالب التنفيذ',
      'فرع الجهة العامة',
      'المنفذ عليه',
      'دائرة التنفيذ',
      'رقم الملف',
      'المحامي المختص',
      'الإجراءات والملاحظات',
      'عدد المشاهدات',
    ];
    expect(headers.length).toBe(expected.length);
    expected.forEach((label, i) => {
      expect(headers[i]).toContain(label);
    });
    expect(within(table).getByRole('button', { name: 'فلترة الحالة' })).toBeInTheDocument();
    expect(within(table).getByRole('button', { name: 'فلترة طالب التنفيذ' })).toBeInTheDocument();
    expect(within(table).getByRole('button', { name: 'فلترة دائرة التنفيذ' })).toBeInTheDocument();
    expect(within(table).queryByText('النوع')).not.toBeInTheDocument();
    expect(within(table).queryByText('المقترض')).not.toBeInTheDocument();
  });

  it('يعرض اسم أول «طالب تنفيذ» (الاسم الثلاثي) في عمود طالب التنفيذ لملف وضع «منفذ عليه»', async () => {
    const page: PagedResult<DocumentResponse> = {
      page: 1,
      perPage: 20,
      totalCount: 1,
      totalPages: 1,
      items: [
        makeDocument({
          id: 9,
          generalEntitySide: 'executed',
          applicant: '',
          executionApplicants: [{ id: 1, name: 'سليم', father: 'حسن', family: 'علي' }],
          executedNaturalPersons: [{ name: 'محمود', father: 'علي', family: 'حسن' }],
        }),
      ],
    };
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: page });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('سليم حسن علي')).toBeInTheDocument();
    expect(within(table).getByText('محمود علي حسن')).toBeInTheDocument();
  });

  it('يعرض «—» في عمود طالب التنفيذ لملف «منفذ عليه» بلا طالب تنفيذ ولا حقل applicant', async () => {
    const page: PagedResult<DocumentResponse> = {
      page: 1,
      perPage: 20,
      totalCount: 1,
      totalPages: 1,
      items: [
        makeDocument({
          id: 10,
          generalEntitySide: 'executed',
          applicant: '',
          executionApplicants: [],
          executedNaturalPersons: [{ name: 'محمود', father: 'علي', family: 'حسن' }],
        }),
      ],
    };
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: page });

    renderList();

    const table = await screen.findByRole('table');
    const rows = within(table).getAllByRole('row');
    expect(rows[1].textContent).toContain('محمود علي حسن');
    expect(rows[1].textContent).not.toContain('سليم');
  });

  it('يعرض الحالة بأحد أشكالها الأربعة دون «بدون حالة»', async () => {
    const page: PagedResult<DocumentResponse> = {
      page: 1,
      perPage: 20,
      totalCount: 4,
      totalPages: 1,
      items: [
        makeDocument({ id: 1, isDraft: true, execStatus: '', documentType: 'تحت رفع - س' }),
        makeDocument({ id: 2, isDraft: false, execStatus: '', documentType: 'متداول - ص' }),
        makeDocument({ id: 3, isDraft: false, execStatus: 'منفذ بالتسوية', documentType: 'متداول - ق' }),
        makeDocument({ id: 4, isDraft: true, execStatus: 'تريث', documentType: 'تحت رفع - ر' }),
      ],
    };
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: page });

    renderList();

    const table = await screen.findByRole('table');
    const tbody = within(table).getAllByRole('rowgroup').at(-1) as HTMLElement;
    expect(within(tbody).getByText('تحت رفع')).toBeInTheDocument();
    expect(within(tbody).getAllByText('متداول').length).toBeGreaterThan(0);
    expect(within(tbody).getByText('منفذ')).toBeInTheDocument();
    expect(within(tbody).getByText('تريث')).toBeInTheDocument();
    expect(within(tbody).queryByText('بدون حالة')).not.toBeInTheDocument();
  });

  it('يرسل فلتر «متداول» إلى الخلفية', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 0, totalPages: 0, items: [] },
    });

    renderList();
    const table = await screen.findByRole('table');
    await user.click(within(table).getByRole('button', { name: 'فلترة الحالة' }));
    const menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'متداول' }));

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('status=' + encodeURIComponent('متداول'));
  });

  it('يعرض اسم المنفذ عليه الثلاثي كرابط لصفحة التفاصيل', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 5 })] },
    });

    renderList();

    const link = await screen.findByRole('link', { name: 'أحمد خالد الخطيب' });
    expect(link).toHaveAttribute('href', '/documents/5');
  });

  it('يعرض رقم الملف مع نوعه مثل «99 حقوق»', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ fileNumber: '99', fileType: 'حقوق', isDraft: false })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('99 حقوق')).toBeInTheDocument();
  });

  it('يعرض رقم أساس السنة الحالية بدل رقم الملف عند وجوده: «1500 حقوق»', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [
          makeDocument({ fileNumber: '99', displayFileNumber: '1500', fileType: 'حقوق', isDraft: false }),
        ],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('1500 حقوق')).toBeInTheDocument();
    expect(within(table).queryByText('99 حقوق')).not.toBeInTheDocument();
  });

  it('يعرض رقم الملف فقط عند غياب النوع', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ fileNumber: '99', fileType: undefined, isDraft: false })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('99')).toBeInTheDocument();
  });

  it('يعرض أعمدة المحامي بالترتيب المعتمد: الحالة، طالب التنفيذ، الفرع، المنفذ عليه، دائرة التنفيذ، رقم الملف، الإجراءات', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 1, applicantPublicEntities: [{ id: 1, name: 'المصرف', branch: 'فرع 1' }] })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[1].textContent).toBe('المدعي');
    expect(cells[2].textContent).toBe('فرع 1');
    expect(cells[3].textContent).toBe('أحمد خالد الخطيب');
    expect(cells[4].textContent).toBe('دمشق');
    expect(within(table).queryByRole('button', { name: 'فلترة الفرع' })).not.toBeInTheDocument();
    expect(within(table).queryByRole('button', { name: 'فلترة فرع الإدارة' })).not.toBeInTheDocument();
  });

  it('يعرض فراغًا في رقم الملف عندما يكون الملف تحت رفع', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ fileNumber: undefined, fileYear: undefined, isDraft: true })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    const fileNumberCell = within(table).getAllByRole('cell')[5];
    expect(fileNumberCell.textContent).toBe('');
  });

  it('يعرض عمود «عدد المشاهدات» للمدير', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ viewCount: 7 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('عدد المشاهدات')).toBeInTheDocument();
    expect(within(table).getByText('7')).toBeInTheDocument();
  });

  it('يعرض عمود «عدد المشاهدات» لرئيس القسم', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: true });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ viewCount: 3 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('عدد المشاهدات')).toBeInTheDocument();
    expect(within(table).getByText('3')).toBeInTheDocument();
  });

  it('يخفي عمود «عدد المشاهدات» عن المحامي', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ viewCount: 7 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).queryByText('عدد المشاهدات')).not.toBeInTheDocument();
    expect(within(table).queryByText('7')).not.toBeInTheDocument();
  });

  it('يعرض فلاتر الحالة وطالب التنفيذ ودائرة التنفيذ بجانب أعمدة الجدول على المكتبي ويُفلتر عند الاختيار', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    const applicantButton = within(table).getByRole('button', { name: 'فلترة طالب التنفيذ' });
    expect(applicantButton).toBeInTheDocument();
    await user.click(applicantButton);
    let menu = screen.getByRole('menu', { name: 'فلترة طالب التنفيذ' });
    await user.click(within(menu).getByRole('menuitem', { name: 'المدعي' }));
    let [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('applicant=' + encodeURIComponent('المدعي'));

    const courtButton = within(table).getByRole('button', { name: 'فلترة دائرة التنفيذ' });
    expect(courtButton).toBeInTheDocument();
    await user.click(courtButton);
    menu = screen.getByRole('menu', { name: 'فلترة دائرة التنفيذ' });
    await user.click(within(menu).getByRole('menuitem', { name: 'دمشق' }));
    [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('court=' + encodeURIComponent('دمشق'));

    const statusButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    expect(statusButton).toBeInTheDocument();
    await user.click(statusButton);
    menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'تريث' }));
    [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('status=' + encodeURIComponent('تريث'));
  });

  it('يستبعد «منفذ» من فلتر الحالة في القائمة الرئيسية (صفحته مستقلة)', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');
    const statusButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    await user.click(statusButton);

    const menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    expect(within(menu).queryByRole('menuitem', { name: 'منفذ' })).not.toBeInTheDocument();
    for (const option of ['تريث', 'تحت رفع', 'متداول']) {
      expect(within(menu).getByRole('menuitem', { name: option })).toBeInTheDocument();
    }
  });

  it('يُلغي «عرض الكل» الفلتر النشط ويغلق قائمة العمود ويميز السهم بلون الفلتر النشط', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');
    const statusButton = within(table).getByRole('button', { name: 'فلترة الحالة' });

    await user.click(statusButton);
    let menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'متداول' }));
    let [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('status=' + encodeURIComponent('متداول'));
    const activeButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    expect(activeButton.className).toContain('text-emerald-900');
    expect(activeButton.className).toContain('font-bold');
    expect(activeButton.querySelector('svg')?.getAttribute('class')).toContain('text-red-600');

    await user.click(within(table).getByRole('button', { name: 'فلترة الحالة' }));
    menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'كل الحالات' }));
    [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).not.toContain('status=');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    const clearedButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    expect(clearedButton.className).toContain('text-emerald-900');
    expect(clearedButton.className).toContain('font-bold');
    expect(clearedButton.querySelector('svg')?.getAttribute('class')).toContain('text-gray-400');
  });

  it('يغلق قائمة فلتر العمود بمفتاح Escape دون تطبيق التغيير', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة الحالة' }));
    expect(screen.getByRole('menu', { name: 'فلترة الحالة' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).not.toContain('status=');
  });

  it('يغلق قائمة فلتر العمود عند النقر خارجها دون تطبيق التغيير', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة الحالة' }));
    expect(screen.getByRole('menu', { name: 'فلترة الحالة' })).toBeInTheDocument();
    await user.click(screen.getByPlaceholderText(/بحث بالاسم الثنائي/));
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).not.toContain('status=');
  });

  it('لا يعرض فلاتر طالب التنفيذ ودائرة التنفيذ في شريط الفلترة على المكتبي (موجودة في رؤوس الأعمدة)', async () => {
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    await screen.findByRole('table');

    const searchInput = screen.getByPlaceholderText(/بحث بالاسم الثنائي/);
    const topBar = searchInput.closest('div');
    expect(topBar).not.toBeNull();
    expect(within(topBar as HTMLElement).queryByRole('combobox', { name: 'فلترة طالب التنفيذ' })).not.toBeInTheDocument();
    expect(within(topBar as HTMLElement).queryByRole('combobox', { name: 'فلترة دائرة التنفيذ' })).not.toBeInTheDocument();
  });

  it('يعرض فلاتر طالب التنفيذ ودائرة التنفيذ في شريط الفلترة على الجوال', async () => {
    stubMobile();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    await screen.findByRole('link', { name: 'أحمد خالد الخطيب' });

    expect(screen.getByRole('combobox', { name: 'فلترة طالب التنفيذ' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'فلترة دائرة التنفيذ' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'فلترة الحالة' })).toBeInTheDocument();
  });

  it('يعرض آخر إجراء وتاريخه في عمود الإجراءات والملاحظات', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: true, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [
          makeDocument({
            executionActions: [
              { id: 2, type: 'action', text: 'الإجراء الأحدث', actionDate: '2/8/2026', createdByName: 'محامي', createdAt: '2026-08-02' },
              { id: 1, type: 'action', text: 'إجراء أقدم', actionDate: '1/8/2026', createdByName: 'محامي', createdAt: '2026-08-01' },
            ],
          }),
        ],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('الإجراءات والملاحظات')).toBeInTheDocument();
    expect(within(table).getByText('الإجراء الأحدث')).toBeInTheDocument();
    expect(within(table).getByText('2/8/2026')).toBeInTheDocument();
    expect(within(table).queryByText('الإجراء الأقدم')).not.toBeInTheDocument();
  });

  it('يفتح نافذة الإجراءات والملاحظات عند النقر على العمود', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ hasFullAccess: true, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 5, executionActions: [] })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    await user.click(within(table).getAllByText('لا توجد إجراءات أو ملاحظات')[0]);

    expect((await screen.findAllByText('الإجراءات والملاحظات')).length).toBeGreaterThan(0);
    expect(api.get).toHaveBeenCalledWith('/documents/5/actions');
  });

  it('يعرض بطاقات على شاشة الموبايل بدلاً من الجدول', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 5, viewCount: 7 })],
      },
    });

    renderList();

    expect(await screen.findByRole('link', { name: 'أحمد خالد الخطيب' })).toHaveAttribute(
      'href',
      '/documents/5',
    );
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText(/99 حقوق/)).toBeInTheDocument();
    expect(screen.getByText(/مشاهدات: 7/)).toBeInTheDocument();
  });

  it('يعرض بطاقة الموبايل للمحامي بصيغة «طالب التنفيذ · فرع الجهة العامة · دائرة التنفيذ»', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 5, applicantPublicEntities: [{ id: 1, name: 'المصرف', branch: 'فرع المزة' }] })],
      },
    });

    renderList();

    expect(await screen.findByRole('link', { name: 'أحمد خالد الخطيب' })).toBeInTheDocument();
    expect(screen.getByText(/المدعي · فرع المزة · دمشق/)).toBeInTheDocument();
  });

  it('يعرض «لا توجد نتائج» كبطاقة عند عدم وجود ملفات على الموبايل', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 0, totalPages: 0, items: [] },
    });

    renderList();

    expect(await screen.findByText('لا توجد نتائج')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('يعرض زر «ادخال ملف جديد» للمحامي فقط', async () => {
    useAuthMock.mockReturnValue({
      hasFullAccess: false,
      isHead: false,
      user: { role: 'lawyer' },
    });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 0, totalPages: 0, items: [] },
    });

    renderList();

    expect(await screen.findByRole('link', { name: /ادخال ملف جديد/ })).toHaveAttribute(
      'href',
      '/documents/new',
    );
  });

  it('يخفي زر «ادخال ملف جديد» عن المدير والمشرف ورئيس القسم', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 0, totalPages: 0, items: [] },
    });

    renderList();

    expect(await screen.findByRole('table')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /ادخال ملف جديد/ })).not.toBeInTheDocument();
  });

  it('يعرض عمودي «المحامي المختص» و«فرع الإدارة» للمدير بقيمتيهما', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 1, lawyer: 'المحامي سامر', administrativeBranchName: 'الفرع الرئيسي - دمشق' })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('المحامي المختص')).toBeInTheDocument();
    expect(within(table).getByText('فرع الإدارة')).toBeInTheDocument();
    expect(within(table).getByText('المحامي سامر')).toBeInTheDocument();
    expect(within(table).getByText('الفرع الرئيسي - دمشق')).toBeInTheDocument();
  });

  it('يعرض عمود «المحامي المختص» لرئيس القسم دون «فرع الإدارة»', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: true });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 1, lawyer: 'المحامي سامر', administrativeBranchName: 'الفرع الرئيسي - دمشق' })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('المحامي المختص')).toBeInTheDocument();
    expect(within(table).getByText('المحامي سامر')).toBeInTheDocument();
    expect(within(table).queryByText('فرع الإدارة')).not.toBeInTheDocument();
  });

  it('يخفي عمودي «فرع الإدارة» و«المحامي المختص» عن المحامي', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 1 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).queryByText('فرع الإدارة')).not.toBeInTheDocument();
    expect(within(table).queryByText('المحامي المختص')).not.toBeInTheDocument();
  });

  it('يُفلتر باسم المحامي عند الاختيار من عمود «المحامي المختص»', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة المحامي المختص' }));
    const menu = screen.getByRole('menu', { name: 'فلترة المحامي المختص' });
    await user.click(within(menu).getByRole('menuitem', { name: 'المحامي سامر' }));

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    expect(params.get('lawyer')).toBe('المحامي سامر');
  });

  it('يعرض فلتر المحامي المختص في شريط الفلترة على الجوال لرئيس القسم والمدير', async () => {
    stubMobile();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    await screen.findByRole('link', { name: 'أحمد خالد الخطيب' });

    expect(screen.getByRole('combobox', { name: 'فلترة المحامي المختص' })).toBeInTheDocument();
  });

  it('يعرض فرع الإدارة والمحامي المختص في بطاقة الموبايل للمدير', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 5, lawyer: 'المحامي سامر', administrativeBranchName: 'فرع الرقة' })],
      },
    });

    renderList();

    expect(await screen.findByRole('link', { name: 'أحمد خالد الخطيب' })).toBeInTheDocument();
    expect(screen.getByText(/المحامي المختص: المحامي سامر/)).toBeInTheDocument();
    expect(screen.getByText(/فرع الإدارة: فرع الرقة/)).toBeInTheDocument();
  });

  it('يُفلتر بفرع الإدارة عند الاختيار من عمود «فرع الإدارة» للمدير', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة فرع الإدارة' }));
    const menu = screen.getByRole('menu', { name: 'فلترة فرع الإدارة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'الفرع الرئيسي - دمشق' }));

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    expect(params.get('administrativeBranch')).toBe('الفرع الرئيسي - دمشق');
  });

  it('يُفلتر بالجهة العامة المنفذ عليها عند الاختيار من عمود «المنفذ عليه»', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({
          data: {
            applicants: [],
            courts: [],
            lawyers: [],
            administrativeBranches: [],
            branches: [],
            executedEntities: ['المصرف العقاري'],
          },
        });
      }
      return Promise.resolve({
        data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 1 })] },
      });
    });

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة الجهة العامة المنفذ عليها' }));
    const menu = screen.getByRole('menu', { name: 'فلترة الجهة العامة المنفذ عليها' });
    await user.click(within(menu).getByRole('menuitem', { name: 'المصرف العقاري' }));

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    expect(params.get('executedEntity')).toBe('المصرف العقاري');
  });

  it('يعرض فلتر الجهة العامة المنفذ عليها في شريط الفلاتر على الموبايل', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({
          data: {
            applicants: [],
            courts: [],
            lawyers: [],
            administrativeBranches: [],
            branches: [],
            executedEntities: ['المصرف التجاري'],
          },
        });
      }
      return Promise.resolve({
        data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 1 })] },
      });
    });

    renderList();

    const select = await screen.findByRole('combobox', { name: 'فلترة الجهة العامة المنفذ عليها' });
    expect(select).toBeInTheDocument();
  });

  it('يُفلتر بفرع الجهة العامة عند الاختيار من عمود «فرع الجهة العامة»', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({
          data: {
            applicants: [],
            courts: [],
            lawyers: [],
            administrativeBranches: [],
            branches: [],
            executedEntities: ['المصرف العقاري'],
            publicEntityBranches: ['فرع المزة'],
          },
        });
      }
      return Promise.resolve({
        data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 1 })] },
      });
    });

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة فرع الجهة العامة' }));
    const menu = screen.getByRole('menu', { name: 'فلترة فرع الجهة العامة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'فرع المزة' }));

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    expect(params.get('publicEntityBranch')).toBe('فرع المزة');
  });

  it('يعرض فلتر فرع الجهة العامة في شريط الفلاتر على الموبايل', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({
          data: {
            applicants: [],
            courts: [],
            lawyers: [],
            administrativeBranches: [],
            branches: [],
            executedEntities: [],
            publicEntityBranches: ['فرع حلب'],
          },
        });
      }
      return Promise.resolve({
        data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 1 })] },
      });
    });

    renderList();

    const select = await screen.findByRole('combobox', { name: 'فلترة فرع الجهة العامة' });
    expect(select).toBeInTheDocument();
  });

  it('يعرض عمود «فرع الجهة العامة» للمحامي ويخفي عمود وفلتر «فرع الإدارة» عنه', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    expect(within(table).getByRole('button', { name: 'فلترة فرع الجهة العامة' })).toBeInTheDocument();
    expect(within(table).queryByText('فرع الإدارة')).not.toBeInTheDocument();
    expect(within(table).queryByRole('button', { name: 'فلترة فرع الإدارة' })).not.toBeInTheDocument();
  });

  it('يعرض زر «تصدير إكسل» للمدير وينزّل الملف بفلاتر الحالية', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ hasFullAccess: true, isHead: false, user: { role: 'manager' } });
    mockPageWithExport([makeDocument({ id: 1 })]);

    URL.createObjectURL = vi.fn(() => 'blob:fake');
    URL.revokeObjectURL = vi.fn();
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    renderList();
    const table = await screen.findByRole('table');

    const statusButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    await user.click(statusButton);
    const menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'متداول' }));

    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    const moreMenu = screen.getByRole('menu', { name: 'المزيد' });
    const exportItem = within(moreMenu).getByRole('menuitem', { name: 'تصدير إكسل' });
    expect(exportItem).toBeEnabled();
    expect(screen.queryByText('طبّق فلترًا واحدًا على الأقل قبل التصدير')).not.toBeInTheDocument();
    await user.click(exportItem);

    expectExportRequestedWithStatus();

    clickSpy.mockRestore();
    vi.unstubAllGlobals();
  });

  it('يعرض رسالة ولا يرسل طلب تصدير عند النقر دون تطبيق أي فلتر', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 1 })]);

    renderList();
    await screen.findByRole('table');

    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    const exportItem = within(screen.getByRole('menu', { name: 'المزيد' })).getByRole('menuitem', {
      name: 'تصدير إكسل',
    });
    expect(exportItem).toBeEnabled();

    await user.click(exportItem);

    expect(screen.getByRole('alert')).toHaveTextContent('طبّق فلترًا واحدًا على الأقل قبل التصدير');
    expect(api.get).not.toHaveBeenCalledWith('/documents/export', expect.anything());
  });

  it('يُصدر بعد تطبيق فلتر ويعيد الرسالة بعد إلغائه', async () => {
    const user = userEvent.setup();
    mockPageWithExport([makeDocument({ id: 1 })]);

    URL.createObjectURL = vi.fn(() => 'blob:fake');
    URL.revokeObjectURL = vi.fn();
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    renderList();
    const table = await screen.findByRole('table');

    const statusButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    await user.click(statusButton);
    let menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'متداول' }));

    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    await user.click(
      within(screen.getByRole('menu', { name: 'المزيد' })).getByRole('menuitem', { name: 'تصدير إكسل' }),
    );

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expectExportRequestedWithStatus();

    await user.click(within(table).getByRole('button', { name: 'فلترة الحالة' }));
    menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'كل الحالات' }));

    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    await user.click(
      within(screen.getByRole('menu', { name: 'المزيد' })).getByRole('menuitem', { name: 'تصدير إكسل' }),
    );

    expect(screen.getByRole('alert')).toHaveTextContent('طبّق فلترًا واحدًا على الأقل قبل التصدير');
    const exportCalls = (api.get as unknown as ReturnType<typeof vi.fn>).mock.calls.filter(
      (call: unknown[]) => call[0] === '/documents/export',
    );
    expect(exportCalls).toHaveLength(1);

    clickSpy.mockRestore();
    vi.unstubAllGlobals();
  });

  it('يعرض الرسالة على الجوال عند النقر دون فلتر ويُصدر بعد اختيار فلتر الحالة', async () => {
    const user = userEvent.setup();
    stubMobile();
    mockPageWithExport([makeDocument({ id: 1 })]);

    renderList();
    await screen.findByRole('combobox', { name: 'فلترة الحالة' });

    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    await user.click(
      within(screen.getByRole('menu', { name: 'المزيد' })).getByRole('menuitem', { name: 'تصدير إكسل' }),
    );

    expect(screen.getByRole('alert')).toHaveTextContent('طبّق فلترًا واحدًا على الأقل قبل التصدير');
    expect(api.get).not.toHaveBeenCalledWith('/documents/export', expect.anything());

    await user.selectOptions(screen.getByRole('combobox', { name: 'فلترة الحالة' }), 'متداول');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    await user.click(
      within(screen.getByRole('menu', { name: 'المزيد' })).getByRole('menuitem', { name: 'تصدير إكسل' }),
    );

    expectExportRequestedWithStatus();

    vi.unstubAllGlobals();
  });

  it('يعرض زر «الملفات المحذوفة» للمحامي ورئيس القسم والمشرف ولا يعرضه للمدير', async () => {
    const user = userEvent.setup();
    mockPage([]);

    const roles = ['lawyer', 'head', 'admin'];
    for (const role of roles) {
      useAuthMock.mockReturnValue({
        hasFullAccess: role === 'admin',
        isHead: role === 'head',
        user: { role },
      });
      const { unmount } = renderList();
      await screen.findByRole('table');
      await user.click(screen.getByRole('button', { name: 'المزيد' }));
      const moreMenu = screen.getByRole('menu', { name: 'المزيد' });
      expect(within(moreMenu).getByRole('menuitem', { name: 'الملفات المحذوفة' })).toHaveAttribute(
        'href',
        '/documents/deleted',
      );
      unmount();
    }

    useAuthMock.mockReturnValue({ hasFullAccess: true, isHead: false, user: { role: 'manager' } });
    renderList();
    await screen.findByRole('table');
    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    expect(
      within(screen.getByRole('menu', { name: 'المزيد' })).queryByRole('menuitem', {
        name: 'الملفات المحذوفة',
      }),
    ).not.toBeInTheDocument();
  });

  it('يعرض زر «تدوير أرقام الأساس» للمحامي فقط', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    mockPage([]);

    renderList();
    await screen.findByRole('table');
    await user.click(screen.getByRole('button', { name: 'المزيد' }));

    expect(
      within(screen.getByRole('menu', { name: 'المزيد' })).getByRole('menuitem', {
        name: 'تدوير أرقام الأساس',
      }),
    ).toHaveAttribute('href', '/documents/rotate');
  });

  it('يخفي زر «تدوير أرقام الأساس» عن المدير ورئيس القسم والمشرف', async () => {
    const user = userEvent.setup();
    mockPage([]);

    const roles = ['manager', 'head', 'admin'];
    for (const role of roles) {
      useAuthMock.mockReturnValue({
        hasFullAccess: role !== 'head',
        isHead: role === 'head',
        user: { role },
      });
      const { unmount } = renderList();
      await screen.findByRole('table');
      await user.click(screen.getByRole('button', { name: 'المزيد' }));
      expect(
        within(screen.getByRole('menu', { name: 'المزيد' })).queryByRole('menuitem', {
          name: 'تدوير أرقام الأساس',
        }),
      ).not.toBeInTheDocument();
      unmount();
    }
  });

  it('يعرض زر «المزيد» ويجمع الإجراءات الأربعة للمحامي في قائمة منسدلة ويغلقها بمفتاح Escape', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    mockPage([]);

    renderList();
    await screen.findByRole('table');

    const moreBtn = screen.getByRole('button', { name: 'المزيد' });
    expect(screen.queryByRole('menu', { name: 'المزيد' })).not.toBeInTheDocument();

    await user.click(moreBtn);
    const menu = screen.getByRole('menu', { name: 'المزيد' });
    expect(within(menu).getByRole('menuitem', { name: 'الملفات المحذوفة' })).toHaveAttribute(
      'href',
      '/documents/deleted',
    );
    expect(within(menu).getByRole('menuitem', { name: 'الملفات المشطوبة' })).toHaveAttribute(
      'href',
      '/documents/struck-off',
    );
    expect(within(menu).getByRole('menuitem', { name: 'تدوير أرقام الأساس' })).toHaveAttribute(
      'href',
      '/documents/rotate',
    );
    expect(within(menu).getByRole('menuitem', { name: 'تصدير إكسل' })).toBeInTheDocument();

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('menu', { name: 'المزيد' })).not.toBeInTheDocument();
  });

  it('يغلق قائمة «المزيد» عند النقر خارجها دون تنفيذ أي إجراء', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    mockPage([]);

    renderList();
    await screen.findByRole('table');

    await user.click(screen.getByRole('button', { name: 'المزيد' }));
    expect(screen.getByRole('menu', { name: 'المزيد' })).toBeInTheDocument();

    await user.click(screen.getByText('الملفات التنفيذية'));
    expect(screen.queryByRole('menu', { name: 'المزيد' })).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalledWith('/documents/export', expect.anything());
  });

  it('يعرض رقم الملف بالأحمر في الجدول عندما يحتاج الملف التدوير', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 9, needsRotation: true })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    const fileNumber = within(table).getByText('99 حقوق');
    expect(fileNumber.className).toContain('text-red-600');
    expect(fileNumber.className).toContain('font-bold');
  });

  it('يعرض رقم الملف بالأحمر في بطاقة الموبايل عندما يحتاج الملف التدوير', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 9, needsRotation: true })],
      },
    });

    renderList();

    const fileNumber = await screen.findByText('99 حقوق');
    expect(fileNumber.className).toContain('text-red-600');
    expect(fileNumber.className).toContain('font-bold');
  });

  it('لا يلوّن رقم الملف بالأحمر عندما لا يحتاج الملف التدوير', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [makeDocument({ id: 9, needsRotation: false })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    const fileNumber = within(table).getByText('99 حقوق');
    expect(fileNumber.className).not.toContain('text-red-600');
  });

  it('يستعيد موضع القائمة المحفوظ (بحث وصفحة) عند العودة من صفحة ملف', async () => {
    sessionStorage.setItem(
      'documentsListPosition',
      JSON.stringify({
        query: 'محمود',
        status: 'منفذ',
        applicant: '',
        court: 'دمشق',
        lawyer: '',
        administrativeBranch: '',
        executedEntity: '',
        publicEntityBranch: '',
        page: 2,
      }),
    );
    mockPage([]);

    renderList();
    await screen.findByText('لا توجد نتائج');

    const called = vi.mocked(api.get).mock.calls.some(([url]) =>
      String(url).includes('/documents?') && String(url).includes('q=') && String(url).includes('page=2'));
    expect(called).toBe(true);
    expect(screen.getByPlaceholderText(/بحث بالاسم/)).toHaveValue('محمود');
  });

  it('يميّز الملف الذي كان مفتوحًا في الجدول بشارة وخلفية ويمرر إليه', async () => {
    sessionStorage.setItem('lastViewedDocumentId', '5');
    mockPage([makeDocument({ id: 5, borrowerName: 'محمود', borrowerFather: 'علي', borrowerFamily: 'حسن' })]);

    renderList();

    const table = await screen.findByRole('table');
    const row = table.querySelector('tr[id="doc-row-5"]');
    expect(row).not.toBeNull();
    expect(row!.className).toContain('bg-emerald-50');
    expect(within(table).getByText('آخر ملف تم فتحه')).toBeInTheDocument();
    expect(Element.prototype.scrollIntoView).toHaveBeenCalled();
  });

  it('يميّز بطاقة الجوال للملف الذي كان مفتوحًا', async () => {
    stubMobile();
    sessionStorage.setItem('lastViewedDocumentId', '5');
    mockPage([makeDocument({ id: 5, borrowerName: 'محمود', borrowerFather: 'علي', borrowerFamily: 'حسن' })]);

    renderList();

    const card = await screen.findByText('محمود علي حسن');
    const article = card.closest('article');
    expect(article).not.toBeNull();
    expect(article!.className).toContain('bg-emerald-50');
    expect(screen.getByText('آخر ملف تم فتحه')).toBeInTheDocument();
  });

  it('يعرض شريطًا احتياطيًا يربط بالملف عندما لا يكون الملف المفتوح ظاهرًا في الصفحة', async () => {
    sessionStorage.setItem('lastViewedDocumentId', '99');
    mockPage([makeDocument({ id: 1, borrowerName: 'أحمد', borrowerFamily: 'الخطيب' })]);
    (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
      if (url === '/documents/99') {
        return Promise.resolve({ data: makeDocument({ id: 99, borrowerName: 'منى', borrowerFather: 'سامر', borrowerFamily: 'نور' }) });
      }
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({ data: { applicants: [], courts: [], lawyers: [], administrativeBranches: [], branches: [], publicEntityBranches: [] } });
      }
      return Promise.resolve({ data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 1, borrowerName: 'أحمد', borrowerFamily: 'الخطيب' })] } });
    });

    renderList();

    expect(await screen.findByText(/كنت تعمل على ملف «منى سامر نور»/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'فتح الملف' })).toHaveAttribute('href', '/documents/99');
  });

  it('يحفظ موضع القائمة وآخر ملف مفتوح عند النقر على ملف', async () => {
    const user = userEvent.setup();
    mockPage([makeDocument({ id: 5, borrowerName: 'محمود', borrowerFather: 'علي', borrowerFamily: 'حسن' })]);

    renderList();

    const link = await screen.findByRole('link', { name: 'محمود علي حسن' });
    await user.click(link);

    expect(sessionStorage.getItem('lastViewedDocumentId')).toBe('5');
    expect(JSON.parse(sessionStorage.getItem('documentsListPosition') ?? '{}')).toMatchObject({ page: 1, query: '' });
  });

  it('يعرض حالة خطأ كاملة مع إعادة محاولة ناجحة عند فشل الجلب الأول', async () => {
    const user = userEvent.setup();
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    get.mockImplementation((url: string) => {
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({ data: { applicants: [], courts: [], lawyers: [], administrativeBranches: [], branches: [], publicEntityBranches: [] } });
      }
      return Promise.reject(new Error('انقطع الاتصال بالخادم'));
    });

    renderList();

    expect(await screen.findByText('انقطع الاتصال بالخادم')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();

    get.mockImplementation((url: string) => {
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({ data: { applicants: [], courts: [], lawyers: [], administrativeBranches: [], branches: [], publicEntityBranches: [] } });
      }
      return Promise.resolve({ data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 3 })] } });
    });
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));

    expect(await screen.findByRole('link', { name: 'أحمد خالد الخطيب' })).toBeInTheDocument();
    expect(screen.queryByText('انقطع الاتصال بالخادم')).not.toBeInTheDocument();
  });

  it('يُبقي الصفوف القديمة ويعرض تنبيه تحديث عند فشل تحديث خلفي، وتزيله إعادة محاولة ناجحة', async () => {
    const user = userEvent.setup();
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    let listShouldFail = false;
    get.mockImplementation((url: string) => {
      if (url.startsWith('/documents/filter-options')) {
        return Promise.resolve({ data: { applicants: [], courts: [], lawyers: [], administrativeBranches: [], branches: [], publicEntityBranches: [] } });
      }
      if (listShouldFail) return Promise.reject(new Error('فشل التحديث'));
      return Promise.resolve({
        data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [makeDocument({ id: 9 })] },
      });
    });

    renderList();

    await screen.findByRole('link', { name: 'أحمد خالد الخطيب' });

    listShouldFail = true;
    await user.type(screen.getByPlaceholderText('بحث بالاسم الثنائي أو الثلاثي لأحد المنفذ عليهم أو ورثة المتوفى، رقم العقد، دائرة التنفيذ...'), 'س');

    expect(await screen.findByText('تعذر تحديث القائمة — تُعرض بيانات سابقة.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'أحمد خالد الخطيب' })).toBeInTheDocument();

    listShouldFail = false;
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));

    await waitFor(() =>
      expect(screen.queryByText('تعذر تحديث القائمة — تُعرض بيانات سابقة.')).not.toBeInTheDocument(),
    );
    expect(screen.getByRole('link', { name: 'أحمد خالد الخطيب' })).toBeInTheDocument();
  });
});
