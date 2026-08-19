import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import StatusChangeModal from './StatusChangeModal';
import type { DocumentResponse } from '../types';
import { makeDocument } from '../test/factories';

vi.mock('../api/client', () => ({
  api: { post: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { response?: { data?: { message?: string } } })?.response?.data?.message
    ?? (error as { message?: string })?.message
    ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../api/client';

function renderModal(doc: Partial<DocumentResponse>) {
  render(<StatusChangeModal doc={makeDocument(doc)} onClose={() => {}} onChanged={() => {}} />);
}

describe('StatusChangeModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('يعرض من المتداول كل الانتقالات المسموحة: تريث/منفذ بالتسوية/منفذ جبريا/مشطوب', () => {
    renderModal({ isDraft: false, execStatus: '' });

    const select = screen.getByLabelText('الإجراء') as HTMLSelectElement;
    const options = Array.from(select.options).map((o) => o.textContent);
    expect(options).toEqual(['تريث', 'منفذ بالتسوية', 'منفذ جبريا', 'مشطوب']);
  });

  it('لا يعرض «منفذ جبريا» من تحت الرفع (مقيد بآلة الحالات)', () => {
    renderModal({ isDraft: true, execStatus: '' });

    const select = screen.getByLabelText('الإجراء') as HTMLSelectElement;
    const options = Array.from(select.options).map((o) => o.textContent);
    expect(options).toEqual(['تريث', 'منفذ بالتسوية']);
  });

  it('يرسل حقول التريث عند الاختيار مع حفظ الحالة', async () => {
    const user = userEvent.setup();
    renderModal({ isDraft: false, execStatus: '' });

    await user.type(screen.getByLabelText('رقم كتاب التريث'), '5');
    await user.type(screen.getByLabelText('تاريخ كتاب التريث'), '1/1/2024');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/1/status', {
      status: 'تريث',
      fields: { tarithNumber: '5', tarithDate: '1/1/2024' },
    });
  });

  it('يمنع منفذ جبريا دون اختيار عقارات مباعة', async () => {
    const user = userEvent.setup();
    renderModal({
      isDraft: false,
      execStatus: '',
      assets: [{ id: 1, assetKind: 'عقار', property: 'بيت' }],
    });

    await user.selectOptions(screen.getByLabelText('الإجراء'), 'منفذ جبريا');
    await user.type(screen.getByLabelText('تاريخ قرار الإحالة القطعية'), '1/6/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(screen.getByText('اختر الأموال التي جرى بيعها بالمزاد العلني على الأقل')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرفض منفذ جبريا دون تاريخ قرار الإحالة القطعية', async () => {
    const user = userEvent.setup();
    renderModal({
      isDraft: false,
      execStatus: '',
      assets: [{ id: 1, assetKind: 'عقار', property: 'بيت' }],
    });

    await user.selectOptions(screen.getByLabelText('الإجراء'), 'منفذ جبريا');
    await user.click(screen.getByLabelText(/بيت/));
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(screen.getByText('يجب إدخال تاريخ قرار الإحالة القطعية')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يرسل العقارات المباعة وتاريخ قرار الإحالة القطعية عند تعبئة منفذ جبريا', async () => {
    const user = userEvent.setup();
    renderModal({
      isDraft: false,
      execStatus: '',
      assets: [{ id: 7, assetKind: 'عقار', property: 'بيت' }],
    });

    await user.selectOptions(screen.getByLabelText('الإجراء'), 'منفذ جبريا');
    await user.type(screen.getByLabelText('تاريخ قرار الإحالة القطعية'), '٥/٦/٢٠٢٦');
    await user.click(screen.getByLabelText(/بيت/));
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/1/status', {
      status: 'منفذ جبريا',
      fields: { execSubStatus: 'منفذ كاملا', forcedExecutionDate: '5/6/2026', soldAssetIds: '7' },
    });
  });

  it('من تريث يعرض منفذ بالتسوية وتراجع فقط، والتراجع يستدعي revert-status بحقول السير بالملف', async () => {
    const user = userEvent.setup();
    renderModal({ isDraft: false, execStatus: 'تريث' });

    const select = screen.getByLabelText('الإجراء') as HTMLSelectElement;
    expect(Array.from(select.options).map((o) => o.textContent)).toEqual(['منفذ بالتسوية', 'تراجع']);

    await user.selectOptions(select, 'تراجع');
    await user.type(screen.getByLabelText('رقم كتاب الجهة العامة بالسير بالملف'), '8');
    await user.type(screen.getByLabelText('تاريخ كتاب الجهة العامة بالسير بالملف'), '2/2/2024');
    await user.type(screen.getByLabelText('رقم ورود كتاب بالسير بالملف'), '9');
    await user.type(screen.getByLabelText('تاريخ ورود كتاب بالسير بالملف'), '3/3/2024');
    await user.click(screen.getByRole('button', { name: 'حفظ الحالة' }));

    expect(api.post).toHaveBeenCalledWith('/documents/1/revert-status', {
      fields: { sayerNumber: '8', sayerDate: '2/2/2024', sayerRegNumber: '9', sayerRegDate: '3/3/2024' },
    });
  });

  it('لملف مشطوب لا يعرض انتقالات ويذكر صفحة «الملفات المشطوبة»', () => {
    renderModal({ isDraft: false, execStatus: 'مشطوب' });

    expect(screen.getByText(/الملفات المشطوبة/)).toBeInTheDocument();
    expect(screen.queryByLabelText('الإجراء')).not.toBeInTheDocument();
  });
});
