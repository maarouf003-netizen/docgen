import { describe, expect, it } from 'vitest';
import type { AppealReminderDto, PersonalReminderDto, ReminderDto } from '../../../types';
import type { CalendarOccurrence } from '../personalReminders';
import {
  deleteTargetKey,
  endDateFrom,
  filterOccurrences,
  personalDoneKey,
} from './dayRemindersModel';

const DAY = '2026-08-08';

function docOccurrence(actionText = 'مراجعة دائرة التنفيذ'): CalendarOccurrence {
  const reminder: ReminderDto = {
    actionId: 1,
    documentId: 5,
    borrowerName: 'سامر',
    borrowerFather: 'محمد',
    borrowerFamily: 'حسن',
    actionText,
    actionDate: '2026-08-01',
    reminderDuration: '3 أيام',
    reminderColor: 'أحمر',
    dueDate: DAY,
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
    documentLabel: 'شركة بركات التجارية',
    actionText: 'جلسة',
    dueDate: DAY,
    dueDateSuspect: false,
  };
  return { dayKey: DAY, kind: 'appeal', reminder };
}

function personalOccurrence(): CalendarOccurrence {
  const personal: PersonalReminderDto = {
    id: 7,
    title: 'مراجعة دائرة التنفيذ',
    notes: 'ملاحظة خاصة',
    dueDate: DAY,
    color: 'زمردي',
    recurrence: 'مرة واحدة',
    recurrenceEnd: null,
    isArchived: false,
    completedOccurrenceKeys: [],
    createdAt: '2026-08-01',
    version: 9,
  };
  return { dayKey: DAY, kind: 'personal', personal };
}

describe('dayRemindersModel', () => {
  it('يحسب نهاية التكرار من يوم التذكير، ويعيد null لـ«بلا نهاية»', () => {
    expect(endDateFrom(DAY, '7')).toBe('2026-08-15');
    expect(endDateFrom(DAY, '30')).toBe('2026-09-07');
    expect(endDateFrom(DAY, '')).toBeNull();
  });

  it('يبقي كل التذكيرات عند بحث فارغ', () => {
    const all = [docOccurrence(), appealOccurrence(), personalOccurrence()];
    expect(filterOccurrences(all, '')).toHaveLength(3);
  });

  it('يبحث في اسم الملف وعنوان الاستئناف والتذكير الشخصي وملاحظاته', () => {
    const all = [docOccurrence(), appealOccurrence(), personalOccurrence()];

    expect(filterOccurrences(all, 'حسن')).toEqual([all[0]]);
    expect(filterOccurrences(all, 'القرار 9')).toEqual([all[1]]);
    expect(filterOccurrences(all, 'خاصة')).toEqual([all[2]]);
  });

  it('يجرّد نص الإجراء الغني قبل البحث', () => {
    const occurrence = docOccurrence('<p>تبليغ <strong>الخصم</strong></p>');
    expect(filterOccurrences([occurrence], 'الخصم')).toEqual([occurrence]);
    expect(filterOccurrences([occurrence], '<strong>')).toEqual([]);
  });

  it('يعيد قائمة فارغة عند عدم التطابق', () => {
    expect(filterOccurrences([docOccurrence(), personalOccurrence()], 'غير موجود')).toEqual([]);
  });

  it('يبني مفاتيح مستقرة لتعطيل الأزرار أثناء العمليات', () => {
    expect(deleteTargetKey({ kind: 'document', actionId: 3, documentId: 5, title: 'س' })).toBe('document-3');
    expect(deleteTargetKey({ kind: 'appeal', actionId: 4, appealId: 9, title: 'س' })).toBe('appeal-4');
    expect(deleteTargetKey({ kind: 'personal', id: 7, title: 'س' })).toBe('personal-7');
    expect(personalDoneKey(7)).toBe('personal-done-7');
  });
});
