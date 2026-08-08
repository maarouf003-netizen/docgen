import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Rotation from './Rotation';
import type { PagedResult, RotationDocumentDto } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), put: vi.fn() },
  getApiErrorMessage: (err: unknown) => (err as { message?: string })?.message ?? 'خطأ غير متوقع',
}));

import { api } from '../api/client';

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

function row(overrides: Partial<RotationDocumentDto>): RotationDocumentDto {
  return {
    documentId: 1,
    court: 'دمشق',
    borrowerName: 'أحمد',
    borrowerFather: 'خالد',
    borrowerFamily: 'الخطيب',
    fileNumber: '99',
    fileType: 'حقوق',
    baseNumber: undefined,
    ...overrides,
  };
}

function page(rows: RotationDocumentDto[], pageNo = 1, perPage = 20): PagedResult<RotationDocumentDto> {
  return {
    items: rows,
    page: pageNo,
    perPage,
    totalCount: rows.length,
    totalPages: Math.max(1, Math.ceil(rows.length / perPage)),
  };
}

const year = new Date().getFullYear();

beforeEach(() => {
  vi.clearAllMocks();
  stubDesktop();
  useAuthMock.mockReturnValue({
    hasFullAccess: false,
    isHead: false,
    user: { role: 'lawyer' },
  });
  (api.get as ReturnType<typeof vi.fn>).mockResolvedValue({
    data: page([
      row({}),
      row({ documentId: 2, court: 'حلب', borrowerName: 'سمير', borrowerFather: 'حسن', borrowerFamily: 'علي', fileNumber: '88', fileType: 'تنفيذ' }),
    ]),
  });
});

describe('Rotation', () => {
  it('يعرض جدول التدوير بأعمدته المطلوبة وسنة التدوير الحالية', async () => {
    render(<Rotation />);

    const table = await screen.findByRole('table');
    const headers = within(table).getAllByRole('columnheader').map((h) => h.textContent ?? '');
    expect(headers).toEqual(['الدائرة', 'الاسم الثلاثي', 'رقم الملف', 'نوعه', `رقم أساس ${year}`]);
    expect(screen.getByText(`سنة التدوير: ${year}`)).toBeInTheDocument();
    expect(within(table).getByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(within(table).getByText('99 حقوق')).toBeInTheDocument();
    expect(within(table).getByText('حقوق')).toBeInTheDocument();
    expect(within(table).getByText('دمشق')).toBeInTheDocument();
    // الملفات الظاهرة لا تملك رقمًا للسنة الحالية، فتبدأ الحقول فارغة.
    const input = within(table).getByRole('textbox', { name: 'رقم أساس أحمد خالد الخطيب' });
    expect(input).toHaveValue('');
  });

  it('يعرض بطاقات على الجوال بدلاً من الجدول', async () => {
    stubMobile();
    render(<Rotation />);

    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText(/رقم الملف: 99 حقوق/)).toBeInTheDocument();
    expect(screen.getByText(/نوعه: حقوق/)).toBeInTheDocument();
  });

  it('يحفظ التغييرات فقط عبر PUT /documents/rotate ويظهر رسالة النجاح ويعيد التحميل', async () => {
    const user = userEvent.setup();
    (api.put as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(<Rotation />);

    const input = await screen.findByRole('textbox', { name: 'رقم أساس أحمد خالد الخطيب' });
    await user.type(input, '600');

    await user.click(screen.getByRole('button', { name: 'حفظ أرقام الأساس' }));

    expect(api.put).toHaveBeenCalledWith('/documents/rotate', {
      entries: [{ documentId: 1, baseNumber: '600' }],
    });
    expect(await screen.findByText('تم حفظ أرقام الأساس بنجاح')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/documents/rotate', {
      params: { page: 1, perPage: 20 },
    });
  });

  it('يرسل الملفات المتغيّرة فقط ويستبعد الملفات التي لم تُعدّل', async () => {
    const user = userEvent.setup();
    (api.put as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    render(<Rotation />);

    const first = await screen.findByRole('textbox', { name: 'رقم أساس أحمد خالد الخطيب' });
    const second = screen.getByRole('textbox', { name: 'رقم أساس سمير حسن علي' });
    await user.type(first, '700');
    // الملف الثاني يبقى فارغًا دون تعديل → لا يُرسل.
    expect(second).toHaveValue('');

    await user.click(screen.getByRole('button', { name: 'حفظ أرقام الأساس' }));

    expect(api.put).toHaveBeenCalledWith('/documents/rotate', {
      entries: [{ documentId: 1, baseNumber: '700' }],
    });
  });

  it('يعطّل زر الحفظ عندما لا يوجد أي تغيير', async () => {
    render(<Rotation />);

    await screen.findByRole('table');
    expect(screen.getByRole('button', { name: 'حفظ أرقام الأساس' })).toBeDisabled();
  });

  it('يعرض رسالة خطأ من الخادم عند فشل الحفظ', async () => {
    const user = userEvent.setup();
    (api.put as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('الملف غير مؤهل للتدوير'),
    );
    render(<Rotation />);

    const input = await screen.findByRole('textbox', { name: 'رقم أساس أحمد خالد الخطيب' });
    await user.clear(input);
    await user.type(input, '600');

    await user.click(screen.getByRole('button', { name: 'حفظ أرقام الأساس' }));

    expect(await screen.findByText('الملف غير مؤهل للتدوير')).toBeInTheDocument();
  });

  it('يعرض رسالة عدم السماح لغير المحامي ولا يستدعي واجهة التدوير', async () => {
    useAuthMock.mockReturnValue({
      hasFullAccess: true,
      isHead: false,
      user: { role: 'manager' },
    });
    render(<Rotation />);

    expect(await screen.findByText('لا تملك صلاحية تنفيذ هذا الإجراء')).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });

  it('يعرض حالة فارغة عندما لا توجد ملفات مؤهلة', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: page([], 1, 20),
    });
    render(<Rotation />);

    expect(await screen.findByText('لا توجد ملفات مؤهلة للتدوير')).toBeInTheDocument();
  });

  it('يتنقل بين الصفحات عبر أزرار السابق/التالي ويجلب كل صفحة بمعاملات الترحيل', async () => {
    const user = userEvent.setup();
    (api.get as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({
        data: { ...page([row({ documentId: 1 })], 1, 1), totalCount: 2, totalPages: 2 },
      })
      .mockResolvedValueOnce({
        data: { ...page([row({ documentId: 2 })], 2, 1), totalCount: 2, totalPages: 2 },
      });

    render(<Rotation />);

    expect(await screen.findByText('أحمد خالد الخطيب')).toBeInTheDocument();
    expect(screen.getByText(/صفحة 1 من 2 \(2 نتيجة\)/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'التالي' }));

    expect(api.get).toHaveBeenLastCalledWith('/documents/rotate', {
      params: { page: 2, perPage: 20 },
    });
    expect(await screen.findByText(/صفحة 2 من 2 \(2 نتيجة\)/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'السابق' }));

    expect(api.get).toHaveBeenLastCalledWith('/documents/rotate', {
      params: { page: 1, perPage: 20 },
    });
  });
});
