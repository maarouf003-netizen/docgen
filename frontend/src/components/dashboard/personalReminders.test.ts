import { describe, it, expect } from 'vitest';
import {
  currentWeekRange,
  dayKeyOf,
  dotTone,
  expandPersonalOccurrences,
  expandPersonalReminder,
  groupRemindersByDay,
  hasPendingOccurrenceOnOrBefore,
  nextOccurrenceKey,
  parseDayKey,
  toDayKey,
} from './personalReminders';
import type { PersonalReminderDto, ReminderDto } from '../../types';

function personal(overrides: Partial<PersonalReminderDto> = {}): PersonalReminderDto {
  return {
    id: 1,
    title: 'تذكير',
    notes: null,
    dueDate: '2026-08-08',
    color: 'أحمر',
    recurrence: 'مرة واحدة',
    recurrenceEnd: null,
    isArchived: false,
    completedOccurrenceKeys: [],
    createdAt: '2026-08-01',
    ...overrides,
  };
}

function fileReminder(dueDate: string): ReminderDto {
  return {
    actionId: 1,
    documentId: 5,
    actionText: 'مراجعة',
    dueDate,
    dueDateSuspect: false,
  };
}

describe('toDayKey / parseDayKey', () => {
  it('يبني المفتاح من المكونات المحلية دون انزياح', () => {
    expect(toDayKey(new Date(2026, 7, 8))).toBe('2026-08-08');
    expect(parseDayKey('2026-08-08')?.getDate()).toBe(8);
  });

  it('يرفض المفاتيح الفاسدة', () => {
    expect(parseDayKey('2026-13-01')).toBeNull();
    expect(parseDayKey('2026-02-30')).toBeNull();
    expect(parseDayKey('ليس تاريخًا')).toBeNull();
    expect(parseDayKey('')).toBeNull();
  });

  it('يطبّع `ISO` بوقت لنفس اليوم المحلي', () => {
    expect(dayKeyOf('2026-08-08T00:00:00')).toBe('2026-08-08');
    expect(dayKeyOf('2026-08-08')).toBe('2026-08-08');
    expect(dayKeyOf('فاسد')).toBeNull();
  });
});

describe('expandPersonalReminder', () => {
  const from = new Date(2026, 7, 1);
  const to = new Date(2026, 7, 31);

  it('المرة الواحدة تُرجع يوم الاستحقاق وحده', () => {
    expect(expandPersonalReminder(personal(), from, to)).toEqual(['2026-08-08']);
  });

  it('اليومي يملأ النافذة ويحترم النهاية', () => {
    const keys = expandPersonalReminder(
      personal({ recurrence: 'يومي', dueDate: '2026-08-29', recurrenceEnd: '2026-08-30' }),
      from,
      to,
    );
    expect(keys).toEqual(['2026-08-29', '2026-08-30']);
  });

  it('الأسبوعي بخطوات 7 أيام', () => {
    const keys = expandPersonalReminder(
      personal({ recurrence: 'أسبوعي', dueDate: '2026-08-01' }),
      from,
      to,
    );
    expect(keys).toEqual(['2026-08-01', '2026-08-08', '2026-08-15', '2026-08-22', '2026-08-29']);
  });

  it('الشهري يثبّت 31 على آخر الشهر (28 في 2026)', () => {
    const keys = expandPersonalReminder(
      personal({ recurrence: 'شهري', dueDate: '2026-01-31' }),
      new Date(2026, 0, 1),
      new Date(2026, 2, 31),
    );
    expect(keys).toEqual(['2026-01-31', '2026-02-28', '2026-03-31']);
  });

  it('يستبعد المنجزة والمؤرشفة وخارج النافذة', () => {
    expect(
      expandPersonalReminder(personal({ completedOccurrenceKeys: ['2026-08-08'] }), from, to),
    ).toEqual([]);
    expect(expandPersonalReminder(personal({ isArchived: true }), from, to)).toEqual([]);
    expect(expandPersonalReminder(personal({ dueDate: '2026-09-05' }), from, to)).toEqual([]);
    expect(expandPersonalReminder(personal({ recurrenceEnd: '2026-08-07' }), from, to)).toEqual([]);
  });
});

describe('groupRemindersByDay', () => {
  it('يدمج المصادر ويستبعد الفاسد', () => {
    const groups = groupRemindersByDay(
      [fileReminder('2026-08-08T00:00:00'), fileReminder('فاسد')],
      [personal({ dueDate: '2026-08-08' })],
      new Date(2026, 7, 1),
      new Date(2026, 7, 31),
    );
    const day = groups.get('2026-08-08');
    expect(day?.map((o) => o.kind).sort()).toEqual(['document', 'personal']);
    expect(groups.size).toBe(1);
  });

  it('يضمّن المنجزة موسومة (للتراجع) لا مسقطة', () => {
    const groups = groupRemindersByDay(
      [],
      [personal({ dueDate: '2026-08-08', completedOccurrenceKeys: ['2026-08-08'] })],
      new Date(2026, 7, 1),
      new Date(2026, 7, 31),
    );
    const day = groups.get('2026-08-08');
    expect(day).toHaveLength(1);
    expect(day?.[0].done).toBe(true);
  });
});

