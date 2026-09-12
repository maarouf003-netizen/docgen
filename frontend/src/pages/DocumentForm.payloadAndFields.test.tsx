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

describe('DocumentForm · الحقول والحمولات', () => {
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


  it('يملأ «المحافظة» للجهة الطالبة تلقائيًا من فرع المحامي كحقل مقفل لا يقبل تحريرًا يدويًا', async () => {
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', branchName: 'الفرع الرئيسي - دمشق' } });
    render(<DocumentForm />);

    const governorate = screen.getByLabelText('المحافظة 1');
    expect(governorate).toHaveValue('دمشق');
    expect(governorate).toHaveAttribute('readonly');
    expect(screen.getByLabelText('اسم الجهة 1')).toHaveAttribute('readonly');
    expect(screen.getByLabelText('فرع الجهة 1')).toHaveAttribute('readonly');
  });


  it('يملأ «المحافظة» تلقائيًا عند إضافة جهة جديدة طالبة للتنفيذ', async () => {
    const user = userEvent.setup();
    useAuthMock.mockReturnValue({ user: { role: 'lawyer', branchName: 'فرع حلب' } });
    render(<DocumentForm />);

    await user.click(screen.getByRole('button', { name: /إضافة جهة/ }));
    expect(screen.getByLabelText('المحافظة 1')).toHaveValue('حلب');
    expect(screen.getByLabelText('المحافظة 2')).toHaveValue('حلب');
  });


  it('يرسل تاريخ قيد الملف مع بيانات الملف عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('تاريخ قيد الملف'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/documents'));
    expect(navigateMock).toHaveBeenCalledWith('/documents');
  });


  it('يرسل تاريخ إلقاء حجز المنظومة مع بيانات الملف عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('تاريخ إلقاء حجز المنظومة'), '1/8/2026');
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(vi.mocked(api.post)).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerName).toBe('مقترض تجربة');
    expect(payload.amountNumeric).toBe(1500);
    expect(payload).not.toHaveProperty('amountWords');
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/documents'));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toEqual([{ type: 'note', text: 'ملاحظة افتتاحية' }]);
  });


  it('لا يرسل initialActions عندما يكون الحقلان فارغين', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toBeUndefined();
  });


  it('يرسل initialActions في PUT عند تعديل ملف مع إضافة إجراءات', async () => {
    const user = userEvent.setup();
    await renderEdit();

    await user.type(screen.getByLabelText('الإجراءات المطلوب إضافتها إلى الإخطار التنفيذي'), 'متابعة مع المحكمة');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديلات' }));

    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.initialActions).toBeUndefined();
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

    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('/documents/1'));
    expect(api.delete).toHaveBeenCalledWith('/documents/1');
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/documents'));
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

  it('يرسل حتى ثلاثة مبالغ مصرفية بعملاتها الافتراضية عند إضافة خانات جديدة', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('المبلغ المطالب به'), '1000');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ الثاني'), '2000');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ الثالث'), '3000');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

    await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1));
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

});
