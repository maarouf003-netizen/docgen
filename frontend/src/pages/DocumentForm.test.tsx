import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentForm from './DocumentForm';
import { api } from '../api/client';
import type { DocumentResponse } from '../types';

const { navigateMock, paramsMock } = vi.hoisted(() => ({
  navigateMock: vi.fn(),
  paramsMock: { id: undefined as string | undefined },
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
  useParams: () => paramsMock,
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return {
    ...original,
    api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
  };
});

describe('DocumentForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
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
  };

  async function renderEdit(doc: DocumentResponse = mockDoc) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
    render(<DocumentForm />);
    return screen.findByText('⚙️ تغيير الحالة');
  }

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
});
