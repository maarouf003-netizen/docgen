import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LawyerCalendar } from './LawyerCalendar';
import type { PersonalReminderDto, ReminderDto } from '../../types';

function fileReminder(): ReminderDto {
  return {
    actionId: 1,
    documentId: 5,
    actionText: 'مراجعة',
    dueDate: '2026-08-08',
    reminderColor: 'أحمر',
    dueDateSuspect: false,
  };
}

function personal(): PersonalReminderDto {
  return {
    id: 7,
    title: 'شخصي',
    notes: null,
    dueDate: '2026-08-10',
    color: 'زمردي',
    recurrence: 'مرة واحدة',
    recurrenceEnd: null,
    isArchived: false,
    completedOccurrenceKeys: [],
    createdAt: '2026-08-01',
  };
}

describe('LawyerCalendar', () => {
  it('يعلّم أيام التذكيرات وينادي باليوم المختار عند النقر', async () => {
    const user = userEvent.setup();
    const onSelectDay = vi.fn();
    const { container } = render(
      <LawyerCalendar
        reminders={[fileReminder()]}
        personal={[personal()]}
        weekStartsOn="sunday"
        selectedDay={null}
        onSelectDay={onSelectDay}
        initialMonth={new Date(2026, 7, 1)}
      />,
    );

    // يومان مميزان: 8 (ملف) و10 (شخصي).
    expect(container.querySelectorAll('.has-reminders, .has-urgent').length).toBeGreaterThanOrEqual(2);
    // التسمية العربية للشهر من مصفوفات المشروع لا `Intl` الخام.
    expect(screen.getByText('آب 2026')).toBeInTheDocument();

    const marked = container.querySelector('.has-reminders, .has-urgent') as HTMLElement;
    await user.click(marked);
    expect(onSelectDay).toHaveBeenCalledTimes(1);
    expect(onSelectDay.mock.calls[0][0]).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it('يتجاهل التكرار المنجز في التلوين والنقاط', () => {
    const done = personal();
    done.dueDate = '2026-08-08';
    done.completedOccurrenceKeys = ['2026-08-08'];
    const { container } = render(
      <LawyerCalendar
        reminders={[]}
        personal={[done]}
        weekStartsOn="sunday"
        selectedDay={null}
        onSelectDay={() => {}}
        initialMonth={new Date(2026, 7, 1)}
      />,
    );

    expect(container.querySelectorAll('.has-reminders, .has-urgent')).toHaveLength(0);
  });

  it('يحترم أول الأسبوع (الأحد مقابل الاثنين)', () => {
    const sunday = render(
      <LawyerCalendar
        reminders={[]}
        personal={[]}
        weekStartsOn="sunday"
        selectedDay={null}
        onSelectDay={() => {}}
        initialMonth={new Date(2026, 7, 1)}
      />,
    );
    const sundayFirst = within(sunday.container)
      .getAllByText(/^(أحد|اثنين|ثلاثاء|أربعاء|خميس|جمعة|سبت)$/)[0].textContent;
    expect(sundayFirst).toBe('أحد');
    sunday.unmount();

    const monday = render(
      <LawyerCalendar
        reminders={[]}
        personal={[]}
        weekStartsOn="monday"
        selectedDay={null}
        onSelectDay={() => {}}
        initialMonth={new Date(2026, 7, 1)}
      />,
    );
    const mondayFirst = within(monday.container)
      .getAllByText(/^(أحد|اثنين|ثلاثاء|أربعاء|خميس|جمعة|سبت)$/)[0].textContent;
    expect(mondayFirst).toBe('اثنين');
    monday.unmount();
  });

  it('يتنقل بين الشهور بأزرار التنقل المسماة ويحدّث البلاطات', async () => {
    const user = userEvent.setup();
    const { container } = render(
      <LawyerCalendar
        reminders={[]}
        personal={[personal()]}
        weekStartsOn="sunday"
        selectedDay={null}
        onSelectDay={() => {}}
        initialMonth={new Date(2026, 7, 1)}
      />,
    );

    // آب: التذكير الشخصي (10 آب) مميز.
    expect(screen.getByText('آب 2026')).toBeInTheDocument();
    expect(container.querySelectorAll('.has-reminders, .has-urgent')).toHaveLength(1);

    // أيلول: لا تذكيرات.
    await user.click(screen.getByRole('button', { name: 'الشهر التالي' }));
    expect(screen.getByText('أيلول 2026')).toBeInTheDocument();
    expect(container.querySelectorAll('.has-reminders, .has-urgent')).toHaveLength(0);

    // عودة لآب: يعود التمييز.
    await user.click(screen.getByRole('button', { name: 'الشهر السابق' }));
    expect(screen.getByText('آب 2026')).toBeInTheDocument();
    expect(container.querySelectorAll('.has-reminders, .has-urgent')).toHaveLength(1);
  });
});
