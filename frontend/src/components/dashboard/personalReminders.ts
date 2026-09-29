import type { AppealReminderDto, PersonalReminderDto, ReminderDto } from '../../types';
import { MONTHS } from './dashboardFormat';

export type CalendarSource = 'document' | 'appeal' | 'personal';

export interface CalendarOccurrence {
  /** مفتاح اليوم `yyyy-MM-dd` (بتوقيت المتصفح المحلي حصرًا). */
  dayKey: string;
  kind: CalendarSource;
  reminder?: ReminderDto | AppealReminderDto;
  personal?: PersonalReminderDto;
  /** تكرار منجز — يُخفى من البلاطات والأجراس ويظهر مشطوبًا في نافذة اليوم مع التراجع. */
  done?: boolean;
}

/** وقوع موسّع مع علامة الإنجاز. */
export interface PersonalOccurrence {
  key: string;
  done: boolean;
}

/** مفتاح اليوم من مكونات التاريخ المحلية — ممنوع `toISOString()` (يزيح اليوم في التوقيتات الموجبة). */
export function toDayKey(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** تحليل صارم لمفتاح `yyyy-MM-dd` — `null` للفاسد. */
export function parseDayKey(key: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(key.trim());
  if (!match) return null;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  if (
    date.getFullYear() !== Number(match[1]) ||
    date.getMonth() !== Number(match[2]) - 1 ||
    date.getDate() !== Number(match[3])
  )
    return null;
  return date;
}

/** مفتاح اليوم من نص `dueDate` (قد يأتي `ISO` بوقت) — `null` للفاسد. */
export function dayKeyOf(value: string): string | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return toDayKey(date);
}

/** أسماء الأيام الكاملة بترتيب `getDay()` (الأحد=0). */
const WEEKDAYS_FULL = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];

/** تسمية اليوم الكاملة: «السبت 8 آب 2026». */
export function dayLabelOf(dayKey: string): string {
  const date = parseDayKey(dayKey);
  if (!date) return dayKey;
  return `${WEEKDAYS_FULL[date.getDay()]} ${date.getDate()} ${MONTHS[date.getMonth()]} ${date.getFullYear()}`;
}

function daysInMonth(year: number, monthIndex: number): number {
  return new Date(year, monthIndex + 1, 0).getDate();
}

/**
 * توسيع التكرار لأيام العرض ضمن النافذة `[from, to]` — عرض فقط:
 * - `مرة واحدة`: يوم الاستحقاق وحده.
 * - `يومي`/`أسبوعي`: خطوات ثابتة من يوم الاستحقاق.
 * - `شهري`: اليوم نفسه من كل شهر، ويُثبَّت على آخر الشهر عند غيابه (31 ← 28/29/30).
 * يُحترم `recurrenceEnd` وتُستبعد المنجزة والمؤرشفة.
 */
export function expandPersonalReminder(
  reminder: PersonalReminderDto,
  from: Date,
  to: Date,
): string[] {
  return expandPersonalOccurrences(reminder, from, to)
    .filter((o) => !o.done)
    .map((o) => o.key);
}

/**
 * التوسيع الكامل موسومًا بالإنجاز — لنافذة اليوم (تعرض المنجز مشطوبًا مع التراجع).
 * البلاطات والأجراس تستعمل `expandPersonalReminder` (غير المنجز فقط).
 */
export function expandPersonalOccurrences(
  reminder: PersonalReminderDto,
  from: Date,
  to: Date,
): PersonalOccurrence[] {
  if (reminder.isArchived) return [];
  const start = new Date(reminder.dueDate);
  if (Number.isNaN(start.getTime())) return [];
  start.setHours(0, 0, 0, 0);
  const end = new Date(to);
  end.setHours(0, 0, 0, 0);
  const windowStart = new Date(from);
  windowStart.setHours(0, 0, 0, 0);

  let hardEnd = new Date(end);
  if (reminder.recurrenceEnd) {
    const cap = new Date(reminder.recurrenceEnd);
    if (Number.isNaN(cap.getTime())) return [];
    cap.setHours(0, 0, 0, 0);
    if (cap < hardEnd) hardEnd = cap;
  }
  if (start > hardEnd) return [];

  const done = new Set(
    (reminder.completedOccurrenceKeys ?? [])
      .map((k) => k.trim())
      .filter((k) => k.length > 0),
  );

  const occurrences: PersonalOccurrence[] = [];
  const push = (date: Date) => {
    if (date < windowStart || date > hardEnd) return;
    const key = toDayKey(date);
    occurrences.push({ key, done: done.has(key) });
  };

  switch (reminder.recurrence) {
    case 'يومي': {
      for (let cursor = new Date(start); cursor <= hardEnd; cursor.setDate(cursor.getDate() + 1))
        push(new Date(cursor));
      break;
    }
    case 'أسبوعي': {
      for (let cursor = new Date(start); cursor <= hardEnd; cursor.setDate(cursor.getDate() + 7))
        push(new Date(cursor));
      break;
    }
    case 'شهري': {
      const day = start.getDate();
      let y = start.getFullYear();
      let m = start.getMonth();
      for (;;) {
        const clamped = new Date(y, m, Math.min(day, daysInMonth(y, m)));
        if (clamped < start) {
          // دفاعي: لا يتقدم قبل البداية أبدًا.
        } else if (clamped > hardEnd) {
          break;
        } else {
          push(clamped);
        }
        m += 1;
        if (m > 11) {
          m = 0;
          y += 1;
        }
        // حارس اللانهاية: قرن كامل كافٍ لأي نافذة عرض.
        if (y - start.getFullYear() > 100) break;
      }
      break;
    }
    default: {
      // `مرة واحدة` وأي قيمة غير معروفة تُعامل كحدث مفرد (لا تُسقط التذكير).
      push(new Date(start));
      break;
    }
  }
  return occurrences;
}

