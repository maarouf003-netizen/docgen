import { Link } from 'react-router-dom';
import type { AppealReminderDto, PersonalReminderDto, ReminderDto } from '../../../types';
import { richToPlainText } from '../../../utils/richText';
import { appealReminderTitle, borrowerFullName, dueLabel } from '../dashboardFormat';
import { dayKeyOf, dotTone, type CalendarOccurrence } from '../personalReminders';
import { personalDoneKey, type DeletingTarget } from './dayRemindersModel';

function RowBadge({ dueDate }: { dueDate: string }) {
  const { text, tone } = dueLabel(dueDate);
  return (
    <span className={`text-xs rounded-full border px-2 py-0.5 font-medium whitespace-nowrap ${tone}`}>
      {text}
    </span>
  );
}

/**
 * صف تذكير واحد في نافذة اليوم: التذكير الشخصي (تم/تعديل/حذف) أو تذكير ملف/استئناف
 * (رابط، شارة استحقاق، تغيير اليوم، وإلغاء التذكير). الحالة تُدار في النافذة.
 */
export function DayReminderRow({
  occurrence: o,
  rescheduling,
  rescheduleInput,
  rescheduleError,
  rescheduleSaving,
  actingKey,
  onStartReschedule,
  onRescheduleInput,
  onSaveReschedule,
  onCancelReschedule,
  onDelete,
  onToggleDone,
  onEdit,
}: {
  occurrence: CalendarOccurrence;
  rescheduling: string | null;
  rescheduleInput: string;
  rescheduleError: string;
  rescheduleSaving: boolean;
  actingKey: string | null;
  onStartReschedule: (key: string, current: string | undefined) => void;
  onRescheduleInput: (value: string) => void;
  onSaveReschedule: () => void;
  onCancelReschedule: () => void;
  onDelete: (target: DeletingTarget) => void;
  onToggleDone: (personal: PersonalReminderDto, done: boolean) => void;
  onEdit: (personal: PersonalReminderDto) => void;
}) {
  if (o.kind === 'personal' && o.personal) {
    const p = o.personal;
    const doneKey = personalDoneKey(p.id);
    const done = o.done ?? (p.completedOccurrenceKeys ?? []).includes(o.dayKey);
    return (
      <li className="py-3">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="flex flex-wrap items-center gap-2">
              <span className={`font-medium text-gray-900 break-words ${done ? 'line-through text-gray-400' : ''}`}>
                {p.title}
              </span>
              {done ? (
                <span className="text-xs rounded-full bg-gray-100 text-gray-600 border border-gray-200 px-2 py-0.5 font-medium">
                  منجز
                </span>
              ) : null}
            </p>
            {p.notes ? <p className="text-xs text-gray-500 mt-0.5 break-words line-clamp-2">{p.notes}</p> : null}
            <p className="text-xs text-gray-400 mt-1">
              تذكير شخصي · {p.recurrence}
              {p.recurrenceEnd ? ` · حتى ${p.recurrenceEnd}` : ' · بلا نهاية'}
            </p>
          </div>
          <span
            className={`w-2.5 h-2.5 rounded-full shrink-0 mt-1.5 ${dotTone(p.color)}`}
            aria-hidden="true"
          />
        </div>
        <div className="flex flex-wrap gap-2 mt-2">
          <button
            type="button"
            onClick={() => onToggleDone(p, !done)}
            disabled={actingKey === doneKey}
            className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 hover:bg-gray-50 disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            {done ? 'إعادة التكرار' : 'تم'}
          </button>
          <button
            type="button"
            onClick={() => onEdit(p)}
            className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            تعديل
          </button>
          <button
            type="button"
            onClick={() => onDelete({ kind: 'personal', id: p.id, title: p.title })}
            className="min-h-11 px-3 rounded-lg text-xs font-medium text-red-700 hover:bg-red-50 focus-visible:ring-2 focus-visible:ring-red-600"
          >
            حذف
          </button>
        </div>
      </li>
    );
  }

  if (!o.reminder) return null;
  const isAppeal = o.kind === 'appeal';
  const r = o.reminder as ReminderDto & { appealId?: number; appealTitle?: string };
  const parentId = isAppeal ? (r.appealId as number) : r.documentId;
  const linkTo = isAppeal ? `/appeals/${parentId}` : `/documents/${parentId}`;
  const title = isAppeal
    ? (r.appealTitle
        ? appealReminderTitle({ appealTitle: r.appealTitle, documentLabel: (r as AppealReminderDto).documentLabel })
        : `استئناف ${parentId}`)
    : borrowerFullName(r as ReminderDto);
  const rescheduleKey = `${o.kind}-${r.actionId}`;
  // نص الإجراء غني (`<p>…</p>`) فيُعرض نصًا عاديًا كما في `ReminderList` — الفارغ
  // بعد التجريد (مثل `<p><br></p>`) يُخفى سطره فلا واجهة مكسورة.
  const plainActionText = richToPlainText(r.actionText);

  return (
    <li className="py-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <Link to={linkTo} className="font-medium text-sky-700 hover:underline break-words">
            {title}
          </Link>
          {plainActionText ? (
            <p className="text-xs text-gray-500 mt-0.5 break-words line-clamp-2">{plainActionText}</p>
          ) : null}
        </div>
        <RowBadge dueDate={r.dueDate} />
      </div>
      {rescheduling === rescheduleKey ? (
        <div className="mt-2 rounded-xl border border-gray-200 p-3">
          <label className="block text-xs text-gray-600 mb-1.5" htmlFor={`reschedule-${rescheduleKey}`}>
            اليوم الجديد
          </label>
          <input
            id={`reschedule-${rescheduleKey}`}
            type="text"
            value={rescheduleInput}
            onChange={(e) => onRescheduleInput(e.target.value)}
            placeholder="مثال: 1/8/2026…"
            name={`reschedule-${rescheduleKey}`}
            autoComplete="off"
            aria-describedby={`reschedule-help-${rescheduleKey}`}
            className="w-full min-h-11 rounded-xl border border-gray-200 px-3 text-sm tabular-nums focus-visible:ring-2 focus-visible:ring-emerald-600"
          />
          <p id={`reschedule-help-${rescheduleKey}`} className="text-[11px] text-gray-500 mt-1">
            يوم الاستحقاق نفسه — المدة تبقى، ولا يتغيّر تاريخ الإجراء.
          </p>
          {rescheduleError ? (
            <p role="alert" className="text-red-700 text-xs mt-1.5">
              {rescheduleError}
            </p>
          ) : null}
          <div className="flex gap-2 mt-2">
            <button
              type="button"
              onClick={onSaveReschedule}
              disabled={rescheduleSaving}
              className="min-h-11 px-4 rounded-lg bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-medium disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              {rescheduleSaving ? 'جارٍ الحفظ…' : 'حفظ اليوم'}
            </button>
            <button
              type="button"
              onClick={onCancelReschedule}
              className="min-h-11 px-4 rounded-lg border border-gray-200 text-xs hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              إلغاء
            </button>
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap gap-2 mt-2">
          <button
            type="button"
            onClick={() => onStartReschedule(rescheduleKey, dayKeyOf(r.dueDate) ?? o.dayKey)}
            className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            تغيير اليوم
          </button>
          <button
            type="button"
            onClick={() =>
              onDelete(
                isAppeal
                  ? { kind: 'appeal', actionId: r.actionId, appealId: parentId, title }
                  : { kind: 'document', actionId: r.actionId, documentId: parentId, title },
              )
            }
            className="min-h-11 px-3 rounded-lg text-xs font-medium text-red-700 hover:bg-red-50 focus-visible:ring-2 focus-visible:ring-red-600"
          >
            إلغاء التذكير
          </button>
        </div>
      )}
    </li>
  );
}