describe('expandPersonalOccurrences', () => {
  it('يوسم المنجزة دون إسقاطها', () => {
    const out = expandPersonalOccurrences(
      personal({ recurrence: 'يومي', dueDate: '2026-08-08', completedOccurrenceKeys: ['2026-08-09'] }),
      new Date(2026, 7, 8),
      new Date(2026, 7, 10),
    );
    expect(out).toEqual([
      { key: '2026-08-08', done: false },
      { key: '2026-08-09', done: true },
      { key: '2026-08-10', done: false },
    ]);
  });
});

describe('hasPendingOccurrenceOnOrBefore', () => {
  const today = new Date(2026, 7, 10);
  it('مرة واحدة ماضية غير منجزة تُنبّه، والمستقبلية والمنجزة والمؤرشفة لا', () => {
    expect(hasPendingOccurrenceOnOrBefore(personal({ dueDate: '2026-08-05' }), today)).toBe(true);
    expect(hasPendingOccurrenceOnOrBefore(personal({ dueDate: '2026-08-15' }), today)).toBe(false);
    expect(
      hasPendingOccurrenceOnOrBefore(
        personal({ dueDate: '2026-08-05', completedOccurrenceKeys: ['2026-08-05'] }),
        today,
      ),
    ).toBe(false);
    expect(hasPendingOccurrenceOnOrBefore(personal({ isArchived: true }), today)).toBe(false);
    expect(hasPendingOccurrenceOnOrBefore(personal({ dueDate: 'فاسد' }), today)).toBe(false);
  });

  it('اليومي يحسب الوقوعات الماضية غير المنجزة', () => {
    expect(
      hasPendingOccurrenceOnOrBefore(
        personal({ recurrence: 'يومي', dueDate: '2026-08-01', completedOccurrenceKeys: ['2026-08-10'] }),
        today,
      ),
    ).toBe(true);
    expect(
      hasPendingOccurrenceOnOrBefore(
        personal({
          recurrence: 'يومي',
          dueDate: '2026-08-01',
          recurrenceEnd: '2026-08-05',
          completedOccurrenceKeys: ['2026-08-01', '2026-08-02', '2026-08-03', '2026-08-04', '2026-08-05'],
        }),
        today,
      ),
    ).toBe(false);
  });
});

describe('dotTone', () => {
  it('يُرجع لونًا ثابتًا لكل قيمة معروفة ورماديًا للغريب', () => {
    expect(dotTone('أحمر')).toBe('bg-red-500');
    expect(dotTone('زمردي')).toBe('bg-emerald-500');
    expect(dotTone('أخضر')).toBe('bg-gray-400');
    expect(dotTone(null)).toBe('bg-gray-400');
  });
});

describe('currentWeekRange', () => {
  it('أسبوع من الأحد إلى السبت يحوي اليوم المحدد', () => {
    // 2026-08-08 يوم سبت → الأسبوع من 2 آب إلى 8 آب.
    const range = currentWeekRange(new Date(2026, 7, 8));
    expect(range.fromKey).toBe('2026-08-02');
    expect(range.toKey).toBe('2026-08-08');
    expect(range.fromDate.getDay()).toBe(0);
  });

  it('يعبر حد الشهر والسنة', () => {
    // الخميس 1 كانون الثاني 2026 → الأسبوع من 28 كانون الأول 2025.
    const range = currentWeekRange(new Date(2026, 0, 1));
    expect(range.fromKey).toBe('2025-12-28');
    expect(range.toKey).toBe('2026-01-03');
  });
});

describe('nextOccurrenceKey', () => {
  it('يُرجع أول وقوع غير منجز بدءًا من اليوم', () => {
    expect(nextOccurrenceKey(personal({ dueDate: '2026-08-08' }), new Date(2026, 7, 1))).toBe('2026-08-08');
    expect(
      nextOccurrenceKey(
        personal({ recurrence: 'يومي', dueDate: '2026-08-01', completedOccurrenceKeys: ['2026-08-05'] }),
        new Date(2026, 7, 5),
      ),
    ).toBe('2026-08-06');
  });

  it('يُرجع `undefined` عند الانتهاء أو الأرشفة', () => {
    expect(nextOccurrenceKey(personal({ dueDate: '2026-08-01' }), new Date(2026, 7, 10))).toBeUndefined();
    expect(nextOccurrenceKey(personal({ isArchived: true }), new Date(2026, 7, 1))).toBeUndefined();
  });
});
