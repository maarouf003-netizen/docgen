import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ExecutedStatusModal from './ExecutedStatusModal';
import { makeDocument } from '../test/factories';
import type { DocumentResponse } from '../types';

const apiMock = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
}));

vi.mock('../api/client', () => ({
  api: apiMock,
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function renderModal(doc: DocumentResponse, onChanged = () => {}) {
  return render(<ExecutedStatusModal doc={doc} onClose={() => {}} onChanged={onChanged} />);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('ExecutedStatusModal', () => {
  it('يعرض حالة الملف الحالية وخيارات الحالة المتاحة من «متداول»', async () => {
    renderModal(makeDocument({ generalEntitySide: 'executed', executedStatus: '' }));

    expect(await screen.findByText('الحالة الحالية')).toBeInTheDocument();
    expect(screen.getByText('متداول')).toBeInTheDocument();
    expect(screen.getByLabelText('الحالة')).toHaveValue('منفذ');
  });

  it('يحفظ الانتقال إلى «منفذ» على صفة «منفذ عليها» بكيفية تنفيذ الملف والمبلغ', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    renderModal(makeDocument({ generalEntitySide: 'executed', executedStatus: '' }));

    await user.type(screen.getByLabelText('كيفية تنفيذ الملف'), 'تم التحصيل');
    await user.type(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة'), '2000');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledWith('/documents/1/executed-status', {
      status: 'منفذ',
      executedDescription: 'تم التحصيل',
      executedPaidAmount: 2000,
      executedPaidCurrency: 'ليرة سورية',
    });
  });

  it('يحفظ تاريخ التنفيذ عند الانتقال إلى «منفذ» على صفة «منفذ عليها»', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    renderModal(makeDocument({ generalEntitySide: 'executed', executedStatus: '' }));

    await user.type(screen.getByLabelText('تاريخ التنفيذ'), '15/8/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledWith('/documents/1/executed-status', {
      status: 'منفذ',
      executedExecutionDate: '15/8/2026',
    });
  });

  it('يحفظ حتى ثلاثة مبالغ مدفوعة بعملاتها عند إضافة خانات جديدة', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    renderModal(makeDocument({ generalEntitySide: 'executed', executedStatus: '' }));

    await user.type(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة'), '2000');
    await user.selectOptions(screen.getByLabelText('العملة'), 'دولار أمريكي');
    await user.click(screen.getByRole('button', { name: '➕ مبلغ آخر' }));
    await user.type(screen.getByLabelText('المبلغ الذي دفعته الجهة العامة 2'), '3000');
    await user.selectOptions(screen.getAllByLabelText('العملة')[1], 'يورو');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledWith('/documents/1/executed-status', {
      status: 'منفذ',
      executedPaidAmount: 2000,
      executedPaidCurrency: 'دولار أمريكي',
      executedPaidAmount2: 3000,
      executedPaidCurrency2: 'يورو',
    });
  });

  it('يحفظ الانتقال إلى «منفذ» على صفة «عرض وايداع» بالمبلغ المودع وتاريخ الإيداع', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    renderModal(makeDocument({ generalEntitySide: 'deposit', executedStatus: '' }));

    await user.type(screen.getByLabelText('المبلغ المودع'), '1250');
    await user.type(screen.getByLabelText('تاريخ ايداعه حساب الجهة العامة'), '10/6/2024');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledWith('/documents/1/executed-status', {
      status: 'منفذ',
      executedPaidAmount: 1250,
      executedPaidCurrency: 'ليرة سورية',
      executedDepositDate: '10/6/2024',
    });
  });

  it('يحفظ تاريخ الشطب عند الانتقال إلى «مشطوب» مع تعريب الأرقام', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    renderModal(makeDocument({ generalEntitySide: 'executed', executedStatus: '' }));

    await user.selectOptions(screen.getByLabelText('الحالة'), 'مشطوب');
    await user.type(screen.getByLabelText('تاريخ الشطب'), '٥/٨/٢٠٢٦');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledWith('/documents/1/executed-status', {
      status: 'مشطوب',
      struckOffDate: '5/8/2026',
    });
  });

  it('يرفض إعادة الملف المشطوب بلا رقم ملف جديد ويعرض رسالة الخلفية', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    const user = userEvent.setup();
    renderModal(makeDocument({ generalEntitySide: 'executed', executedStatus: 'مشطوب', struckOffDate: '2026-08-05' }));

    // «متداول» (الإعادة) هي الخيار الأول للملف المشطوب مع حقول التجديد.
    expect(screen.getByLabelText('الحالة')).toHaveValue('متداول');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(await screen.findByText('رقم الملف الجديد مطلوب عند إعادة الملف المشطوب')).toBeInTheDocument();
    expect(apiPost).not.toHaveBeenCalled();
  });

  it('يحفظ تجديد الملف المشطوب عند الإعادة إلى متداول برقمه الجديد', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    const onChanged = vi.fn();
    renderModal(makeDocument({ generalEntitySide: 'executed', executedStatus: 'مشطوب', struckOffDate: '2026-08-05' }), onChanged);

    await user.type(screen.getByLabelText(/رقم الملف الجديد/), '2026/55');
    await user.type(screen.getByLabelText('نوع الملف الجديد'), 'قضية تنفيذ');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledWith('/documents/1/executed-status', {
      status: '',
      renewalFileNumber: '2026/55',
      renewalFileType: 'قضية تنفيذ',
      renewalFileReceiptNumber: null,
      renewalFileReceiptDate: null,
      renewalDate: null,
    });
    expect(onChanged).toHaveBeenCalled();
    expect(apiPost).toHaveBeenCalledTimes(1);
  });

  it('يعرض رسالة نهائية ولا خيارات عند محاولة تغيير «منفذ عليها» من حالة «منفذ»', async () => {
    renderModal(makeDocument({
      generalEntitySide: 'executed',
      generalEntitySideLabel: 'الجهة العامة منفذ عليها',
      executedStatus: 'منفذ',
      executedPaidAmount: 2000,
    }));

    expect(await screen.findByText(/حالة «منفذ» في صفة «الجهة العامة منفذ عليها» نهائية/)).toBeInTheDocument();
    expect(screen.queryByLabelText('الحالة')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حفظ الحالة' })).not.toBeInTheDocument();
  });

  it('يعرض خيار «متداول» فقط لحالة «منفذ» في «عرض وايداع» مع حقول السير بالملف', async () => {
    renderModal(makeDocument({ generalEntitySide: 'deposit', executedStatus: 'منفذ', executedPaidAmount: 1250 }));

    const select = await screen.findByLabelText('الحالة');
    expect(select).toHaveValue('متداول');
    expect(screen.queryByRole('option', { name: 'مشطوب' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('رقم كتاب الجهة العامة بالسير بالملف')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ كتاب الجهة العامة بالسير بالملف')).toBeInTheDocument();
    expect(screen.getByLabelText('رقم ورود كتاب بالسير بالملف')).toBeInTheDocument();
    expect(screen.getByLabelText('تاريخ ورود كتاب بالسير بالملف')).toBeInTheDocument();
  });

  it('يرفض إرجاع «عرض وايداع» من «منفذ» إلى متداول دون حقول السير بالملف', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    const user = userEvent.setup();
    renderModal(makeDocument({ generalEntitySide: 'deposit', executedStatus: 'منفذ', executedPaidAmount: 1250 }));

    await screen.findByLabelText('الحالة');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(await screen.findByText('يجب إدخال رقم وتاريخ كتاب الجهة العامة بالسير بالملف وورودهما')).toBeInTheDocument();
    expect(apiPost).not.toHaveBeenCalled();
  });

  it('يرسل حقول السير بالملف عند إرجاع «عرض وايداع» من «منفذ» إلى متداول', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    const onChanged = vi.fn();
    renderModal(makeDocument({ generalEntitySide: 'deposit', executedStatus: 'منفذ', executedPaidAmount: 1250 }), onChanged);

    await screen.findByLabelText('الحالة');
    await user.type(screen.getByLabelText('رقم كتاب الجهة العامة بالسير بالملف'), '44');
    await user.type(screen.getByLabelText('تاريخ كتاب الجهة العامة بالسير بالملف'), '1/8/2026');
    await user.type(screen.getByLabelText('رقم ورود كتاب بالسير بالملف'), '55');
    await user.type(screen.getByLabelText('تاريخ ورود كتاب بالسير بالملف'), '2/8/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledWith('/documents/1/executed-status', {
      status: '',
      sayerNumber: '44',
      sayerDate: '1/8/2026',
      sayerRegNumber: '55',
      sayerRegDate: '2/8/2026',
    });
    expect(onChanged).toHaveBeenCalled();
  });

  it('يستدعي onChanged عند نجاح الحفظ ويُغلق النافذة', async () => {
    const apiPost = api.post as unknown as ReturnType<typeof vi.fn>;
    apiPost.mockResolvedValue({});
    const user = userEvent.setup();
    const onChanged = vi.fn();
    const onClose = vi.fn();
    render(
      <ExecutedStatusModal doc={makeDocument({ generalEntitySide: 'executed', executedStatus: '' })} onClose={onClose} onChanged={onChanged} />,
    );

    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(apiPost).toHaveBeenCalledTimes(1);
    expect(onChanged).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });
});