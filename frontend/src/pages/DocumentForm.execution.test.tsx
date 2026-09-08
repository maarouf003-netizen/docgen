// ملف اختبار موزّع من DocumentForm.test.tsx (سابقًا ملفًا أحاديًا ضخمًا).
// vi.hoisted/vi.mock تُكرَّر عمدًا في كل ملف موزَّع — vitest يعزل الملفات وكذا تقلبات المحاكاة.
// كتلة الاستيراد كاملة إلزامية (لا تختصرها — أي نقص يكسر tsc/oxlint):
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, fireEvent, waitFor } from '@testing-library/react';
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

describe('DocumentForm · التنفيذ', () => {
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

  async function renderExecutedEdit(doc: Partial<DocumentResponse>) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { ...mockDoc, ...doc } });
    render(<DocumentForm />);
    return screen.findByText('📋 حالة الملف', {}, { timeout: 5000 });
  }

  async function selectExecutedSide(user: ReturnType<typeof userEvent.setup>) {
    render(<DocumentForm />);
    await user.click(screen.getByLabelText('الجهة العامة منفذ عليها'));
  }

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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.executedStatus).toBe('');
    expect(payload.renewalFileNumber).toBe('100');
    expect(payload.renewalFileType).toBe('حقوقي');
    expect(payload.renewalDate).toBe('1/8/2026');
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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
