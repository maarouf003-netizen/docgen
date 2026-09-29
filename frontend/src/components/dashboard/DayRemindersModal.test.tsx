import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { DayRemindersModal } from './DayRemindersModal';
import type { CalendarOccurrence } from './personalReminders';
import type { AppealReminderDto, PersonalReminderDto, ReminderDto } from '../../types';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const DAY = '2026-08-08';

function docOccurrence(): CalendarOccurrence {
  const reminder: ReminderDto = {
    actionId: 1,
    documentId: 5,
    borrowerName: 'سامر',
    borrowerFather: 'محمد',
    borrowerFamily: 'حسن',
    actionText: 'مراجعة دائرة التنفيذ',
    actionDate: '2026-08-01',
    reminderDuration: '3 أيام',
    reminderColor: 'أحمر',
    dueDate: '2026-08-08',
    dueDateSuspect: false,
  };
  return { dayKey: DAY, kind: 'document', reminder };
}

function appealOccurrence(): CalendarOccurrence {
  const reminder: AppealReminderDto = {
    actionId: 2,
    appealId: 9,
    documentId: 5,
    appealTitle: 'استئناف القرار 9',
    actionText: 'جلسة',
    dueDate: '2026-08-08',
    dueDateSuspect: false,
  };
  return { dayKey: DAY, kind: 'appeal', reminder };
}

function personalOccurrence(done = false): CalendarOccurrence {
  const personal: PersonalReminderDto = {
    id: 7,
    title: 'تذكير شخصي',
    notes: 'ملاحظة',
    dueDate: DAY,
    color: 'زمردي',
    recurrence: 'مرة واحدة',
    recurrenceEnd: null,
    isArchived: false,
    completedOccurrenceKeys: done ? [DAY] : [],
    createdAt: '2026-08-01',
  };
  return { dayKey: DAY, kind: 'personal', personal, ...(done ? { done: true as const } : {}) };
}

