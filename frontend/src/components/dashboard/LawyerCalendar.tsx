import { useMemo, useState } from 'react';
import Calendar from 'react-calendar';
import 'react-calendar/dist/Calendar.css';
import './calendar.css';
import type { AppealReminderDto, PersonalReminderDto, ReminderDto } from '../../types';
import { MONTHS, isDueTodayOrOverdue } from './dashboardFormat';
import {
  dotTone,
  groupRemindersByDay,
  toDayKey,
} from './personalReminders';

/** أسماء الأيام الكاملة (ترويسة التقويم) — بترتيب `getDay()` (الأحد=0). */
const WEEKDAYS_FULL = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];
/** أسماء الأيام المختصرة المعروضة. */
const WEEKDAYS_SHORT = ['أحد', 'اثنين', 'ثلاثاء', 'أربعاء', 'خميس', 'جمعة', 'سبت'];

/** أول أيام الأسبوع: الأحد (`gregory`) أو الاثنين (`iso8601`) — قيد المكتبة. */
export type WeekStart = 'sunday' | 'monday';

/**
 * تقويم المحامي العربي (`react-calendar` بثيم زمردي):
 * - التسميات من مصفوفات المشروع (`MONTHS` الشامية + أيام عربية) لا `Intl` الخام.
 * - بداية الأسبوع من التفضيل (الأحد `gregory` / الاثنين `iso8601` — قيد المكتبة).
 * - أيام التذكيرات مميزة، ونقاط بعددها بألوانها؛ اليوم/المتأخر بتنبيه أحمر خفيف.
 */
export function LawyerCalendar({
  reminders,
  personal,
  weekStartsOn,
  selectedDay,
  onSelectDay,
  initialMonth,
}: {
  reminders: readonly (ReminderDto | AppealReminderDto)[];
  personal: readonly PersonalReminderDto[];
  weekStartsOn: WeekStart;
  selectedDay: string | null;
  onSelectDay: (dayKey: string) => void;
  /** أول شهر معروض (للاختبارات) — الافتراضي الشهر الحالي. */
  initialMonth?: Date;
}) {
  const [activeStartDate, setActiveStartDate] = useState<Date>(() => initialMonth ?? new Date());

  const { from, to } = useMemo(() => {
    const start = new Date(activeStartDate.getFullYear(), activeStartDate.getMonth(), 1);
    const end = new Date(activeStartDate.getFullYear(), activeStartDate.getMonth() + 1, 0);
    // هامش يغطي أيام الشهور المجاورة المعروضة في الشبكة.
    return {
      from: new Date(start.getFullYear(), start.getMonth(), start.getDate() - 10),
      to: new Date(end.getFullYear(), end.getMonth(), end.getDate() + 10),
    };
  }, [activeStartDate]);

  const groups = useMemo(
    () => groupRemindersByDay(reminders, personal, from, to),
    [reminders, personal, from, to],
  );

  const todayKey = toDayKey(new Date());

  return (
    <div className="lawyer-calendar" dir="rtl">
      <Calendar
        locale="ar"
        calendarType={weekStartsOn === 'sunday' ? 'gregory' : 'iso8601'}
        activeStartDate={activeStartDate}
        onActiveStartDateChange={({ activeStartDate: next }) => {
          if (next) setActiveStartDate(next);
        }}
        formatMonth={(_locale, date) => MONTHS[date.getMonth()]}
        formatMonthYear={(_locale, date) => `${MONTHS[date.getMonth()]} ${date.getFullYear()}`}
        formatWeekday={(_locale, date) => WEEKDAYS_FULL[date.getDay()]}
        formatShortWeekday={(_locale, date) => WEEKDAYS_SHORT[date.getDay()]}
        formatDay={(_locale, date) => String(date.getDate())}
        prevAriaLabel="الشهر السابق"
        prev2AriaLabel="السنة السابقة"
        nextAriaLabel="الشهر التالي"
        next2AriaLabel="السنة التالية"
        navigationAriaLabel="التنقل بين الشهور"
        tileClassName={({ date, view }) => {
          if (view !== 'month') return null;
          const key = toDayKey(date);
          const classes: string[] = [];
          if (key === selectedDay) classes.push('react-calendar__tile--active');
          // المنجزة لا تلوّن البلاطة ولا تنبّه (تظهر مشطوبة في نافذة اليوم فقط).
          const items = (groups.get(key) ?? []).filter((o) => !o.done);
          if (items.length > 0) {
            const urgent =
              key <= todayKey &&
              items.some((o) =>
                o.kind === 'personal'
                  ? true
                  : o.reminder
                    ? isDueTodayOrOverdue(o.reminder.dueDate)
                    : false,
              );
            classes.push(urgent ? 'has-urgent' : 'has-reminders');
          }
          return classes.length > 0 ? classes : null;
        }}
        tileContent={({ date, view }) => {
          if (view !== 'month') return null;
          const items = (groups.get(toDayKey(date)) ?? []).filter((o) => !o.done);
          if (items.length === 0) return null;
          const shown = items.slice(0, 4);
          return (
            <span className="flex items-center justify-center gap-0.5" aria-hidden="true">
              {shown.map((o, i) => (
                <span
                  key={`${o.kind}-${o.reminder?.actionId ?? o.personal?.id ?? i}`}
                  className={`w-1.5 h-1.5 rounded-full ${
                    o.kind === 'personal' ? dotTone(o.personal?.color ?? 'زمردي') : dotTone(o.reminder?.reminderColor)
                  }`}
                />
              ))}
              {items.length > shown.length ? (
                <span className="text-[10px] leading-none text-gray-500 tabular-nums">
                  +{items.length - shown.length}
                </span>
              ) : null}
            </span>
          );
        }}
        onClickDay={(date) => onSelectDay(toDayKey(date))}
      />
    </div>
  );
}
