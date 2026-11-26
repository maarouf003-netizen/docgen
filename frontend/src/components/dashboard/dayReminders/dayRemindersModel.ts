import type { AppealReminderDto, ReminderDto } from '../../../types';
import { richToPlainText } from '../../../utils/richText';
import { appealReminderTitle, borrowerFullName } from '../dashboardFormat';
import { parseDayKey, toDayKey, type CalendarOccurrence } from '../personalReminders';

/** هدف الحذف المعلّق في نافذة اليوم (ملف/استئناف/تذكير شخصي). */
export type DeletingTarget =
  | { kind: 'document'; actionId: number; documentId: number; title: string }
  | { kind: 'appeal'; actionId: number; appealId: number; title: string }
  | { kind: 'personal'; id: number; title: string };

/** خيارات «انتهاء التكرار» في التذكير الشخصي (أيام من يوم التذكير). */
export const END_OPTIONS = [
  { value: '', label: 'بلا نهاية' },
  { value: '7', label: 'بعد أسبوع' },
  { value: '30', label: 'بعد شهر' },
  { value: '90', label: 'بعد 3 أشهر' },
  { value: '365', label: 'بعد سنة' },
];

/** اليوم الأخير للتكرار: يوم التذكير + عدد الأيام، أو `null` عند «بلا نهاية». */
export function endDateFrom(dayKey: string, days: string): string | null {
  if (!days) return null;
  const base = parseDayKey(dayKey);
  if (!base) return null;
  base.setDate(base.getDate() + Number(days));
  return toDayKey(base);
}

/**
 * تصفية تذكيرات اليوم بالنص: عنوان/ملاحظة الشخصي، واسم الملف أو عنوان الاستئناف،
 * ونص الإجراء مجرَّدًا من وسومه الغنية. البحث الجزئي حرفي (كما كان في النافذة).
 */
export function filterOccurrences(
  occurrences: CalendarOccurrence[],
  query: string,
): CalendarOccurrence[] {
  if (!query) return occurrences;
  return occurrences.filter((o) => {
    const haystack = [
      o.kind === 'personal' ? o.personal?.title : null,
      o.kind === 'personal' ? o.personal?.notes : null,
      o.reminder && 'borrowerName' in o.reminder ? borrowerFullName(o.reminder as ReminderDto) : null,
      o.reminder && 'appealTitle' in o.reminder ? appealReminderTitle(o.reminder as AppealReminderDto) : null,
      // نص الإجراء غني (`<p>…</p>`) فيُجرَّد قبل البحث كما في العرض.
      o.reminder?.actionText ? richToPlainText(o.reminder.actionText) : null,
    ]
      .filter(Boolean)
      .join(' ');
    return haystack.includes(query);
  });
}

/** مفتاح تعطيل زر الحذف أثناء تنفيذ العملية. */
export function deleteTargetKey(target: DeletingTarget): string {
  return `${target.kind}-${target.kind === 'personal' ? target.id : target.actionId}`;
}

/** مفتاح تعطيل زر «تم/إعادة التكرار» للتذكير الشخصي. */
export function personalDoneKey(id: number): string {
  return `personal-done-${id}`;
}