function renderModal(occurrences: CalendarOccurrence[], onChanged = vi.fn()) {
  const onClose = vi.fn();
  render(
    <DayRemindersModal dayKey={DAY} occurrences={occurrences} onClose={onClose} onChanged={onChanged} />,
  );
  return { onClose, onChanged };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('DayRemindersModal', () => {
  it('يعرض تسمية اليوم والمواعيد الثلاثة بروابطها', () => {
    renderModal([docOccurrence(), appealOccurrence(), personalOccurrence()]);

    expect(screen.getByRole('dialog', { name: /تذكيرات/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'سامر محمد حسن' })).toHaveAttribute('href', '/documents/5');
    expect(screen.getByRole('link', { name: 'استئناف القرار 9' })).toHaveAttribute('href', '/appeals/9');
    expect(screen.getByText('تذكير شخصي')).toBeInTheDocument();
    expect(screen.getByText('مراجعة دائرة التنفيذ')).toBeInTheDocument();
  });

  it('يصفّي بالبحث النصي', async () => {
    const user = userEvent.setup();
    renderModal([docOccurrence(), personalOccurrence()]);

    await user.type(screen.getByPlaceholderText('بحث بالنص أو اسم الملف…'), 'شخصي');
    expect(screen.queryByText('سامر محمد حسن')).not.toBeInTheDocument();
    expect(screen.getByText('تذكير شخصي')).toBeInTheDocument();
  });

  it('يغلق بمفتاح Escape', () => {
    const { onClose } = renderModal([docOccurrence()]);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يغلق بزر الإغلاق المرئي', async () => {
    const user = userEvent.setup();
    const { onClose } = renderModal([docOccurrence()]);

    await user.click(screen.getByRole('button', { name: 'إغلاق' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('يضع التركيز الأولي على اللوحة لا على زر الخلفية', () => {
    renderModal([docOccurrence()]);
    expect(document.activeElement?.getAttribute('tabindex')).toBe('-1');
  });

  it('يعزل مسودة التعديل عن هدف آخر', async () => {
    const user = userEvent.setup();
    const occB = personalOccurrence();
    occB.personal = { ...occB.personal!, id: 8, title: 'ثانٍ' };
    renderModal([personalOccurrence(), occB]);

    const edits = screen.getAllByRole('button', { name: 'تعديل' });
    await user.click(edits[0]);
    const titleInput = screen.getByLabelText('العنوان *');
    await user.clear(titleInput);
    await user.type(titleInput, 'مسودة أ');

    await user.click(edits[1]);
    expect(screen.getByLabelText('العنوان *')).toHaveValue('ثانٍ');
  });

  it('يسمّي الخيار الفارغ إبقاءً عند وجود نهاية ويُبقيها ما لم يُختر غيره', async () => {
    const user = userEvent.setup();
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const occ = personalOccurrence();
    occ.personal = { ...occ.personal!, recurrence: 'يومي', recurrenceEnd: '2026-09-01' };
    renderModal([occ]);

    await user.click(screen.getByRole('button', { name: 'تعديل' }));
    const endSelect = screen.getByLabelText(/انتهاء التكرار/);
    expect(within(endSelect as HTMLElement).getByRole('option', { name: 'إبقاء النهاية الحالية' })).toBeInTheDocument();
    expect(within(endSelect as HTMLElement).queryByRole('option', { name: 'بلا نهاية' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));
    const body = (api.put as unknown as ReturnType<typeof vi.fn>).mock.calls[0][1] as Record<string, unknown>;
    // الخيار الفارغ يُبقي المخزّن: يُرسَل `undefined` (تُسقطه الخلفية) لا قيمة.
    expect(body.recurrenceEnd).toBeUndefined();
  });

  it('يزيل نهاية التكرار من نموذج التعديل صراحةً', async () => {
    const user = userEvent.setup();
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const occ = personalOccurrence();
    occ.personal = {
      ...occ.personal!,
      recurrence: 'يومي',
      recurrenceEnd: '2026-09-01',
    };
    renderModal([occ]);

    await user.click(screen.getByRole('button', { name: 'تعديل' }));
    const endSelect = screen.getByLabelText(/انتهاء التكرار/);
    expect(endSelect).toHaveTextContent('إزالة النهاية');
    await user.selectOptions(endSelect, '__clear__');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    expect(api.put).toHaveBeenCalledWith(
      '/personal-reminders/7',
      expect.objectContaining({ recurrenceEnd: '' }),
    );
  });

  it('ينشئ تذكيرًا شخصيًا بتاريخ اليوم المختار', async () => {
    const user = userEvent.setup();
    (api.post as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const { onChanged } = renderModal([docOccurrence()]);

    await user.click(screen.getByRole('button', { name: '+ تذكير شخصي لهذا اليوم' }));
    await user.type(screen.getByLabelText('العنوان *'), 'متابعة جديدة');
    await user.click(screen.getByRole('button', { name: 'إضافة التذكير' }));

    expect(api.post).toHaveBeenCalledWith(
      '/personal-reminders',
      expect.objectContaining({ title: 'متابعة جديدة', dueDate: DAY }),
    );
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('يرفض العنوان الفارغ ويركّز الحقل', async () => {
    const user = userEvent.setup();
    renderModal([]);

    await user.click(screen.getByRole('button', { name: '+ تذكير شخصي لهذا اليوم' }));
    await user.click(screen.getByRole('button', { name: 'إضافة التذكير' }));

    expect(screen.getByRole('alert')).toHaveTextContent('عنوان التذكير مطلوب');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('يغيّر يوم تذكير الملف مع حفظ نوع الإجراء الأصلي', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [{ id: 1, type: 'action', text: 'مراجعة دائرة التنفيذ' }],
    });
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const { onChanged } = renderModal([docOccurrence()]);

    await user.click(screen.getByRole('button', { name: 'تغيير اليوم' }));
    const input = screen.getByLabelText(/اليوم الجديد/);
    await user.clear(input);
    await user.type(input, '10/8/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ اليوم' }));

    expect(api.get).toHaveBeenCalledWith('/documents/5/actions');
    expect(api.put).toHaveBeenCalledWith(
      '/documents/5/actions/1',
      expect.objectContaining({
        type: 'action',
        text: 'مراجعة دائرة التنفيذ',
        actionDate: '10/8/2026',
        reminderDuration: '3 أيام',
        reminderColor: 'أحمر',
      }),
    );
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('يغيّر يوم تذكير الاستئناف مع حفظ نوع الإجراء الأصلي', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: [{ id: 2, type: 'note', text: 'جلسة' }],
    });
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const { onChanged } = renderModal([appealOccurrence()]);

    await user.click(screen.getByRole('button', { name: 'تغيير اليوم' }));
    const input = screen.getByLabelText(/اليوم الجديد/);
    await user.clear(input);
    await user.type(input, '10/8/2026');
    await user.click(screen.getByRole('button', { name: 'حفظ اليوم' }));

    expect(api.get).toHaveBeenCalledWith('/appeals/9/actions');
    expect(api.put).toHaveBeenCalledWith(
      '/appeals/9/actions/2',
      expect.objectContaining({
        type: 'note',
        text: 'جلسة',
        actionDate: '10/8/2026',
      }),
    );
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('يعرض خطأ الخلفية عند فشل تغيير اليوم', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: [] });
    (api.put as unknown as ReturnType<typeof vi.fn>).mockRejectedValue({
      message: 'تاريخ الإجراء غير صالح — استخدم مثال: 1/8/2026',
    });
    renderModal([docOccurrence()]);

    await user.click(screen.getByRole('button', { name: 'تغيير اليوم' }));
    await user.click(screen.getByRole('button', { name: 'حفظ اليوم' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('تاريخ الإجراء غير صالح');
  });

  it('يلغي تذكير الملف بتأكيد صريح', async () => {
    const user = userEvent.setup();
    (api.delete as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({});
    const { onChanged } = renderModal([docOccurrence()]);

    await user.click(screen.getByRole('button', { name: 'إلغاء التذكير' }));
    expect(screen.getByRole('alert')).toHaveTextContent('لا يمكن التراجع');
    await user.click(screen.getByRole('button', { name: 'تأكيد الحذف' }));

    expect(api.delete).toHaveBeenCalledWith('/documents/5/actions/1/reminder');
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('يعلّم التكرار الشخصي منجزًا ويعيده', async () => {
    const user = userEvent.setup();
    (api.patch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const { onChanged } = renderModal([personalOccurrence()]);

    await user.click(screen.getByRole('button', { name: 'تم' }));
    expect(api.patch).toHaveBeenCalledWith(
      '/personal-reminders/7/occurrences',
      expect.objectContaining({ occurrenceDate: DAY, done: true }),
    );
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('يعرض المنجز مشطوبًا مع زر الإعادة', async () => {
    const user = userEvent.setup();
    (api.patch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const { onChanged } = renderModal([personalOccurrence(true)]);

    expect(screen.getByText('منجز')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'إعادة التكرار' }));
    expect(api.patch).toHaveBeenCalledWith(
      '/personal-reminders/7/occurrences',
      expect.objectContaining({ occurrenceDate: DAY, done: false }),
    );
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('يمسح الملاحظة من التعديل بسلسلة فارغة', async () => {
    const user = userEvent.setup();
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    renderModal([personalOccurrence()]);

    await user.click(screen.getByRole('button', { name: 'تعديل' }));
    await user.clear(screen.getByLabelText('ملاحظة'));
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    expect(api.put).toHaveBeenCalledWith(
      '/personal-reminders/7',
      expect.objectContaining({ notes: '' }),
    );
  });

  it('يحفظ غياب اللون دون زرع افتراضي', async () => {
    const user = userEvent.setup();
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const occ = personalOccurrence();
    occ.personal = { ...occ.personal!, color: null };
    renderModal([occ]);

    await user.click(screen.getByRole('button', { name: 'تعديل' }));
    expect(screen.getByLabelText('اللون')).toHaveValue('');
    await user.click(screen.getByRole('button', { name: 'حفظ التعديل' }));

    expect(api.put).toHaveBeenCalledWith(
      '/personal-reminders/7',
      expect.objectContaining({ color: '' }),
    );
  });

  it('يعرض حالة فراغ موجَّهة بلا مواعيد', () => {
    renderModal([]);
    expect(screen.getByText('لا توجد تذكيرات لهذا اليوم — أضف تذكيرًا شخصيًا')).toBeInTheDocument();
  });
});
