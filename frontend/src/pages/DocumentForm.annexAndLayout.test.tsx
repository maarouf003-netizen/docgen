// ملف اختبار موزّع من DocumentForm.test.tsx (سابقًا ملفًا أحاديًا ضخمًا).
// vi.hoisted/vi.mock تُكرَّر عمدًا في كل ملف موزَّع — vitest يعزل الملفات وكذا تقلبات المحاكاة.
// كتلة الاستيراد كاملة إلزامية (لا تختصرها — أي نقص يكسر tsc/oxlint):
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentForm from './DocumentForm';
import { api } from '../api/client';
import type { DocumentResponse } from '../types';
import { mockDoc } from './test/documentFormFixtures'; // البيانات الخام المشتركة (§7.3)

const { navigateMock, paramsMock, useAuthMock } = vi.hoisted(() => ({
  navigateMock: vi.fn(),
  paramsMock: { id: undefined as string | undefined },
  useAuthMock: vi.fn(),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
  useParams: () => paramsMock,
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

vi.mock('../auth/useAuth', () => ({ useAuth: () => useAuthMock() }));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return { ...original, api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } };
});

describe('DocumentForm · ملحق العقد والتخطيط', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
    useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
  });

  async function renderEdit(doc: DocumentResponse = mockDoc) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
    render(<DocumentForm />);
    return screen.findByText('📂 وقوعات الملف', {}, { timeout: 5000 });
  }

  it('يوسّع حقل عنوان المقترض والكفيل عمودين ليستغلا الخلية الفارغة المجاورة', () => {
    const { container } = render(<DocumentForm />);

    const borrowerAddress = container.querySelector('#borrowerAddress') as HTMLElement;
    expect(borrowerAddress.closest('.md\\:col-span-2')).toBeTruthy();

    const guarantorCard = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const guarantorAddress = Array.from(guarantorCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'العنوان',
    ) as HTMLElement;
    expect(guarantorAddress.closest('.md\\:col-span-2')).toBeTruthy();
  });


  it('يحاذي زر «إضافة ممثل شرعي» إلى الجهة المقابلة (نهاية الصف) في قسمي المقترض والكفيل', () => {
    render(<DocumentForm />);

    const repButtons = screen.getAllByRole('button', { name: '＋ إضافة ممثل شرعي' });
    expect(repButtons.length).toBeGreaterThan(0);
    repButtons.forEach((btn) => {
      const wrapper = btn.parentElement as HTMLElement;
      expect(wrapper.className).toContain('justify-end');
    });
  });


  it('يعرض أسماء الحقول بخط عريض في الإدخال الجديد', () => {
    render(<DocumentForm />);

    ['الاسم', 'الرقم الوطني', 'نوع العنوان', 'العنوان', 'المبلغ المطالب به'].forEach((name) => {
      const labels = screen.getAllByText(name);
      expect(labels.length).toBeGreaterThan(0);
      labels.forEach((el) => expect(el.className).toContain('font-bold'));
    });
  });


  it('يعرض أسماء الحقول بخط عريض عند التعديل', async () => {
    await renderEdit();

    ['الاسم', 'الرقم الوطني', 'نوع العنوان', 'العنوان'].forEach((name) => {
      const labels = screen.getAllByText(name);
      expect(labels.length).toBeGreaterThan(0);
      labels.forEach((el) => expect(el.className).toContain('font-bold'));
    });
  });


  it('يضع «نوع العنوان» قبل «العنوان/الوكيل» في قسمي المقترض والكفيل', () => {
    render(<DocumentForm />);

    const typeSelect = screen.getByLabelText('نوع العنوان');
    const addressInput = screen.getByLabelText('العنوان');
    expect(typeSelect.compareDocumentPosition(addressInput) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const gTypeSelect = card.querySelector('select') as HTMLSelectElement;
    const gAddress = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'العنوان',
    ) as HTMLInputElement;
    expect(gTypeSelect.compareDocumentPosition(gAddress) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });


  it('يعرض افتراضيات «مصرفي» مثل تطبيق سطح المكتب', () => {
    render(<DocumentForm />);

    expect(screen.getByLabelText('نوع السند')).toHaveValue('مصرفي');
    expect(screen.getByText('المبلغ المطالب به')).toBeInTheDocument();
    expect(screen.getByText('نوع العقد')).toBeInTheDocument();
    expect(screen.getByText('رقم العقد')).toBeInTheDocument();
    expect(screen.getByText('تاريخ العقد')).toBeInTheDocument();
    expect(screen.getByText('👤 بيانات المقترض')).toBeInTheDocument();
    expect(screen.getByText('👥 الكفلاء')).toBeInTheDocument();
    expect(screen.getByText('كفيل 1')).toBeInTheDocument();
    expect(screen.getByText('➕ إضافة كفيل (شخص طبيعي)')).toBeInTheDocument();

    expect(screen.queryByText('المتضمن')).not.toBeInTheDocument();
    expect(screen.queryByText(/المبلغ كتابة/)).not.toBeInTheDocument();
  });


  it('يضيف ملحق العقد للمصرفي ويرسله مع بيانات السند عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getByRole('button', { name: 'إضافة ملحق' }));
    expect(screen.getByLabelText('نوع الملحق')).toBeInTheDocument();
    expect(screen.getByLabelText('رقم الملحق')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ الملحق')).toBeInTheDocument();

    await user.type(screen.getByLabelText('نوع الملحق'), 'تعديل');
    await user.type(screen.getByLabelText('رقم الملحق'), 'A-42');
    await user.type(screen.getByLabelText('تاريخ الملحق'), '15/3/2026');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.annexType).toBe('تعديل');
    expect(payload.annexNumber).toBe('A-42');
    expect(payload.annexDate).toBe('15/3/2026');
  });


  it('يخفي زر «إضافة ملحق» للعقد العادي', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.selectOptions(screen.getByLabelText('نوع السند'), 'عادي');
    expect(screen.queryByRole('button', { name: 'إضافة ملحق' })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('نوع الملحق')).not.toBeInTheDocument();
  });


  it('يعرض بيانات الملحق المحفوظة عند تعديل عقد مصرفي ويسمح بإزالتها', async () => {
    await renderEdit({
      ...mockDoc,
      contractTypeSelector: 'مصرفي',
      annexType: 'تعديل',
      annexNumber: 'A-42',
      annexDate: '15/3/2026',
    });

    expect(screen.getByLabelText('نوع الملحق')).toHaveValue('تعديل');
    expect(screen.getByLabelText('رقم الملحق')).toHaveValue('A-42');
    expect(screen.getByLabelText('تاريخ الملحق')).toHaveValue('15/3/2026');
    expect(screen.getByRole('button', { name: 'إزالة الملحق' })).toBeInTheDocument();
  });


  it('يزيل الملحق المحفوظ عند الضغط على «إزالة الملحق» ويُرسل قيمه فارغة عند الحفظ', async () => {
    const user = userEvent.setup();
    await renderEdit({
      ...mockDoc,
      contractTypeSelector: 'مصرفي',
      annexType: 'تعديل',
      annexNumber: 'A-42',
      annexDate: '15/3/2026',
    });

    await user.click(screen.getByRole('button', { name: 'إزالة الملحق' }));
    expect(screen.queryByLabelText('نوع الملحق')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('رقم الملحق')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('تاريخ الملحق')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إضافة ملحق' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /حفظ التعديلات/ }));

    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.annexType).toBe('');
    expect(payload.annexNumber).toBe('');
    expect(payload.annexDate).toBe('');
  });


  it('يخفي «فرع الملف» من النموذج (يُحفظ تلقائيًا) ويعرض «فرع الجهة» مع بقية حقول المعلومات الأساسية', () => {
    render(<DocumentForm />);

    expect(screen.queryByLabelText('فرع الملف')).not.toBeInTheDocument();
    expect(screen.getByLabelText('فرع الجهة 1')).toBeInTheDocument();
    expect(screen.getByLabelText('فرع الجهة 1')).toHaveAttribute('placeholder', 'الفرع');
    expect(screen.getByLabelText('رقم كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByLabelText('رقم تحت رفع')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ قيد الملف')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ إلقاء حجز المنظومة')).toBeInTheDocument();
  });


  it('يحفظ «فرع الملف» تلقائيًا من فرع المحامي دون إظهاره في النموذج', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', branchName: 'الفرع الرئيسي - دمشق' } });
    render(<DocumentForm />);

    expect(screen.queryByLabelText('فرع الملف')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.branchName).toBe('الفرع الرئيسي - دمشق');
  });

});
