import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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
});
