import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AppealActionsModal from './AppealActionsModal';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const noop = () => {};

function existingAction() {
  return {
    id: 3,
    type: 'action',
    text: '<p>جلسة سابقة</p>',
    actionDate: '2026-07-01',
    reminderDuration: null,
    reminderColor: null,
    createdByName: 'محامي دمشق',
  };
}

describe('AppealActionsModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [existingAction()] });
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { id: 4 } });
  });

  it('القارئ (canWrite=false): يرى القائمة بلا أي زر كتابة', async () => {
    render(<AppealActionsModal appealId={7} onClose={noop} canWrite={false} />);

    expect(await screen.findByText('جلسة سابقة')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '+ إضافة إجراء أو ملاحظة' })).not.toBeInTheDocument();
  });

  it('الإجراء بلا تاريخ مرفوض مع تركيز الحقل وبلا إرسال', async () => {
    const user = userEvent.setup();
    render(<AppealActionsModal appealId={7} onClose={noop} canWrite />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة إجراء أو ملاحظة' }));
    await user.click(screen.getByLabelText('أدخل نص الإجراء أو الملاحظة...'));
    await user.keyboard('إجراء بلا تاريخ');
    await user.click(screen.getByRole('button', { name: 'حفظ كإجراء' }));

    expect(await screen.findByText('يجب إدخال تاريخ الإجراء')).toBeInTheDocument();
    expect(document.activeElement).toBe(screen.getByLabelText(/التاريخ/));
    expect(api.post).not.toHaveBeenCalled();
  });

  it('الملاحظة بلا تاريخ مسموحة والإجراء بتاريخ يُرسَل', async () => {
    const user = userEvent.setup();
    render(<AppealActionsModal appealId={7} onClose={noop} canWrite />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة إجراء أو ملاحظة' }));
    await user.click(screen.getByLabelText('أدخل نص الإجراء أو الملاحظة...'));
    await user.keyboard('ملاحظة حرة');
    await user.click(screen.getByRole('button', { name: 'حفظ كملاحظة' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/appeals/7/actions', expect.objectContaining({
        type: 'note',
        actionDate: null,
      }));
    });
  });

  it('يرفض «ذكرني» بلا مدة/لون بلا أي إرسال', async () => {
    const user = userEvent.setup();
    render(<AppealActionsModal appealId={7} onClose={noop} canWrite />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة إجراء أو ملاحظة' }));
    await user.click(screen.getByLabelText('أدخل نص الإجراء أو الملاحظة...'));
    await user.keyboard('إجراء بتذكير ناقص');
    await user.type(screen.getByLabelText(/التاريخ/), '3/8/2026');
    await user.click(screen.getByRole('button', { name: 'ذكرني' }));
    await user.click(screen.getByRole('button', { name: 'حفظ كإجراء' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('اختر مدة التذكير');
    expect(api.post).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(screen.getByLabelText('مدة التذكير'));
  });

  it('تعديل الإجراء يُرسَل بلا حقول تذكير (العقد لا يحملها)', async () => {
    const user = userEvent.setup();
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    render(<AppealActionsModal appealId={7} onClose={noop} canWrite />);

    await user.click(await screen.findByLabelText('تعديل الإجراء'));
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/appeals/7/actions/3', {
        type: 'action',
        text: '<p>جلسة سابقة</p>',
        actionDate: '2026-07-01',
      });
    });
    expect(api.put).toHaveBeenCalledTimes(1);
    expect(api.delete).not.toHaveBeenCalled();
  });

  it('تعديل التذكير يُحفظ عبر مساره المستقل ولا يغيّر تاريخ الإجراء', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [{ ...existingAction(), reminderDuration: '3 أيام', reminderColor: 'أحمر' }],
    });
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    render(<AppealActionsModal appealId={7} onClose={noop} canWrite />);

    await user.click(await screen.findByLabelText('تعديل الإجراء'));
    await user.selectOptions(screen.getByLabelText('مدة التذكير'), 'أسبوع');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/appeals/7/actions/3/reminder', {
        reminderDuration: 'أسبوع',
        reminderColor: 'أحمر',
      });
    });
  });
});
