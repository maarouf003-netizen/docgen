import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReminderList } from './ReminderList';
import type { AppealReminderDto, ReminderDto } from '../../types';

vi.mock('react-router-dom', async (importOriginal) => {
  const original = await importOriginal<typeof import('react-router-dom')>();
  return { ...original, Link: ({ to, children, ...rest }: any) => <a href={to} {...rest}>{children}</a> };
});

function makeFileReminder(overrides: Partial<ReminderDto> = {}): ReminderDto {
  return {
    actionId: 1,
    documentId: 11,
    documentType: 'متداول',
    actionText: 'متابعة',
    actionDate: '2026-08-01',
    reminderDuration: 'أسبوع',
    dueDate: '2026-08-08',
    dueDateSuspect: false,
    ...overrides,
  };
}

function makeAppealReminder(overrides: Partial<AppealReminderDto> = {}): AppealReminderDto {
  return {
    actionId: 2,
    appealId: 5,
    documentId: 11,
    appealTitle: 'استئناف القرار',
    documentLabel: 'شركة بركات التجارية',
    actionText: 'متابعة',
    actionDate: '2026-08-01',
    reminderDuration: 'أسبوع',
    dueDate: '2026-08-08',
    dueDateSuspect: false,
    ...overrides,
  };
}

describe('ReminderList', () => {
  it('يعرض شارة «تاريخ مشتبه» لتذكير الملف الموسوم ويخفيها للسليم', () => {
    render(
      <ReminderList
        reminders={[makeFileReminder({ dueDateSuspect: true }), makeFileReminder({ actionId: 3, dueDateSuspect: false })]}
      />,
    );

    expect(screen.getAllByText('تاريخ مشتبه — تحقق')).toHaveLength(1);
  });

  it('يعرض شارة «تاريخ مشتبه» لتذكير الاستئناف الموسوم', () => {
    render(<ReminderList reminders={[]} appealReminders={[makeAppealReminder({ dueDateSuspect: true })]} />);

    expect(screen.getByText('تاريخ مشتبه — تحقق')).toBeInTheDocument();
  });

  it('لا يعرض أي شارة اشتباه بلا وسم', () => {
    render(
      <ReminderList reminders={[makeFileReminder()]} appealReminders={[makeAppealReminder()]} />,
    );

    expect(screen.queryByText('تاريخ مشتبه — تحقق')).not.toBeInTheDocument();
  });

  it('يعرض اسم الملف في تذكير الاستئناف قبل نوعه', () => {
    render(<ReminderList reminders={[]} appealReminders={[makeAppealReminder()]} />);

    expect(screen.getByText(/شركة بركات التجارية/)).toBeInTheDocument();
  });

  it('يعرض اسم الملف الموحّد لتذكير ملف «منفذ عليه» بدل نوع المستند', () => {
    render(
      <ReminderList
        reminders={[makeFileReminder({ documentType: 'متداول', documentLabel: 'شركة بركات التجارية' })]}
      />,
    );

    expect(screen.getByText('شركة بركات التجارية')).toBeInTheDocument();
    expect(screen.queryByText('متداول')).not.toBeInTheDocument();
  });

  it('يعرض النص الكامل في نافذة «عرض التذكير» ويفتح الإجراءات', async () => {
    const user = userEvent.setup();
    const onOpenActions = vi.fn();
    const fullText = 'نص التذكير الكامل الذي كان مقتطعًا في القائمة';

    render(
      <ReminderList
        reminders={[makeFileReminder({ actionText: `<p>${fullText}</p>` })]}
        onOpenActions={onOpenActions}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'عرض التذكير' }));
    const dialog = screen.getByRole('dialog', { name: 'عرض التذكير' });
    expect(within(dialog).getByText(fullText)).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'فتح الإجراءات والملاحظات' }));
    expect(onOpenActions).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog', { name: 'عرض التذكير' })).not.toBeInTheDocument();
  });
});
