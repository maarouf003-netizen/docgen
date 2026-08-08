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
    inclusionAmountNumeric: 0,
    viewCount: 0,
    printCount: 0,
    guarantors: [],
    realEstates: [],
    executionApplicants: [],
    executedPublicEntities: [],
    executedNaturalPersons: [],
  };

  async function renderEdit(doc: DocumentResponse = mockDoc) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
    render(<DocumentForm />);
    return screen.findByText('⚙️ تغيير الحالة');
  }

  it('يحوّل تسمية حقل عنوان المقترض إلى «الوكيل» عند اختيار «يمثله»', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    expect(screen.getByLabelText('العنوان')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('نوع العنوان'), 'يمثله');

    expect(screen.getByLabelText('الوكيل')).toBeInTheDocument();
    expect(screen.queryByLabelText('العنوان')).not.toBeInTheDocument();
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
    expect(screen.getByText('➕ إضافة كفيل')).toBeInTheDocument();

    expect(screen.queryByText('المتضمن')).not.toBeInTheDocument();
    expect(screen.queryByText(/المبلغ كتابة/)).not.toBeInTheDocument();
  });

  it('يعرض الفرع ورقم/تاريخ كتاب الجهة العامة ورقم تحت رفع في المعلومات الأساسية', () => {
    render(<DocumentForm />);

    expect(screen.getByLabelText('الفرع')).toBeInTheDocument();
    expect(screen.getByLabelText('الفرع')).not.toHaveAttribute('placeholder');
    expect(screen.getByLabelText('رقم كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ كتاب الجهة العامة')).toBeInTheDocument();
    expect(screen.getByLabelText('رقم تحت رفع')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ قيد الملف')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ إلقاء حجز المنظومة')).toBeInTheDocument();
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
    expect(screen.getByText('➕ إضافة منفذ عليه')).toBeInTheDocument();

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
      await user.click(screen.getByText('➕ إضافة كفيل'));
    }

    const button = screen.getByRole('button', { name: /🛑 الحد الأقصى/ });
    expect(button).toBeDisabled();
  });

  it('يعرض زر «إعادة تعيين» في الإدخال الجديد فقط', async () => {
    const { unmount } = render(<DocumentForm />);
    expect(screen.getByRole('button', { name: /إعادة تعيين/ })).toBeInTheDocument();
    unmount();

    await renderEdit();
    expect(screen.queryByRole('button', { name: /إعادة تعيين/ })).not.toBeInTheDocument();
  });

  it('يعرض قسم «تغيير الحالة» فقط عند تعديل ملف موجود', async () => {
    render(<DocumentForm />);
    expect(screen.queryByText('⚙️ تغيير الحالة')).not.toBeInTheDocument();

    await renderEdit();
    expect(screen.getByLabelText('الحالة')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'حفظ الحالة' })).toBeInTheDocument();
  });

  it('يعرض حقول تغيير الحالة وفق الحالة المختارة في التعديل', async () => {
    const user = userEvent.setup();
    await renderEdit();

    expect(screen.getByLabelText('نوع التنفيذ')).toBeInTheDocument();
    expect(screen.getByLabelText('المبلغ المحصل')).toBeInTheDocument();
    expect(screen.queryByText('رقم كتاب براءة الذمة')).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('الحالة'), 'منفذ بالتسوية');

    expect(screen.getByText('رقم كتاب براءة الذمة')).toBeInTheDocument();
    expect(screen.getByText('تاريخ كتاب براءة الذمة')).toBeInTheDocument();
    expect(screen.getByText('رقم ورود كتاب براءة الذمة')).toBeInTheDocument();
    expect(screen.getByText('تاريخ ورود كتاب براءة الذمة')).toBeInTheDocument();
    expect(screen.getByLabelText('المبلغ المحصل')).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('الحالة'), 'تريث');

    expect(screen.getByText('رقم كتاب التريث')).toBeInTheDocument();
    expect(screen.getByText('تاريخ كتاب التريث')).toBeInTheDocument();
    expect(screen.getByText('رقم ورود كتاب التريث')).toBeInTheDocument();
    expect(screen.getByText('تاريخ ورود كتاب التريث')).toBeInTheDocument();
    expect(screen.queryByText('المبلغ المحصل')).not.toBeInTheDocument();
  });

  it('يمنع تعيين تريث دون رقم وتاريخ كتاب التريث في التعديل', async () => {
    const user = userEvent.setup();
    await renderEdit();

    await user.selectOptions(screen.getByLabelText('الحالة'), 'تريث');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(screen.getByText('يجب إدخال رقم وتاريخ كتاب التريث على الأقل')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يمنع تعيين منفذ بالتسوية دون رقم وتاريخ كتاب براءة الذمة في التعديل', async () => {
    const user = userEvent.setup();
    await renderEdit();

    await user.selectOptions(screen.getByLabelText('الحالة'), 'منفذ بالتسوية');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(screen.getByText('يجب إدخال رقم وتاريخ كتاب براءة الذمة على الأقل')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرسل حقول براءة الذمة عند تعيين منفذ بالتسوية مكتمل في التعديل', async () => {
    const user = userEvent.setup();
    await renderEdit();

    await user.selectOptions(screen.getByLabelText('الحالة'), 'منفذ بالتسوية');
    await user.type(screen.getByLabelText('رقم كتاب براءة الذمة'), '77');
    await user.type(screen.getByLabelText('تاريخ كتاب براءة الذمة'), '1/1/2024');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/1/status', {
      status: 'منفذ بالتسوية',
      fields: { baraetNumber: '77', baraetDate: '1/1/2024' },
    });
  });

  it('يرسل نوع التنفيذ والمبلغ المحصل عند تعيين منفذ جبريا كاملا في التعديل', async () => {
    const user = userEvent.setup();
    await renderEdit();

    await user.type(screen.getByLabelText('المبلغ المحصل'), '750');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/1/status', {
      status: 'منفذ جبريا',
      fields: { execSubStatus: 'منفذ كاملا', collectedAmount: '750' },
    });
  });

  it('يرسل نوع التنفيذ الفرعي والمبلغ المحصل عند تحديث حالة «منفذ جبريا» في التعديل', async () => {
    const user = userEvent.setup();
    await renderEdit();

    await user.selectOptions(screen.getByLabelText('نوع التنفيذ'), 'منفذ جزئيا');
    await user.type(screen.getByLabelText('المبلغ المحصل'), '750');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/1/status', {
      status: 'منفذ جبريا',
      fields: { execSubStatus: 'منفذ جزئيا', collectedAmount: '750' },
    });
  });

  it('يعرض زر «إلغاء الحالة» فقط عند وجود حالة سابقة ويستدعي cancel-status', async () => {
    const user = userEvent.setup();
    await renderEdit({ ...mockDoc, execStatus: 'منفذ جبريا', execSubStatus: 'منفذ جزئيا', collectedAmount: 500 });

    expect(screen.getByRole('button', { name: 'إلغاء الحالة' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'إلغاء الحالة' }));
    expect(api.post).toHaveBeenCalledWith('/documents/1/cancel-status');
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

    const heirName = screen.getByLabelText('الاسم الثلاثي للوريث');
    await user.type(heirName, 'محمود الحلبي');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    const heirAddress = Array.from(heirRow.querySelectorAll('input')).find(
      (el) => el.previousElementSibling?.textContent === 'العنوان',
    ) as HTMLInputElement;
    await user.type(heirAddress, 'المزة');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.borrowerHeirs).toEqual([{ name: 'محمود الحلبي', addressType: 'عنوان', address: 'المزة' }]);
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
    await user.type(within(card).getByLabelText('الاسم الثلاثي للوريث'), 'فارس الخالد');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const guarantors = payload.guarantors as { heirs: unknown[] }[];
    expect(guarantors[0].heirs).toEqual([{ name: 'فارس الخالد', addressType: 'عنوان', address: '' }]);
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

  it('يبدّل تسمية حقل الوريث بين «العنوان» و«الوكيل»', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);

    const heirName = screen.getByLabelText('الاسم الثلاثي للوريث');
    const heirRow = heirName.closest('.grid') as HTMLElement;
    const typeSelect = heirRow.querySelector('select') as HTMLSelectElement;

    expect(Array.from(heirRow.querySelectorAll('input')).some((el) => el.previousElementSibling?.textContent === 'العنوان')).toBe(true);

    await user.selectOptions(typeSelect, 'وكيل');

    expect(Array.from(heirRow.querySelectorAll('input')).some((el) => el.previousElementSibling?.textContent === 'الوكيل')).toBe(true);
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

    expect(screen.getAllByLabelText('الاسم الثلاثي للوريث')[0]).toHaveValue('محمود الحلبي');

    const card = screen.getByText('كفيل 1').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByLabelText('الاسم الثلاثي للوريث')).toHaveValue('فارس الخالد');
    expect(within(card).getByLabelText('الوكيل')).toHaveValue('المحامي سامر');
  });

  it('يعرض ورثة المقترض في قائمة مالكي العقار ويُرسل المختارين عند الحفظ', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.type(screen.getByLabelText('الاسم'), 'أحمد');
    await user.type(screen.getByLabelText('النسبة'), 'الخطيب');
    await user.click(screen.getAllByRole('button', { name: '＋ إضافة وريث' })[0]);
    await user.type(screen.getByLabelText('الاسم الثلاثي للوريث'), 'محمود الحلبي');

    await user.click(screen.getByRole('button', { name: /🏡 إضافة عقار/ }));

    const heirBox = screen.getByRole('checkbox', { name: 'محمود الحلبي' });
    const borrowerBox = screen.getByRole('checkbox', { name: 'أحمد الخطيب' });
    expect(heirBox).toBeInTheDocument();
    expect(borrowerBox).toBeInTheDocument();

    await user.click(heirBox);
    await user.click(borrowerBox);
    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    const realEstates = payload.realEstates as { owners: string[] }[];
    expect(realEstates[0].owners).toEqual(['محمود الحلبي', 'أحمد الخطيب']);
  });

  it('يحافظ على مالك محفوظ سابقًا غير موجود ضمن الخيارات عند التعديل', async () => {
    const docWithOldOwner: DocumentResponse = {
      ...mockDoc,
      realEstates: [
        {
          id: 7,
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
    const realEstates = payload.realEstates as { owners: string[]; shareType: string }[];
    expect(realEstates[0].owners).toEqual(['سمير علي']);
    expect(realEstates[0].shareType).toEqual('حصة سهمية');
  });

  it('يصحّح حصة العقار إلى «حصة سهمية» عند التحميل لعقار بملاك متعددين', async () => {
    const docWithInvalidShare: DocumentResponse = {
      ...mockDoc,
      realEstates: [
        {
          id: 8,
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
    expect(screen.getByText('المبلغ المطلوب دفعه من الجهة العامة')).toBeInTheDocument();
    expect(screen.getByText('👤 طالب التنفيذ')).toBeInTheDocument();
    expect(screen.getByText('🏛️ المنفذ عليه')).toBeInTheDocument();
    expect(screen.getByText('📋 حالة الملف')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ ورود الملف')).toBeInTheDocument();
    expect(screen.getByLabelText('المحكمة مصدرة القرار')).toBeInTheDocument();

    expect(screen.queryByLabelText('تاريخ قيد الملف')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('رقم كتاب الجهة العامة')).not.toBeInTheDocument();
    expect(screen.queryByText('اكتب ما تم من اجراءات لإضافتها الى الإخطار التنفيذي')).not.toBeInTheDocument();
  });

  it('يرسل بيانات وضع «منفذ عليه» (الملف والسند والمبالغ وحالة الملف) عند الحفظ', async () => {
    const user = userEvent.setup();
    await selectExecutedSide(user);

    await user.type(screen.getByLabelText('دائرة التنفيذ'), 'دمشق');
    await user.type(screen.getByLabelText('رقم الملف'), '55');
    await user.selectOptions(screen.getByLabelText('سنة الملف'), '2026');
    await user.type(screen.getByLabelText('تاريخ ورود الملف'), '1/8/2026');
    await user.type(screen.getByLabelText('المحكمة مصدرة القرار'), 'محكمة التنفيذ');
    await user.type(screen.getByLabelText('رقم القرار'), '101');
    fireEvent.change(screen.getByLabelText('تاريخ القرار'), { target: { value: '2026-07-15' } });
    await user.type(screen.getByLabelText('المتضمن'), 'خلاصة القرار');
    await user.type(screen.getByLabelText('المبلغ المطلوب دفعه من الجهة العامة'), '5000');
    const applicantCard = screen.getByText('طالب التنفيذ 1').closest('.rounded-xl') as HTMLElement;
    await user.type(applicantCard.querySelector('input') as HTMLInputElement, 'سليم');

    await user.selectOptions(screen.getByLabelText('الحالة'), 'منفذ');
    await user.type(screen.getByLabelText('كيفية تنفيذ الملف'), 'تم التحصيل');
    await user.click(screen.getByRole('button', { name: '➕ إضافة مبلغ' }));
    await user.type(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة'), '2000');

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    const [, payload] = vi.mocked(api.post).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.fileReceiptDate).toBe('1/8/2026');
    expect(payload.contractType).toBe('محكمة التنفيذ');
    expect(payload.contractNumber).toBe('101');
    expect(payload.contractDate).toBe('2026-07-15');
    expect(payload.inclusionText).toBe('خلاصة القرار');
    expect(payload.executedRequiredAmount).toBe(5000);
    expect(payload.executedStatus).toBe('منفذ');
    expect(payload.executedDescription).toBe('تم التحصيل');
    expect(payload.executedPaidAmount).toBe(2000);
    expect(payload.contractTypeSelector).toBe('عادي');
    expect(payload.guarantors).toEqual([]);
    expect(payload.borrowerHeirs).toEqual([]);
    expect(payload.realEstates).toEqual([]);
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
});
