import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentsList from './DocumentsList';
import type { DocumentResponse, PagedResult } from '../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn() },
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
    guarantors: [],
    realEstates: [],
    executionActions: [],
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
    ...overrides,
  };
}

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
        },
      });
    }
    return Promise.resolve({
      data: { page: 1, perPage: 20, totalCount: items.length, totalPages: 1, items },
    });
  });
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
  stubDesktop();
  useAuthMock.mockReturnValue({
    hasFullAccess: true,
    isHead: false,
    user: { role: 'manager' },
  });
});

describe('DocumentsList', () => {
  it('يعرض الأعمدة الجديدة بالترتيب المعتمد: فرع الإدارة، الحالة، طالب التنفيذ، الفرع، المنفذ عليه، دائرة التنفيذ، رقم الملف، المحامي المختص، الإجراءات والملاحظات، عدد المشاهدات', async () => {
    const page: PagedResult<DocumentResponse> = {
      page: 1,
      perPage: 20,
      totalCount: 1,
      totalPages: 1,
      items: [
        doc({ id: 1, isDraft: true, execStatus: '', documentType: 'تحت رفع - س' }),
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
      'الفرع',
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

  it('يعرض الحالة بأحد أشكالها الأربعة دون «بدون حالة»', async () => {
    const page: PagedResult<DocumentResponse> = {
      page: 1,
      perPage: 20,
      totalCount: 4,
      totalPages: 1,
      items: [
        doc({ id: 1, isDraft: true, execStatus: '', documentType: 'تحت رفع - س' }),
        doc({ id: 2, isDraft: false, execStatus: '', documentType: 'متداول - ص' }),
        doc({ id: 3, isDraft: false, execStatus: 'منفذ بالتسوية', documentType: 'متداول - ق' }),
        doc({ id: 4, isDraft: true, execStatus: 'تريث', documentType: 'تحت رفع - ر' }),
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
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [doc({ id: 5 })] },
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
        items: [doc({ fileNumber: '99', fileType: 'حقوق', isDraft: false })],
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
          doc({ fileNumber: '99', displayFileNumber: '1500', fileType: 'حقوق', isDraft: false }),
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
        items: [doc({ fileNumber: '99', fileType: undefined, isDraft: false })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('99')).toBeInTheDocument();
  });

  it('يعرض عمود «الفرع» بين طالب التنفيذ والمنفذ عليه بقيمة الفرع', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [doc({ id: 1, branchName: 'فرع المزة' })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    const cells = within(table).getAllByRole('cell');
    expect(cells[1].textContent).toBe('المدعي');
    expect(cells[2].textContent).toBe('فرع المزة');
    expect(cells[3].textContent).toBe('أحمد خالد الخطيب');
  });

  it('يعرض فراغًا في رقم الملف عندما يكون الملف تحت رفع', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [doc({ fileNumber: undefined, fileYear: undefined, isDraft: true })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    const fileNumberCell = within(table).getAllByRole('cell')[5];
    expect(fileNumberCell.textContent).toBe('');
  });

  it('يعرض عمود «عدد المشاهدات» للمدير', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [doc({ viewCount: 7 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('عدد المشاهدات')).toBeInTheDocument();
    expect(within(table).getByText('7')).toBeInTheDocument();
  });

  it('يعرض عمود «عدد المشاهدات» لرئيس القسم', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: true });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [doc({ viewCount: 3 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).getByText('عدد المشاهدات')).toBeInTheDocument();
    expect(within(table).getByText('3')).toBeInTheDocument();
  });

  it('يخفي عمود «عدد المشاهدات» عن المحامي', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false });
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [doc({ viewCount: 7 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).queryByText('عدد المشاهدات')).not.toBeInTheDocument();
    expect(within(table).queryByText('7')).not.toBeInTheDocument();
  });

  it('يعرض فلاتر الحالة وطالب التنفيذ ودائرة التنفيذ بجانب أعمدة الجدول على المكتبي ويُفلتر عند الاختيار', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 1 })]);

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
    await user.click(within(menu).getByRole('menuitem', { name: 'منفذ' }));
    [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    expect(url).toContain('status=' + encodeURIComponent('منفذ'));
  });

  it('يُلغي «عرض الكل» الفلتر النشط ويغلق قائمة العمود ويميز السهم بلون الفلتر النشط', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 1 })]);

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
    mockPage([doc({ id: 1 })]);

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
    mockPage([doc({ id: 1 })]);

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
    mockPage([doc({ id: 1 })]);

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
    mockPage([doc({ id: 1 })]);

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
          doc({
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
        items: [doc({ id: 5, executionActions: [] })],
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
        items: [doc({ id: 5, viewCount: 7 })],
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

  it('يعرض الفرع في بطاقة الموبايل بين طالب التنفيذ ودائرة التنفيذ', async () => {
    stubMobile();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [doc({ id: 5, branchName: 'فرع المزة' })],
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
        items: [doc({ id: 1, lawyer: 'المحامي سامر', administrativeBranchName: 'الفرع الرئيسي - دمشق' })],
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
        items: [doc({ id: 1, lawyer: 'المحامي سامر', administrativeBranchName: 'الفرع الرئيسي - دمشق' })],
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
      data: { page: 1, perPage: 20, totalCount: 1, totalPages: 1, items: [doc({ id: 1 })] },
    });

    renderList();

    const table = await screen.findByRole('table');
    expect(within(table).queryByText('فرع الإدارة')).not.toBeInTheDocument();
    expect(within(table).queryByText('المحامي المختص')).not.toBeInTheDocument();
  });

  it('يُفلتر باسم المحامي عند الاختيار من عمود «المحامي المختص»', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 1 })]);

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
    mockPage([doc({ id: 1 })]);

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
        items: [doc({ id: 5, lawyer: 'المحامي سامر', administrativeBranchName: 'فرع الرقة' })],
      },
    });

    renderList();

    expect(await screen.findByRole('link', { name: 'أحمد خالد الخطيب' })).toBeInTheDocument();
    expect(screen.getByText(/المحامي المختص: المحامي سامر/)).toBeInTheDocument();
    expect(screen.getByText(/فرع الإدارة: فرع الرقة/)).toBeInTheDocument();
  });

  it('يُفلتر بالفرع عند الاختيار من عمود «الفرع»', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة الفرع' }));
    const menu = screen.getByRole('menu', { name: 'فلترة الفرع' });
    await user.click(within(menu).getByRole('menuitem', { name: 'فرع المزة' }));

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    expect(params.get('branch')).toBe('فرع المزة');
  });

  it('يُفلتر بفرع الإدارة عند الاختيار من عمود «فرع الإدارة» للمدير', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    await user.click(within(table).getByRole('button', { name: 'فلترة فرع الإدارة' }));
    const menu = screen.getByRole('menu', { name: 'فلترة فرع الإدارة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'الفرع الرئيسي - دمشق' }));

    const [url] = vi.mocked(api.get).mock.calls.at(-1) as [string];
    const params = new URLSearchParams(url.split('?')[1] ?? '');
    expect(params.get('administrativeBranch')).toBe('الفرع الرئيسي - دمشق');
  });

  it('يخفي فلتر «فرع الإدارة» عن المحامي', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    mockPage([doc({ id: 1 })]);

    renderList();
    const table = await screen.findByRole('table');

    expect(within(table).getByRole('button', { name: 'فلترة الفرع' })).toBeInTheDocument();
    expect(within(table).queryByRole('button', { name: 'فلترة فرع الإدارة' })).not.toBeInTheDocument();
  });

  it('يعرض زر «تصدير إكسل» للمدير وينزّل الملف بفلاتر الحالية', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ hasFullAccess: true, isHead: false, user: { role: 'manager' } });
    mockPage([doc({ id: 1 })]);

    const fetchMock = vi
      .fn()
      .mockResolvedValue({ ok: true, blob: () => Promise.resolve(new Blob(['xlsx'])) });
    vi.stubGlobal('fetch', fetchMock);
    URL.createObjectURL = vi.fn(() => 'blob:fake');
    URL.revokeObjectURL = vi.fn();
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    renderList();
    const table = await screen.findByRole('table');

    const statusButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    await user.click(statusButton);
    const menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'منفذ' }));

    const exportBtn = screen.getByRole('button', { name: 'تصدير إكسل' });
    expect(exportBtn).toBeEnabled();
    expect(screen.queryByText('طبّق فلترًا واحدًا على الأقل قبل التصدير')).not.toBeInTheDocument();
    await user.click(exportBtn);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/documents/export?status=' + encodeURIComponent('منفذ'),
      expect.objectContaining({ headers: expect.any(Object) }),
    );

    clickSpy.mockRestore();
    vi.unstubAllGlobals();
  });

  it('يعرض رسالة ولا يرسل طلب تصدير عند النقر دون تطبيق أي فلتر', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 1 })]);

    const fetchMock = vi
      .fn()
      .mockResolvedValue({ ok: true, blob: () => Promise.resolve(new Blob(['xlsx'])) });
    vi.stubGlobal('fetch', fetchMock);

    renderList();
    await screen.findByRole('table');

    const exportBtn = screen.getByRole('button', { name: 'تصدير إكسل' });
    expect(exportBtn).toBeEnabled();

    await user.click(exportBtn);

    expect(screen.getByRole('alert')).toHaveTextContent('طبّق فلترًا واحدًا على الأقل قبل التصدير');
    expect(fetchMock).not.toHaveBeenCalled();

    vi.unstubAllGlobals();
  });

  it('يُصدر بعد تطبيق فلتر ويعيد الرسالة بعد إلغائه', async () => {
    const user = userEvent.setup();
    mockPage([doc({ id: 1 })]);

    const fetchMock = vi
      .fn()
      .mockResolvedValue({ ok: true, blob: () => Promise.resolve(new Blob(['xlsx'])) });
    vi.stubGlobal('fetch', fetchMock);
    URL.createObjectURL = vi.fn(() => 'blob:fake');
    URL.revokeObjectURL = vi.fn();
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    renderList();
    const table = await screen.findByRole('table');

    const statusButton = within(table).getByRole('button', { name: 'فلترة الحالة' });
    await user.click(statusButton);
    let menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'منفذ' }));

    const exportBtn = screen.getByRole('button', { name: 'تصدير إكسل' });
    await user.click(exportBtn);

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/documents/export?status=' + encodeURIComponent('منفذ'),
      expect.objectContaining({ headers: expect.any(Object) }),
    );

    await user.click(within(table).getByRole('button', { name: 'فلترة الحالة' }));
    menu = screen.getByRole('menu', { name: 'فلترة الحالة' });
    await user.click(within(menu).getByRole('menuitem', { name: 'كل الحالات' }));

    await user.click(exportBtn);

    expect(screen.getByRole('alert')).toHaveTextContent('طبّق فلترًا واحدًا على الأقل قبل التصدير');
    expect(fetchMock).toHaveBeenCalledTimes(1);

    clickSpy.mockRestore();
    vi.unstubAllGlobals();
  });

  it('يعرض الرسالة على الجوال عند النقر دون فلتر ويُصدر بعد اختيار فلتر الحالة', async () => {
    const user = userEvent.setup();
    stubMobile();
    mockPage([doc({ id: 1 })]);

    const fetchMock = vi
      .fn()
      .mockResolvedValue({ ok: true, blob: () => Promise.resolve(new Blob(['xlsx'])) });
    vi.stubGlobal('fetch', fetchMock);

    renderList();
    await screen.findByRole('combobox', { name: 'فلترة الحالة' });

    const exportBtn = screen.getByRole('button', { name: 'تصدير إكسل' });
    await user.click(exportBtn);

    expect(screen.getByRole('alert')).toHaveTextContent('طبّق فلترًا واحدًا على الأقل قبل التصدير');
    expect(fetchMock).not.toHaveBeenCalled();

    await user.selectOptions(screen.getByRole('combobox', { name: 'فلترة الحالة' }), 'منفذ');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await user.click(exportBtn);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/documents/export?status=' + encodeURIComponent('منفذ'),
      expect.objectContaining({ headers: expect.any(Object) }),
    );

    vi.unstubAllGlobals();
  });

  it('يعرض زر «الملفات المحذوفة» للمحامي ورئيس القسم والمشرف ولا يعرضه للمدير', async () => {
    mockPage([]);

    const roles = ['lawyer', 'head', 'admin'];
    for (const role of roles) {
      useAuthMock.mockReturnValue({
        hasFullAccess: role === 'admin',
        isHead: role === 'head',
        user: { role },
      });
      const { unmount } = renderList();
      const link = await screen.findByRole('link', { name: 'الملفات المحذوفة' });
      expect(link).toHaveAttribute('href', '/documents/deleted');
      unmount();
    }

    useAuthMock.mockReturnValue({ hasFullAccess: true, isHead: false, user: { role: 'manager' } });
    renderList();
    await screen.findByRole('table');
    expect(screen.queryByRole('link', { name: 'الملفات المحذوفة' })).not.toBeInTheDocument();
  });

  it('يعرض زر «تدوير أرقام الأساس» للمحامي فقط', async () => {
    useAuthMock.mockReturnValue({ hasFullAccess: false, isHead: false, user: { role: 'lawyer' } });
    mockPage([]);

    renderList();

    const link = await screen.findByRole('link', { name: 'تدوير أرقام الأساس' });
    expect(link).toHaveAttribute('href', '/documents/rotate');
  });

  it('يخفي زر «تدوير أرقام الأساس» عن المدير ورئيس القسم والمشرف', async () => {
    mockPage([]);

    const roles = ['manager', 'head', 'admin'];
    for (const role of roles) {
      useAuthMock.mockReturnValue({
        hasFullAccess: role !== 'head',
        isHead: role === 'head',
        user: { role },
      });
      const { unmount } = renderList();
      if (role === 'head') {
        await screen.findByRole('link', { name: 'الملفات المحذوفة' });
      } else {
        await screen.findByRole('table');
      }
      expect(screen.queryByRole('link', { name: 'تدوير أرقام الأساس' })).not.toBeInTheDocument();
      unmount();
    }
  });

  it('يعرض رقم الملف بالأحمر في الجدول عندما يحتاج الملف التدوير', async () => {
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        page: 1,
        perPage: 20,
        totalCount: 1,
        totalPages: 1,
        items: [doc({ id: 9, needsRotation: true })],
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
        items: [doc({ id: 9, needsRotation: true })],
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
        items: [doc({ id: 9, needsRotation: false })],
      },
    });

    renderList();

    const table = await screen.findByRole('table');
    const fileNumber = within(table).getByText('99 حقوق');
    expect(fileNumber.className).not.toContain('text-red-600');
  });
});