/**
 * دمج المصادر الثلاثة بمفتاح اليوم — المصدر الوحيد لتلوين التقويم ونافذة اليوم وجرسها.
 * التذكيرات الفاسدة التاريخ تُستبعد بصمت (لا تُكسر التقويم).
 */
export function groupRemindersByDay(
  reminders: readonly (ReminderDto | AppealReminderDto)[],
  personal: readonly PersonalReminderDto[],
  from: Date,
  to: Date,
): Map<string, CalendarOccurrence[]> {
  const groups = new Map<string, CalendarOccurrence[]>();
  const add = (dayKey: string, occurrence: CalendarOccurrence) => {
    const list = groups.get(dayKey);
    if (list) list.push(occurrence);
    else groups.set(dayKey, [occurrence]);
  };

  for (const r of reminders) {
    const key = dayKeyOf(r.dueDate);
    if (!key) continue;
    add(key, {
      dayKey: key,
      kind: 'appealId' in r ? 'appeal' : 'document',
      reminder: r,
    });
  }

  for (const p of personal) {
    // المنجزة تُضمَّن موسومة: نافذة اليوم تعرضها مشطوبة مع التراجع،
    // والبلاطات والأجراس تتجاهلها (تُصفّى عبر `!o.done`).
    for (const occurrence of expandPersonalOccurrences(p, from, to))
      add(occurrence.key, { dayKey: occurrence.key, kind: 'personal', personal: p, done: occurrence.done });
  }

  return groups;
}

/**
 * هل للتذكير وقوع غير منجز بتاريخ `day` أو قبله؟ — لجرس التقويم (المعيار: اليوم/متأخر).
 * المؤرشف والفاسد واللاحق كلها `false`.
 */
export function hasPendingOccurrenceOnOrBefore(reminder: PersonalReminderDto, day: Date): boolean {
  if (reminder.isArchived) return false;
  const start = new Date(reminder.dueDate);
  if (Number.isNaN(start.getTime())) return false;
  start.setHours(0, 0, 0, 0);
  const end = new Date(day);
  end.setHours(0, 0, 0, 0);
  if (start > end) return false;
  return expandPersonalReminder(reminder, new Date(2000, 0, 1), end).length > 0;
}

/** أول وقوع غير منجز بدءًا من اليوم (ضمن سنة) — `undefined` عند الانتهاء أو الأرشفة. */
export function nextOccurrenceKey(reminder: PersonalReminderDto, from: Date = new Date()): string | undefined {
  const to = new Date(from);
  to.setDate(to.getDate() + 365);
  return expandPersonalReminder(reminder, from, to)[0];
}

/** نطاق الأسبوع الحالي (مفاتيح + تواريخ) — يبدأ الأحد. */
export function currentWeekRange(today: Date = new Date()): {
  fromKey: string;
  toKey: string;
  fromDate: Date;
  toDate: Date;
} {
  const fromDate = new Date(today);
  fromDate.setHours(0, 0, 0, 0);
  fromDate.setDate(fromDate.getDate() - fromDate.getDay());
  const toDate = new Date(fromDate);
  toDate.setDate(toDate.getDate() + 6);
  return { fromKey: toDayKey(fromDate), toKey: toDayKey(toDate), fromDate, toDate };
}

/** ألوان النقاط الثابتة (لا بناء ديناميكيًا لكلاسات `Tailwind`). */
export const REMINDER_DOT_TONES: Record<string, string> = {
  'أحمر': 'bg-red-500',
  'بنفسجي': 'bg-violet-500',
  'أصفر': 'bg-amber-400',
  'زمردي': 'bg-emerald-500',
};

export function dotTone(color: string | null | undefined): string {
  return REMINDER_DOT_TONES[color ?? ''] ?? 'bg-gray-400';
}
