import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentForm from './DocumentForm';
import { api } from '../api/client';
import type { DocumentResponse } from '../types';

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

vi.mock('../auth/useAuth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return {
    ...original,
    api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

describe('DocumentForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
    useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
  });

  const mockDoc: DocumentResponse = {
    id: 1,
    createdAt: '2026-07-31',
    updatedAt: '2026-07-31',
    isDraft: false,
    documentType: 'سند دين',
    borrowerName: 'أحمد',
    borrowerFather: 'محمد',
    borrowerFamily: 'الخطيب',
    amountNumeric: 0,
    amount2Numeric: 0,
    amount3Numeric: 0,
    inclusionAmountNumeric: 0,
    inclusionAmount2Numeric: 0,
    inclusionAmount3Numeric: 0,
    viewCount: 0,
    printCount: 0,
    guarantors: [],
    assets: [],
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
  };

  async function renderEdit(doc: DocumentResponse = mockDoc) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
    render(<DocumentForm />);
    return screen.findByText('📂 وقوعات الملف');
  }

  async function renderExecutedEdit(doc: Partial<DocumentResponse>) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { ...mockDoc, ...doc } });
    render(<DocumentForm />);
    return screen.findByText('📋 حالة الملف');
  }

  it('يتحمل استجابة تعديل ناقصة المصفوفات دون انهيار (تطبيع حد الثقة)', async () => {
    const stripped: DocumentResponse = { ...mockDoc };
    delete (stripped as Partial<DocumentResponse>).guarantors;
    delete (stripped as Partial<DocumentResponse>).assets;
    delete (stripped as Partial<DocumentResponse>).executionApplicants;
    delete (stripped as Partial<DocumentResponse>).executedPublicEntities;
    delete (stripped as Partial<DocumentResponse>).executedNaturalPersons;

    await renderEdit(stripped);

    expect(screen.getByDisplayValue('أحمد')).toBeInTheDocument();
    expect(screen.getByText('📂 وقوعات الملف')).toBeInTheDocument();
  });

  it('يربط صف جهة الطالب بقيد السجل عبر نافذة الاختيار ويملأ نصوصه (المرحلة 2)', async () => {
    const user = userEvent.setup();
    await renderEdit();

    // نافذة الاختيار تجلب نتائج البحث عند الفتح.
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        items: [
          {
            id: 11, groupId: 5, canonicalName: 'وزارة التعليم', entityType: 'ministry',
            governorate: 'دمشق', branchName: 'الفرع الرئيسي', citationFormula: 'add-to-job',
            status: 'final', isActive: true, createdAt: '2026-08-24', aliases: [],
          },
        ],
        page: 1, perPage: 50, totalCount: 1, totalPages: 1,
      },
    });

    await user.click(await screen.findByRole('button', { name: 'اختيار من السجل…' }));
    const dialog = screen.getByRole('dialog', { name: 'اختيار الجهة العامة' });
    await user.click(
      within(dialog).getByRole('button', { name: /^وزارة التعليم/ }),
    );

    const nameInput = screen.getByLabelText('اسم الجهة 1') as HTMLInputElement;
    expect(nameInput.value).toBe('وزارة التعليم');
    expect((screen.getByLabelText('فرع الجهة 1') as HTMLInputElement).value).toBe('الفرع الرئيسي');
    expect((screen.getByLabelText('المحافظة 1') as HTMLInputElement).value).toBe('دمشق');
    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'اختيار الجهة العامة' })).not.toBeInTheDocument();
  });

  it('يفكّ ربط السجل تلقائيًا عند التحرير اليدوي لنص الجهة', async () => {
    const user = userEvent.setup();
    await renderEdit({
      ...mockDoc,
      applicantPublicEntities: [
        { id: 3, name: 'وزارة التعليم', branch: 'الفرع الرئيسي', governorate: 'دمشق', registryId: 11 },
      ],
    } as DocumentResponse);

    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    await user.clear(screen.getByLabelText('اسم الجهة 1'));
    await user.type(screen.getByLabelText('اسم الجهة 1'), 'وزارة التعليم العالي');

    expect(screen.queryByText('مرتبطة بالسجل ✓')).not.toBeInTheDocument();
  });

  it('يربط طالب تنفيذ اعتباري بقيد السجل عبر نافذة الاختيار ويملأ اسمه ورقم ربطه', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executionApplicants: [{ id: 1, name: 'المؤسسة السورية للتجارة', nature: 'legal' }],
    });

    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        items: [
          {
            id: 22, groupId: 9, canonicalName: 'هيئة التجارة الموحدة', entityType: 'authority',
            governorate: 'دمشق', branchName: 'الفرع الرئيسي', citationFormula: 'add-to-position',
            status: 'final', isActive: true, createdAt: '2026-08-24', aliases: [],
          },
        ],
        page: 1, perPage: 50, totalCount: 1, totalPages: 1,
      },
    });

    const applicantCard = screen.getByText('طالب التنفيذ 1').closest('.rounded-xl') as HTMLElement;
    await user.click(within(applicantCard).getByRole('button', { name: 'اختيار من السجل…' }));
    const dialog = screen.getByRole('dialog', { name: 'اختيار الجهة العامة' });
    await user.click(within(dialog).getByRole('button', { name: /^هيئة التجارة الموحدة/ }));

    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    expect(screen.getByLabelText('الشخص الاعتباري')).toHaveValue('هيئة التجارة الموحدة');
    expect(screen.queryByRole('dialog', { name: 'اختيار الجهة العامة' })).not.toBeInTheDocument();
  });

  it('يفكّ ربط طالب التنفيذ الاعتباري عند التحرير اليدوي لاسمه', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executionApplicants: [{ id: 1, name: 'هيئة التجارة الموحدة', nature: 'legal', registryId: 22 }],
    });

    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    await user.clear(screen.getByLabelText('الشخص الاعتباري'));
    await user.type(screen.getByLabelText('الشخص الاعتباري'), 'المؤسسة السورية للتجارة');

    expect(screen.queryByText('مرتبطة بالسجل ✓')).not.toBeInTheDocument();
  });

  it('يحوّل تسمية حقل عنوان المقترض إلى «الوكيل القانوني» عند اختيار «وكيله القانوني»', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    expect(screen.getByLabelText('العنوان')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('نوع العنوان'), 'يمثله');

    expect(screen.getByLabelText('الوكيل القانوني')).toBeInTheDocument();
    expect(screen.queryByLabelText('العنوان')).not.toBeInTheDocument();
  });

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

    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.annexType).toBe('');
    expect(payload.annexNumber).toBe('');
    expect(payload.annexDate).toBe('');
  });

  it('يخفي «فرع الملف» من النموذج (يُحفظ تلقائيًا) ويعرض «فرع الجهة» مع بقية حقول المعلومات الأساسية', () => {
    render(<DocumentForm />);

    expect(screen.queryByLabelText('فرع الملف')).not.toBeInTheDocument();
    expect(screen.getByLabelText('فرع الجهة 1')).toBeInTheDocument();
    expect(screen.getByLabelText('فرع الجهة 1')).toHaveAttribute('placeholder', 'فرع الجهة');
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

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.branchName).toBe('الفرع الرئيسي - دمشق');
  });

  it('يملأ «المحافظة» للجهة الطالبة تلقائيًا من فرع المحامي ويبقى قابلاً للتعديل ويُرسل عند الحفظ', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', branchName: 'الفرع الرئيسي - دمشق' } });
    render(<DocumentForm />);

    const governorate = screen.getByLabelText('المحافظة 1');
    expect(governorate).toHaveValue('دمشق');

    await user.clear(governorate);
    await user.type(governorate, 'حلب');
    await user.type(screen.getByLabelText('اسم الجهة 1'), 'المصرف التجاري السوري');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.applicantPublicEntities).toEqual([
      expect.objectContaining({ name: 'المصرف التجاري السوري', branch: '', governorate: 'حلب' }),
    ]);
  });

  it('يملأ «المحافظة» تلقائيًا عند إضافة جهة جديدة طالبة للتنفيذ', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', branchName: 'فرع حلب' } });
    render(<DocumentForm />);

    await user.click(screen.getByRole('button', { name: /إضافة جهة/ }));
    expect(screen.getByLabelText('المحافظة 1')).toHaveValue('حلب');
    expect(screen.getByLabelText('المحافظة 2')).toHaveValue('حلب');
  });

  it('يملأ «المحافظة» للجهة العامة المنفذ عليها تلقائيًا من فرع المحامي', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', branchName: 'الفرع الرئيسي - دمشق' } });
    render(<DocumentForm />);
    await user.click(screen.getByLabelText('الجهة العامة منفذ عليها'));

    const card = screen.getByText('جهة عامة 1').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByDisplayValue('دمشق')).toBeInTheDocument();
  });

  it('يملأ «المحافظة» للشخص الاعتباري المنفذ عليه تلقائيًا من فرع المحامي', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', branchName: 'فرع حلب' } });
    render(<DocumentForm />);
    await user.click(screen.getByLabelText('الجهة العامة منفذ عليها'));
    await user.click(screen.getByRole('button', { name: '＋ إضافة شخص اعتباري' }));

    const card = screen.getByText('شخص اعتباري 2').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByDisplayValue('حلب')).toBeInTheDocument();
  });

  it('يرسل تاريخ قيد الملف مع بيانات الملف عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('تاريخ قيد الملف'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.fileRegistrationDate).toBe('1/8/2026');
  });

  it('يمنع الحفظ عند إدخال رقم وسنة الملف دون تاريخ قيد', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('رقم الملف'), '520');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    expect(screen.getByText('تاريخ قيد الملف مطلوب عند إدخال رقم الملف وسنة الملف')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('يسمح بالحفظ عند إدخال رقم وسنة الملف مع تاريخ قيد', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('رقم الملف'), '520');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');
    await user.type(screen.getByLabelText('تاريخ قيد الملف'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    expect(api.post).toHaveBeenCalledTimes(1);
    expect(navigateMock).toHaveBeenCalledWith('/documents');
  });

  it('يرسل تاريخ إلقاء حجز المنظومة مع بيانات الملف عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('تاريخ إلقاء حجز المنظومة'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.seizureDate).toBe('1/8/2026');
  });

  it('يعرض عنوان «تعديل ملف» بالاسم الثلاثي عند التعديل', async () => {
    await renderEdit();

    expect(screen.getByText('تعديل ملف «أحمد محمد الخطيب»')).toBeInTheDocument();
  });

  it('يعرض حقل «المتضمن» كحقل نصي يتوسع تلقائياً', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.selectOptions(screen.getByLabelText('نوع السند'), 'عادي');

    const inclusion = screen.getByLabelText('المتضمن');
    expect(inclusion.tagName).toBe('TEXTAREA');
    await user.type(inclusion, 'خلاصة القرار للمتضمن');
    expect(inclusion).toHaveValue('خلاصة القرار للمتضمن');
  });

  it('يطبّق منطق «عادي» الخاص بتطبيق سطح المكتب', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.selectOptions(screen.getByLabelText('نوع السند'), 'عادي');

    expect(screen.getByText('المحكمة مصدرة القرار')).toBeInTheDocument();
    expect(screen.getByText('رقم القرار')).toBeInTheDocument();
    expect(screen.getByText('تاريخ القرار')).toBeInTheDocument();
    expect(screen.queryByText('نوع العقد')).not.toBeInTheDocument();

    expect(screen.getByText('المتضمن')).toBeInTheDocument();
    expect(screen.queryByText('المبلغ المطالب به')).not.toBeInTheDocument();
    expect(screen.queryByText(/المبلغ كتابة/)).not.toBeInTheDocument();

    expect(screen.getByText('👤 بيانات المنفذ عليه')).toBeInTheDocument();
    expect(screen.getByText('👥 المنفذ عليهم الآخرون')).toBeInTheDocument();
    expect(screen.getByText('منفذ عليه 2')).toBeInTheDocument();
    expect(screen.getByText('➕ إضافة منفذ عليه (شخص طبيعي)')).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('نوع السند'), 'مصرفي');

    expect(screen.getByText('المبلغ المطالب به')).toBeInTheDocument();
    expect(screen.queryByText('المتضمن')).not.toBeInTheDocument();
    expect(screen.getByText('👤 بيانات المقترض')).toBeInTheDocument();
    expect(screen.getByText('كفيل 1')).toBeInTheDocument();
  });

  it('يرسل المبالغ رقماً فقط دون حقول «المبلغ كتابة»', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'مقترض تجربة');
    await user.type(screen.getByLabelText('المبلغ المطالب به'), '1500');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    expect(vi.mocked(api.post)).toHaveBeenCalledTimes(1);
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerName).toBe('مقترض تجربة');
    expect(payload.amountNumeric).toBe(1500);
    expect(payload).not.toHaveProperty('amountWords');
    expect(navigateMock).toHaveBeenCalledWith('/documents');
  });

  it('يحدّ ترقيم الكفلاء إلى 4', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    for (let i = 1; i < 4; i++) {
      await user.click(screen.getByText('➕ إضافة كفيل (شخص طبيعي)'));
    }

    const buttons = screen.getAllByRole('button', { name: /🛑 الحد الأقصى/ });
    expect(buttons.length).toBeGreaterThan(0);
    buttons.forEach((b) => expect(b).toBeDisabled());
  });

  it('يعرض زر «إعادة تعيين» في الإدخال الجديد فقط', async () => {
    const { unmount } = render(<DocumentForm />);
    expect(screen.getByRole('button', { name: /إعادة تعيين/ })).toBeInTheDocument();
    unmount();

    await renderEdit();
    expect(screen.queryByRole('button', { name: /إعادة تعيين/ })).not.toBeInTheDocument();
  });

  it('يرسل الإجراءات المطلوب إضافتها كإجراء بتاريخ اليوم في initialActions عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الإجراءات المطلوب إضافتها إلى الإخطار التنفيذي'), 'تم إشعار المنفذ عليه');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toEqual([
      { type: 'action', text: 'تم إشعار المنفذ عليه', actionDate: new Date().toISOString().slice(0, 10) },
    ]);
  });

  it('يرسل الملاحظات كملاحظة في initialActions عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الملاحظات'), 'ملاحظة افتتاحية');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toEqual([{ type: 'note', text: 'ملاحظة افتتاحية' }]);
  });

  it('لا يرسل initialActions عندما يكون الحقلان فارغين', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toBeUndefined();
  });

  it('يرسل initialActions في PUT عند تعديل ملف مع إضافة إجراءات', async () => {
    const user = userEvent.setup();
    await renderEdit();

    await user.type(screen.getByLabelText('الإجراءات المطلوب إضافتها إلى الإخطار التنفيذي'), 'متابعة مع المحكمة');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));

    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toEqual([
      { type: 'action', text: 'متابعة مع المحكمة', actionDate: new Date().toISOString().slice(0, 10) },
    ]);
  });

  it('يخفي حقل الملاحظات في التعديل ويبقيه في الإدخال الجديد فقط', async () => {
    await renderEdit({ ...mockDoc, notes: 'ملاحظة محفوظة سابقًا' });

    expect(screen.queryByLabelText('الملاحظات')).not.toBeInTheDocument();
  });

  it('لا يعيد زرع ملاحظات الملف المحفوظة كملاحظة عند التعديل', async () => {
    const user = userEvent.setup();
    await renderEdit({ ...mockDoc, notes: 'ملاحظة محفوظة سابقًا' });

    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));

    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toBeUndefined();
  });

  it('يعرض زر «إضافة وريث» للمقترض ويُرسل ورثته عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    const heirName = screen.getByLabelText('اسم الوريث');
    await user.type(heirName, 'محمود');
    await user.type(screen.getByLabelText('اسم أب الوريث'), 'خالد');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    await user.type(
      Array.from(heirRow.querySelectorAll('input')).find(
        (el) => el.previousElementSibling?.textContent === 'النسبة',
      ) as HTMLInputElement,
      'الحلبي',
    );
    const heirAddress = Array.from(heirRow.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'العنوان',
    ) as HTMLInputElement;
    await user.type(heirAddress, 'المزة');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerHeirs).toEqual([
      { name: 'محمود', father: 'خالد', family: 'الحلبي', capacity: 'أصالة', addressType: 'عنوان', address: 'المزة' },
    ]);
  });

  it('يرسل ورثة الكفيل مع بيانات الكفيل عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const gName = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'الاسم',
    ) as HTMLInputElement;
    await user.type(gName, 'سمير');

    await user.click(within(card).getByRole('button', { name: '＋ إضافة وريث' }));
    await user.type(within(card).getByLabelText('اسم الوريث'), 'فارس');
    await user.type(within(card).getByLabelText('النسبة'), 'الخالد');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const guarantors = payload.guarantors as { heirs: unknown[] }[];
    expect(guarantors[0].heirs).toEqual([
      { name: 'فارس', father: '', family: 'الخالد', capacity: 'أصالة', addressType: 'عنوان', address: '' },
    ]);
  });

  it('يتجاهل ورثة بلا اسم ثلاثي عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerHeirs).toEqual([]);
  });

  it('يبدّل تسمية حقل الوريث بين «العنوان» و«الوكيل القانوني»', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    const heirName = screen.getByLabelText('اسم الوريث');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    const typeSelect = within(heirRow).getByLabelText('نوع العنوان') as HTMLSelectElement;

    expect(Array.from(heirRow.querySelectorAll('input')).some((el) => el.previousElementSibling?.textContent === 'العنوان')).toBe(true);

    await user.selectOptions(typeSelect, 'وكيل');

    expect(Array.from(heirRow.querySelectorAll('input')).some((el) => el.previousElementSibling?.textContent === 'الوكيل القانوني')).toBe(true);
  });

  it('يعرض ورثة المقترض والكفيل المحفوظة عند التعديل', async () => {
    const docWithHeirs: DocumentResponse = {
      ...mockDoc,
      borrowerHeirs: [{ id: 10, name: 'محمود الحلبي', addressType: 'عنوان', address: 'المزة' }],
      guarantors: [
        {
          id: 5,
          guarantorNumber: 1,
          name: 'سمير',
          father: 'حسن',
          family: 'علي',
          address: 'حلب',
          addressType: 'موطن مختار',
          heirs: [{ id: 11, name: 'فارس الخالد', addressType: 'وكيل', address: 'المحامي سامر' }],
        },
      ],
    };
    await renderEdit(docWithHeirs);

    expect(screen.getAllByLabelText('اسم الوريث')[0]).toHaveValue('محمود الحلبي');

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByLabelText('اسم الوريث')).toHaveValue('فارس الخالد');
    expect(within(card).getByLabelText('الوكيل القانوني')).toHaveValue('المحامي سامر');
  });

  it('يعرض ورثة المقترض في قائمة مالكي العقار ويُرسل المختارين عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);
    const heirName = screen.getByLabelText('اسم الوريث');
    await user.type(heirName, 'محمود');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    await user.type(
      Array.from(heirRow.querySelectorAll('input')).find(
        (el) => el.previousElementSibling?.textContent === 'النسبة',
      ) as HTMLInputElement,
      'الحلبي',
    );

    await user.click(screen.getByRole('button', { name: /🏡 إضافة عقار/ }));

    const heirBox = screen.getByRole('checkbox', { name: 'محمود الحلبي' });
    const borrowerBox = screen.getByRole('checkbox', { name: 'أحمد الخطيب' });
    expect(heirBox).toBeInTheDocument();
    expect(borrowerBox).toBeInTheDocument();

    await user.click(heirBox);
    await user.click(borrowerBox);
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as { owners: string[] }[];
    expect(assets[0].owners).toEqual(['محمود الحلبي', 'أحمد الخطيب']);
  });

  it('يحافظ على مالك محفوظ سابقًا غير موجود ضمن الخيارات عند التعديل', async () => {
    const docWithOldOwner: DocumentResponse = {
      ...mockDoc,
      assets: [
        {
          id: 7,
          assetKind: 'عقار',
          owners: ['سمير حسن علي'],
          property: 'منزل',
          propertyNumber: '12',
          propertyDistrict: 'المزة',
          landRegistry: 'سجل 3',
          shareType: 'تمام العقار',
        },
      ],
    };
    await renderEdit(docWithOldOwner);

    expect(screen.getByRole('checkbox', { name: 'سمير حسن علي' })).toBeChecked();
  });

  it('يفرض «حصة سهمية» تلقائيًا عند اختيار أكثر من مالك ولا يرجعها عند النقص', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const gName = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'الاسم',
    ) as HTMLInputElement;
    await user.type(gName, 'سمير');
    const gFamily = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'النسبة',
    ) as HTMLInputElement;
    await user.type(gFamily, 'علي');

    await user.click(screen.getByRole('button', { name: /🏡 إضافة عقار/ }));

    const shareSelect = () => {
      const div = screen.getByText('مقدار الحصة').closest('div');
      return div?.querySelector('select') as HTMLSelectElement;
    };
    expect(shareSelect()).toHaveValue('تمام العقار');

    await user.click(screen.getByRole('checkbox', { name: 'أحمد الخطيب' }));
    expect(shareSelect()).toHaveValue('تمام العقار');

    await user.click(screen.getByRole('checkbox', { name: 'سمير علي' }));
    expect(shareSelect()).toHaveValue('حصة سهمية');

    await user.click(screen.getByRole('checkbox', { name: 'أحمد الخطيب' }));
    expect(shareSelect()).toHaveValue('حصة سهمية');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as { owners: string[]; shareType: string }[];
    expect(assets[0].owners).toEqual(['سمير علي']);
    expect(assets[0].shareType).toEqual('حصة سهمية');
  });

  it('يسمح بإدخال تاريخ تسجيل المتجر وترخيص المتجر غير المسجل كنص حر مع تطبيع الأرقام', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');

    await user.click(screen.getByRole('button', { name: /🏪 إضافة متجر/ }));
    const shopCard = screen.getByText('متجر 1').closest('.rounded-xl') as HTMLElement;
    const regDate = Array.from(shopCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'تاريخ التسجيل',
    ) as HTMLInputElement;
    expect(regDate).toHaveAttribute('placeholder', 'مثال: 1/8/2026');
    expect(regDate).toHaveAttribute('type', 'text');
    await user.type(regDate, '١/٨/٢٠٢٦');
    const regNumber = Array.from(shopCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'رقم السجل',
    ) as HTMLInputElement;
    await user.type(regNumber, '888');

    await user.click(screen.getByRole('button', { name: /🛒 إضافة متجر غير مسجل/ }));
    const unregCard = screen.getByText('متجر غير مسجل 2').closest('.rounded-xl') as HTMLElement;
    const licDate = Array.from(unregCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'تاريخ الترخيص',
    ) as HTMLInputElement;
    expect(licDate).toHaveAttribute('placeholder', 'مثال: 1/8/2026');
    expect(licDate).toHaveAttribute('type', 'text');
    await user.type(licDate, '15-1-2025');
    const licNumber = Array.from(unregCard.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'رقم الترخيص',
    ) as HTMLInputElement;
    await user.type(licNumber, '456');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const assets = payload.assets as { registrationDate?: string; licenseDate?: string }[];
    const shop = assets.find((a) => a.registrationDate);
    const unregistered = assets.find((a) => a.licenseDate);
    expect(shop?.registrationDate).toBe('1/8/2026');
    expect(unregistered?.licenseDate).toBe('15-1-2025');
  }, 12000);

  it('يصحّح حصة العقار إلى «حصة سهمية» عند التحميل لعقار بملاك متعددين', async () => {
    const docWithInvalidShare: DocumentResponse = {
      ...mockDoc,
      assets: [
        {
          id: 8,
          assetKind: 'عقار',
          owners: ['سمير حسن علي', 'أحمد محمد خالد'],
          property: 'منزل',
          propertyNumber: '12',
          propertyDistrict: 'المزة',
          landRegistry: 'سجل 3',
          shareType: 'تمام العقار',
        },
      ],
    };
    await renderEdit(docWithInvalidShare);

    const div = screen.getByText('مقدار الحصة').closest('div');
    expect(div?.querySelector('select')).toHaveValue('حصة سهمية');
  });

  it('لا يعرض زر حذف الملف في الإدخال الجديد', () => {
    render(<DocumentForm />);

    expect(screen.queryByRole('button', { name: /حذف الملف/ })).not.toBeInTheDocument();
  });

  it('يعرض زر حذف الملف في التعديل للمحامي فقط', async () => {
    await renderEdit();
    expect(screen.getByRole('button', { name: /حذف الملف/ })).toBeInTheDocument();
  });

  it('يخفي زر حذف الملف في التعديل لغير المحامي', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'head' } });
    await renderEdit();

    expect(screen.queryByRole('button', { name: /حذف الملف/ })).not.toBeInTheDocument();
  });

  it('لا يحذف الملف عند رفض المستخدم التأكيد', async () => {
    const user = userEvent.setup();
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    await renderEdit();

    await user.click(screen.getByRole('button', { name: /حذف الملف/ }));

    expect(api.delete).not.toHaveBeenCalled();
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('يحذف الملف من نموذج التعديل بعد التأكيد ويعود إلى القائمة', async () => {
    const user = userEvent.setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await renderEdit();

    await user.click(screen.getByRole('button', { name: /حذف الملف/ }));

    expect(api.delete).toHaveBeenCalledWith('/documents/1');
    expect(navigateMock).toHaveBeenCalledWith('/documents');
  });

  it('يعرض «فشل الحذف» ويعيد تفعيل الزر عند خطأ في الحذف', async () => {
    const user = userEvent.setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('server error'));
    await renderEdit();

    await user.click(screen.getByRole('button', { name: /حذف الملف/ }));

    expect(await screen.findByText('فشل الحذف')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /حذف الملف/ })).toBeEnabled();
  });

  async function selectExecutedSide(user: ReturnType<typeof userEvent.setup>) {
    render(<DocumentForm />);
    await user.click(screen.getByLabelText('الجهة العامة منفذ عليها'));
  }

  it('يعرض حقول وضع «الجهة العامة منفذ عليها» عند اختيار صفته', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    expect(screen.getByText('📄 بيانات السند التنفيذي')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '➕ إضافة مبلغ' })).toBeInTheDocument();
    expect(screen.getByText('👤 طالب التنفيذ')).toBeInTheDocument();
    expect(screen.getByText('🏛️ المنفذ عليه')).toBeInTheDocument();
    expect(screen.getByText('📋 حالة الملف')).toBeInTheDocument();
    expect(screen.getByLabelText('رقم ورود الإخطار التنفيذي')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ ورود الاخطار')).toBeInTheDocument();
    expect(screen.getByLabelText('المحكمة مصدرة القرار')).toBeInTheDocument();

    expect(screen.queryByLabelText('تاريخ قيد الملف')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('رقم كتاب الجهة العامة')).not.toBeInTheDocument();
    expect(screen.queryByText('اكتب ما تم من اجراءات لإضافتها الى الإخطار التنفيذي')).not.toBeInTheDocument();
  });

  it('يعرض زر «إضافة طالب عرض» بلا «أل» في وضع «عرض وايداع» مع بقاء عنوان القسم «طالب العرض»', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);
    await user.click(screen.getByLabelText('عرض وايداع'));

    expect(screen.getByRole('button', { name: '＋ إضافة طالب عرض (شخص طبيعي)' })).toBeInTheDocument();
    expect(screen.getByText('👤 طالب العرض')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '＋ إضافة طالب التنفيذ (شخص طبيعي)' })).not.toBeInTheDocument();
  });

  it('يرسل بيانات وضع «منفذ عليه» (الملف والسند والمبالغ وحالة الملف) عند الحفظ', async () => {
    // اختبار تكاملي طويل (تعبئة كاملة + حفظ) يتجاوز مهلة الاختبار الافتراضية في بعض البيئات.
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.type(screen.getByLabelText('دائرة التنفيذ'), 'دمشق');
    await user.type(screen.getByLabelText('رقم الملف'), '55');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');
    await user.type(screen.getByLabelText('رقم ورود الإخطار التنفيذي'), '77');
    await user.type(screen.getByLabelText('تاريخ ورود الاخطار'), '1/8/2026');
    await user.type(screen.getByLabelText('المحكمة مصدرة القرار'), 'محكمة التنفيذ');
    await user.type(screen.getByLabelText('رقم القرار'), '101');
    fireEvent.change(screen.getByLabelText('تاريخ القرار'), { target: { value: '2026-07-15' } });
    await user.type(screen.getByLabelText('المتضمن'), 'خلاصة القرار');
    await user.click(screen.getByRole('button', { name: '➕ إضافة مبلغ' }));
    await user.type(screen.getByLabelText('المبلغ المطلوب دفعه من الجهة العامة'), '5000');
    const applicantCard = screen.getByText('طالب التنفيذ 1').closest('.rounded-xl') as HTMLElement;
    await user.type(applicantCard.querySelector('input') as HTMLInputElement, 'سليم');

    await user.selectOptions(screen.getByLabelText('الحالة'), 'منفذ');
    await user.type(screen.getByLabelText('كيفية تنفيذ الملف'), 'تم التحصيل');
    await user.type(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة'), '2000');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.fileReceiptNumber).toBe('77');
    expect(payload.fileReceiptDate).toBe('1/8/2026');
    expect(payload.contractType).toBe('محكمة التنفيذ');
    expect(payload.contractNumber).toBe('101');
    expect(payload.contractDate).toBe('2026-07-15');
    expect(payload.inclusionText).toBe('خلاصة القرار');
    expect(payload.executedRequiredAmount).toBe(5000);
    expect(payload.executedRequiredCurrency).toBe('ليرة سورية');
    expect(payload.executedRequiredAmount2).toBeUndefined();
    expect(payload.executedRequiredAmount3).toBeUndefined();
    expect(payload.executedStatus).toBe('منفذ');
    expect(payload.executedDescription).toBe('تم التحصيل');
    expect(payload.executedPaidAmount).toBe(2000);
    expect(payload.contractTypeSelector).toBe('عادي');
    expect(payload.guarantors).toEqual([]);
    expect(payload.borrowerHeirs).toEqual([]);
    expect(payload.assets).toEqual([]);
  }, 12000);

  it('يرسل حتى ثلاثة مبالغ مطلوب دفعها بعملاتها عند إضافة خانات جديدة', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.type(screen.getByLabelText('رقم الملف'), '55');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');

    await user.click(screen.getByRole('button', { name: '➕ إضافة مبلغ' }));
    await user.type(screen.getByLabelText('المبلغ المطلوب دفعه من الجهة العامة'), '5000');
    await user.selectOptions(screen.getByLabelText('العملة'), 'دولار أمريكي');

    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ المطلوب 2'), '3000');
    await user.selectOptions(screen.getAllByLabelText('العملة')[1], 'يورو');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.executedRequiredAmount).toBe(5000);
    expect(payload.executedRequiredCurrency).toBe('دولار أمريكي');
    expect(payload.executedRequiredAmount2).toBe(3000);
    expect(payload.executedRequiredCurrency2).toBe('يورو');
    expect(payload.executedRequiredAmount3).toBeUndefined();
  });

  it('لا يسمح بإضافة أكثر من ثلاثة مبالغ مطلوب دفعها', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.click(screen.getByRole('button', { name: '➕ إضافة مبلغ' }));
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));

    expect(screen.queryByRole('button', { name: '➕ مبلغ آخر' })).not.toBeInTheDocument();
    expect(screen.getByText('المبلغ المطلوب 3')).toBeInTheDocument();
  });

  it('يستثني عملة المبلغ الأول من خيارات عملتي المبلغين الثاني والثالث', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.click(screen.getByRole('button', { name: '➕ إضافة مبلغ' }));
    await user.selectOptions(screen.getByLabelText('العملة'), 'دولار أمريكي');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));

    const second = screen.getAllByLabelText('العملة')[1] as HTMLSelectElement;
    const third = screen.getAllByLabelText('العملة')[2] as HTMLSelectElement;
    expect([...second.options].map((o) => o.textContent)).toEqual(['ليرة سورية', 'يورو']);
    expect([...third.options].map((o) => o.textContent)).toEqual(['يورو']);
  });

  it('يفترض عملة غير مستعملة للخانة اللاحقة ويعيد ضبطها تلقائيًا عند تعارض العملات', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.click(screen.getByRole('button', { name: '➕ إضافة مبلغ' }));
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));

    expect(screen.getAllByLabelText('العملة')[1]).toHaveValue('دولار أمريكي');

    await user.selectOptions(screen.getAllByLabelText('العملة')[1], 'يورو');
    await user.selectOptions(screen.getAllByLabelText('العملة')[0], 'يورو');

    expect(screen.getAllByLabelText('العملة')[0]).toHaveValue('يورو');
    expect(screen.getAllByLabelText('العملة')[1]).toHaveValue('ليرة سورية');
  });

  it('يرسل حتى ثلاثة مبالغ مدفوعة بعملاتها عند إضافة خانات جديدة', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.type(screen.getByLabelText('رقم الملف'), '55');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');

    await user.selectOptions(screen.getByLabelText('الحالة'), 'منفذ');
    await user.type(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة'), '2000');
    await user.selectOptions(screen.getByLabelText('العملة'), 'دولار أمريكي');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة 2'), '3000');
    await user.selectOptions(screen.getAllByLabelText('العملة')[1], 'يورو');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.executedPaidAmount).toBe(2000);
    expect(payload.executedPaidCurrency).toBe('دولار أمريكي');
    expect(payload.executedPaidAmount2).toBe(3000);
    expect(payload.executedPaidCurrency2).toBe('يورو');
    expect(payload.executedPaidAmount3).toBeUndefined();
  });

  it('يعيد عرض المبالغ المدفوعة المحفوظة بعملاتها عند التعديل', async () => {
    await renderEdit({
      ...mockDoc,
      generalEntitySide: 'executed',
      executedStatus: 'منفذ',
      executedPaidAmount: 2000,
      executedPaidCurrency: 'دولار أمريكي',
      executedPaidAmount2: 3000,
      executedPaidCurrency2: 'يورو',
    });

    expect(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة')).toHaveValue(2000);
    expect(screen.getAllByLabelText('العملة')[0]).toHaveValue('دولار أمريكي');
    expect(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة 2')).toHaveValue(3000);
    expect(screen.getAllByLabelText('العملة')[1]).toHaveValue('يورو');
  });

  it('يعيد عرض المبالغ المطلوب دفعها المحفوظة بعملاتها عند التعديل', async () => {
    await renderEdit({
      ...mockDoc,
      generalEntitySide: 'executed',
      executedRequiredAmount: 5000,
      executedRequiredCurrency: 'دولار أمريكي',
      executedRequiredAmount2: 3000,
      executedRequiredCurrency2: 'يورو',
    });

    expect(screen.getByLabelText('المبلغ المطلوب دفعه من الجهة العامة')).toHaveValue(5000);
    expect(screen.getAllByLabelText('العملة')[0]).toHaveValue('دولار أمريكي');
    expect(screen.getByLabelText('المبلغ المطلوب 2')).toHaveValue(3000);
    expect(screen.getAllByLabelText('العملة')[1]).toHaveValue('يورو');
  });

  it('يرسل تاريخ الشطب عند اختيار «مشطوب» في حالة الملف', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.type(screen.getByLabelText('رقم الملف'), '55');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');

    await user.selectOptions(screen.getByLabelText('الحالة'), 'مشطوب');
    await user.type(screen.getByLabelText('تاريخ الشطب'), '4/8/2026');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.executedStatus).toBe('مشطوب');
    expect(payload.struckOffDate).toBe('4/8/2026');
  });

  it('يمنع حفظ ملف «الجهة العامة منفذ عليها» دون رقم وسنة الملف', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    expect(screen.getByText('ملف «الجهة العامة منفذ عليها» يجب أن يكون مقيدًا برقم وسنة الملف')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يعرض حقول التجديد عند تعديل ملف مشطوب واختيار «متداول»', async () => {
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executedStatus: 'مشطوب',
      struckOffDate: '2026-08-01',
      fileNumber: '55',
      fileYear: '2026',
    });

    expect(screen.queryByLabelText(/رقم الملف الجديد/)).not.toBeInTheDocument();

    await userEvent
      .setup()
      .selectOptions(screen.getByLabelText('الحالة'), 'متداول');

    expect(screen.getByLabelText(/رقم الملف الجديد/)).toBeInTheDocument();
    expect(screen.getByLabelText(/رقم ورود اخطار التجديد/)).toBeInTheDocument();
    expect(screen.getByLabelText(/تاريخ ورود اخطار التجديد/)).toBeInTheDocument();
    expect(screen.getByLabelText(/نوع الملف الجديد/)).toBeInTheDocument();
    expect(screen.getByLabelText(/تاريخ التجديد/)).toBeInTheDocument();
  });

  it('لا يعرض حقول التجديد عند تعديل ملف مشطوب والإبقاء على «مشطوب»', async () => {
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executedStatus: 'مشطوب',
      struckOffDate: '2026-08-01',
      fileNumber: '55',
      fileYear: '2026',
    });

    expect(screen.getByLabelText('تاريخ الشطب')).toBeInTheDocument();
    expect(screen.queryByLabelText(/رقم الملف الجديد/)).not.toBeInTheDocument();
  });

  it('يمنع الحفظ عند انتقال ملف مشطوب إلى متداول دون رقم الملف الجديد', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executedStatus: 'مشطوب',
      struckOffDate: '2026-08-01',
      fileNumber: '55',
      fileYear: '2026',
    });

    await user.selectOptions(screen.getByLabelText('الحالة'), 'متداول');
    await user.click(screen.getByRole('button', { name: /حفظ التعديلات/ }));

    expect(screen.getByText('رقم الملف الجديد مطلوب عند إعادة الملف المشطوب إلى المتداول')).toBeInTheDocument();
    expect(api.put).not.toHaveBeenCalled();
  });

  it('يرسل رقم الملف الجديد وبيانات التجديد عند إعادة ملف مشطوب إلى متداول', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executedStatus: 'مشطوب',
      struckOffDate: '2026-08-01',
      fileNumber: '55',
      fileYear: '2026',
    });

    await user.selectOptions(screen.getByLabelText('الحالة'), 'متداول');
    await user.type(screen.getByLabelText(/رقم الملف الجديد/), '100');
    await user.type(screen.getByLabelText(/نوع الملف الجديد/), 'حقوقي');
    await user.type(screen.getByLabelText(/تاريخ التجديد/), '1/8/2026');
    await user.click(screen.getByRole('button', { name: /حفظ التعديلات/ }));

    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.executedStatus).toBe('');
    expect(payload.renewalFileNumber).toBe('100');
    expect(payload.renewalFileType).toBe('حقوقي');
    expect(payload.renewalDate).toBe('1/8/2026');
  });

  it('يرسل حتى ثلاثة مبالغ مصرفية بعملاتها الافتراضية عند إضافة خانات جديدة', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('المبلغ المطالب به'), '1000');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ الثاني'), '2000');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ الثالث'), '3000');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.amountNumeric).toBe(1000);
    expect(payload.currency).toBe('ليرة سورية');
    expect(payload.amount2Numeric).toBe(2000);
    expect(payload.currency2).toBe('دولار أمريكي');
    expect(payload.amount3Numeric).toBe(3000);
    expect(payload.currency3).toBe('يورو');
  });

  it('يستثني عملتي المبلغين الأولين من خيارات عملة المبلغ الثالث المصرفي', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('المبلغ المطالب به'), '1000');
    await user.selectOptions(screen.getByLabelText('العملة'), 'يورو');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));

    const third = screen.getAllByLabelText('العملة')[2] as HTMLSelectElement;
    expect([...third.options].map((o) => o.textContent)).toEqual(['ليرة سورية']);
    expect(third).toHaveValue('ليرة سورية');
  });

  it('يعيد عرض المبالغ المصرفية الثلاثة المحفوظة بعملاتها عند التعديل', async () => {
    await renderEdit({
      ...mockDoc,
      amountNumeric: 1000,
      currency: 'ليرة سورية',
      amount2Numeric: 2000,
      currency2: 'دولار أمريكي',
      amount3Numeric: 3000,
      currency3: 'يورو',
    });

    expect(screen.getByLabelText('المبلغ المطالب به')).toHaveValue(1000);
    expect(screen.getAllByLabelText('العملة')[0]).toHaveValue('ليرة سورية');
    expect(screen.getByLabelText('المبلغ الثاني')).toHaveValue(2000);
    expect(screen.getAllByLabelText('العملة')[1]).toHaveValue('دولار أمريكي');
    expect(screen.getByLabelText('المبلغ الثالث')).toHaveValue(3000);
    expect(screen.getAllByLabelText('العملة')[2]).toHaveValue('يورو');
  });

  it('يرسل حتى ثلاثة مبالغ عادية (المتضمن) بعملاتها عند إضافة خانات جديدة', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.selectOptions(screen.getByLabelText('نوع السند'), 'عادي');
    await user.click(screen.getByRole('button', { name: '➕ إضافة مبلغ' }));
    await user.type(screen.getByLabelText('المبلغ'), '500');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ 2'), '600');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ 3'), '700');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.inclusionAmountNumeric).toBe(500);
    expect(payload.inclusionCurrency).toBe('ليرة سورية');
    expect(payload.inclusionAmount2Numeric).toBe(600);
    expect(payload.inclusionCurrency2).toBe('دولار أمريكي');
    expect(payload.inclusionAmount3Numeric).toBe(700);
    expect(payload.inclusionCurrency3).toBe('يورو');
  });

  it('يعيد عرض المبالغ العادية الثلاثة المحفوظة بعملاتها عند التعديل', async () => {
    await renderEdit({
      ...mockDoc,
      contractTypeSelector: 'عادي',
      inclusionAmountNumeric: 500,
      inclusionAmount2Numeric: 600,
      inclusionCurrency2: 'يورو',
      inclusionAmount3Numeric: 700,
      inclusionCurrency3: 'دولار أمريكي',
    });

    expect(screen.getByLabelText('المبلغ')).toHaveValue(500);
    expect(screen.getAllByLabelText('العملة')[0]).toHaveValue('ليرة سورية');
    expect(screen.getByLabelText('المبلغ 2')).toHaveValue(600);
    expect(screen.getAllByLabelText('العملة')[1]).toHaveValue('يورو');
    expect(screen.getByLabelText('المبلغ 3')).toHaveValue(700);
    expect(screen.getAllByLabelText('العملة')[2]).toHaveValue('دولار أمريكي');
  });

  it('يُظهر حقول «الممثل الشرعي» للمقترض ويخفي حقل عنوانه ويُرسل بياناته', async () => {
    const user = userEvent.setup();
    const { container } = render(<DocumentForm />);

    expect(container.querySelector('#borrowerAddress')).toBeTruthy();

    await user.click(screen.getAllByRole('button', { name: '＋ إضافة ممثل شرعي' })[0]);

    expect(container.querySelector('#borrowerAddress')).toBeNull();
    expect(screen.getByLabelText('اسم الممثل الشرعي')).toBeInTheDocument();
    expect(screen.getByLabelText('صفة الممثل الشرعي')).toBeInTheDocument();

    await user.type(screen.getByLabelText('اسم الممثل الشرعي'), 'الولي');
    await user.type(screen.getByLabelText('اسم أب الممثل الشرعي'), 'أب');
    await user.selectOptions(screen.getByLabelText('صفة الممثل الشرعي'), 'ولي');
    await user.selectOptions(screen.getByLabelText('نوع العنوان'), 'وكيل قانوني');
    await user.type(screen.getByLabelText('الوكيل القانوني'), 'المحامي سامر');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerRepresentativeName).toBe('الولي');
    expect(payload.borrowerRepresentativeFather).toBe('أب');
    expect(payload.borrowerRepresentativeCapacity).toBe('ولي');
    expect(payload.borrowerRepresentativeAddressType).toBe('وكيل قانوني');
    expect(payload.borrowerRepresentativeAddress).toBe('المحامي سامر');
    expect(payload.borrowerAddressType).toBe('');
    expect(payload.borrowerAddress).toBe('');
  });

  it('يخفي حقل عنوان المقترض عند إضافة وريث', async () => {
    const user = userEvent.setup();
    const { container } = render(<DocumentForm />);

    expect(container.querySelector('#borrowerAddress')).toBeTruthy();

    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    expect(container.querySelector('#borrowerAddress')).toBeNull();
  });

  it('يعرض قائمة «صفة الوريث» للوريث ويُرسل صفته عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);
    await user.type(screen.getByLabelText('اسم الوريث'), 'محمود');

    await user.selectOptions(screen.getByLabelText('صفة الوريث'), 'إضافة لتركة');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerHeirs).toEqual([
      { name: 'محمود', father: '', family: '', capacity: 'إضافة لتركة', addressType: 'عنوان', address: '' },
    ]);
  });

  it('يُرسل بيانات «الممثل الشرعي» للكفيل ويصفّر عنوان الكفيل', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    const gName = Array.from(card.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'الاسم',
    ) as HTMLInputElement;
    await user.type(gName, 'سمير');

    await user.click(within(card).getByRole('button', { name: '＋ إضافة ممثل شرعي' }));
    await user.type(within(card).getByLabelText('اسم الممثل الشرعي'), 'الوصي');
    await user.selectOptions(within(card).getByLabelText('صفة الممثل الشرعي'), 'وصي');
    await user.selectOptions(within(card).getByLabelText('نوع العنوان'), 'موطن مختار');
    await user.type(within(card).getByLabelText('الموطن المختار'), 'دمشق');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const guarantors = payload.guarantors as Record<string, unknown>[];
    expect(guarantors[0].representativeName).toBe('الوصي');
    expect(guarantors[0].representativeCapacity).toBe('وصي');
    expect(guarantors[0].representativeAddressType).toBe('موطن مختار');
    expect(guarantors[0].representativeAddress).toBe('دمشق');
    expect(guarantors[0].addressType).toBe('');
    expect(guarantors[0].address).toBe('');
  });

  it('يعرض حقول المورث عند «أصالة وإضافة» ويظهر ممثل شرعي عند الإضافة', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    const applicantCard = screen.getByText('طالب التنفيذ 1').closest('.rounded-xl') as HTMLElement;

    expect(within(applicantCard).queryByText('اسم المورث المتوفى')).not.toBeInTheDocument();

    await user.selectOptions(within(applicantCard).getAllByRole('combobox')[1], 'أصالة وإضافة');
    expect(within(applicantCard).getByText('اسم المورث المتوفى')).toBeInTheDocument();

    await user.click(within(applicantCard).getByRole('button', { name: '＋ إضافة ممثل شرعي' }));
    expect(within(applicantCard).getByLabelText('اسم الممثل الشرعي')).toBeInTheDocument();
    expect(within(applicantCard).getByLabelText('صفة الممثل الشرعي')).toBeInTheDocument();
    expect(within(applicantCard).getByLabelText('الوكيل القانوني')).toBeInTheDocument();
  });

  it('يُرسل بيانات «الممثل الشرعي» للشخص الطبيعي عند الحفظ', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.type(screen.getByLabelText('رقم الملف'), '55');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');

    await user.click(screen.getByRole('button', { name: '＋ إضافة شخص طبيعي' }));

    const personCard = screen.getByText('شخص طبيعي 1').closest('.rounded-xl') as HTMLElement;
    await user.type(personCard.querySelector('input') as HTMLInputElement, 'سامر');
    await user.click(within(personCard).getByRole('button', { name: '＋ إضافة ممثل شرعي' }));
    await user.type(within(personCard).getByLabelText('اسم الممثل الشرعي'), 'الولي');
    await user.selectOptions(within(personCard).getByLabelText('صفة الممثل الشرعي'), 'ولي');
    await user.selectOptions(within(personCard).getByLabelText('نوع العنوان'), 'عنوان');
    await user.type(within(personCard).getByLabelText('العنوان'), 'حلب');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const persons = payload.executedNaturalPersons as Record<string, unknown>[];
    expect(persons[0].name).toBe('سامر');
    expect(persons[0].representativeName).toBe('الولي');
    expect(persons[0].representativeCapacity).toBe('ولي');
    expect(persons[0].representativeAddressType).toBe('عنوان');
    expect(persons[0].representativeAddress).toBe('حلب');
  });

  it('يعرض محرر وقوعات الملف مع الوقوعات القائمة في تعديل ملف «منفذ عليه»', async () => {
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      fileNumber: '55',
      fileYear: '2026',
      occurrences: [
        {
          id: 1,
          occurrenceType: 'struck-off',
          occurrenceTypeLabel: 'شطب',
          eventDate: '2026-08-01',
          fileNumber: '55',
          year: 2026,
        },
        {
          id: 2,
          occurrenceType: 'renewal',
          occurrenceTypeLabel: 'تجديد',
          eventDate: '2026-09-01',
          fileNumber: '100',
          fileType: 'حقوقي',
          year: 2026,
          receiptNumber: 'و-9',
        },
      ],
    });

    expect(await screen.findByText('📂 وقوعات الملف')).toBeInTheDocument();
    expect(await screen.findByText(/تم شطب الملف بتاريخ/)).toBeInTheDocument();
    expect(await screen.findByText(/وجُدِّد الملف برقم 100/)).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: '+ إضافة وقعة' })).toBeInTheDocument();
  });

  it('يضيف وقعة شطب يدويًا عبر محرر وقوعات الملف ويحفظها فورًا', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      fileNumber: '55',
      fileYear: '2026',
      occurrences: [],
    });

    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        id: 5,
        occurrenceType: 'struck-off',
        occurrenceTypeLabel: 'شطب',
        eventDate: '5/8/2026',
        fileNumber: '55',
        year: 2026,
      },
    });

    await user.click(screen.getByRole('button', { name: '+ إضافة وقعة' }));
    await user.type(screen.getByLabelText('تاريخ الشطب'), '5/8/2026');
    await user.type(screen.getByLabelText('الرقم المشطوب'), '55');
    await user.click(screen.getByRole('button', { name: 'حفظ الوقعة' }));

    const [url, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(url).toBe('/documents/1/occurrences');
    expect(payload.occurrenceType).toBe('struck-off');
    expect(payload.eventDate).toBe('5/8/2026');
    expect(payload.fileNumber).toBe('55');

    expect(await screen.findByText(/تم شطب الملف بتاريخ/)).toBeInTheDocument();
  });

  it('يمنع إضافة وقعة تجديد دون رقم الملف الجديد', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      fileNumber: '55',
      fileYear: '2026',
      occurrences: [],
    });

    await user.click(screen.getByRole('button', { name: '+ إضافة وقعة' }));
    await user.selectOptions(screen.getByLabelText('نوع الوقعة'), 'renewal');
    await user.click(screen.getByRole('button', { name: 'حفظ الوقعة' }));

    expect(screen.getByText('رقم الملف الجديد مطلوب لوقعة التجديد')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يضيف وقعة «تريث» يدويًا لملف طالبة تنفيذ بحقولها في محرر الوقوعات', async () => {
    const user = userEvent.setup();
    await renderEdit();

    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        id: 6,
        occurrenceType: 'deferred',
        occurrenceTypeLabel: 'تريث',
        eventDate: '1/1/2024',
        details: { tarithNumber: '33', tarithDate: '1/1/2024' },
      },
    });

    await user.click(screen.getByRole('button', { name: '+ إضافة وقعة' }));
    await user.selectOptions(screen.getByLabelText('نوع الوقعة'), 'deferred');
    await user.type(screen.getByLabelText('رقم كتاب التريث'), '33');
    await user.type(screen.getByLabelText('تاريخ كتاب التريث'), '1/1/2024');
    await user.click(screen.getByRole('button', { name: 'حفظ الوقعة' }));

    const [url, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(url).toBe('/documents/1/occurrences');
    expect(payload.occurrenceType).toBe('deferred');
    expect(payload.details).toEqual({ tarithNumber: '33', tarithDate: '1/1/2024' });

    expect(await screen.findByText(/تريث بموجب كتاب التريث رقم 33/)).toBeInTheDocument();
  });

  it('يحذف وقعة من سجل وقوعات الملف بعد التأكيد', async () => {
    const user = userEvent.setup();
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      fileNumber: '55',
      fileYear: '2026',
      occurrences: [
        {
          id: 9,
          occurrenceType: 'struck-off',
          occurrenceTypeLabel: 'شطب',
          eventDate: '2026-08-01',
          fileNumber: '55',
        },
      ],
    });

    (api.delete as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    await user.click(await screen.findByRole('button', { name: 'حذف' }));
    expect(api.delete).toHaveBeenCalledWith('/documents/1/occurrences/9');
    expect(await screen.findByText('لا توجد وقوعات مسجلة لهذا الملف')).toBeInTheDocument();
    confirmSpy.mockRestore();
  });

  it('يبدّل المقترض إلى «شخص اعتباري» فيعرض حقوله ويخفي الهوية الطبيعية', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    // قائمة «نوع الطرف» للمقترض تظهر أولًا في DOM (قبل بطاقة الكفيل الأولى).
    await user.selectOptions(screen.getAllByLabelText('نوع الطرف')[0], 'legal');

    expect(screen.getByLabelText('الشخص الاعتباري')).toBeInTheDocument();
    expect(screen.getByLabelText('رقم تسجيله')).toBeInTheDocument();
    expect(screen.getByLabelText('يمثلها')).toBeInTheDocument();
    expect(screen.queryByLabelText('اسم الأب')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('الرقم الوطني')).not.toBeInTheDocument();
  });

  it('يضيف كفيلًا اعتباريًا عبر زر «إضافة كفيل (شخص اعتباري)» ويعرض حقوله', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getByRole('button', { name: '➕ إضافة كفيل (شخص اعتباري)' }));

    const card = screen.getByText('كفيل 2').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByText('الشخص الاعتباري')).toBeInTheDocument();
    expect(within(card).getByText('رقم تسجيله')).toBeInTheDocument();
    expect(within(card).getByText('يمثلها')).toBeInTheDocument();
  });

  it('يضيف شخصًا اعتباريًا في «المنفذ عليه» عبر زر الإضافة ويعرض حقوله', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.click(screen.getByRole('button', { name: '＋ إضافة شخص اعتباري' }));

    const card = screen.getByText('شخص اعتباري 2').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByText('الشخص الاعتباري')).toBeInTheDocument();
    expect(within(card).getByText('رقم تسجيله')).toBeInTheDocument();
    expect(within(card).getByText('يمثلها')).toBeInTheDocument();
  });
});
