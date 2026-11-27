import { useEffect, useMemo, useRef, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import type { PersonalReminderDto, ReminderDto } from '../../types';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { dayLabelOf, type CalendarOccurrence } from './personalReminders';
import { DayReminderRow } from './dayReminders/DayReminderRow';
import {
  deleteTargetKey,
  filterOccurrences,
  personalDoneKey,
  type DeletingTarget,
} from './dayReminders/dayRemindersModel';
import { PersonalForm } from './dayReminders/PersonalReminderForm';

// يُعاد تصدير `PersonalForm` لأن `CalendarPage` يستوردها من هنا تاريخيًا.
export { PersonalForm } from './dayReminders/PersonalReminderForm';

/**
 * نافذة تذكيرات اليوم: قائمة المواعيد الثلاثة (ملف/استئناف/شخصي) مع روابطها وشاراتها،
 * ونموذج تذكير شخصي (إنشاء/تعديل)، وتغيير يوم تذكير الملف/الاستئناف (نص حر بنفس عقد
 * `ParseDateTime`)، وإلغاء التذكير بتأكيد صريح — سفلية على الجوال ووسطية على المكتبي.
 * الصف في `dayReminders/DayReminderRow`، والتصفية/النموذج في `dayReminders/*`.
 */
export function DayRemindersModal({
  dayKey,
  occurrences,
  onClose,
  onChanged,
}: {
  dayKey: string;
  occurrences: CalendarOccurrence[];
  onClose: () => void;
  onChanged: () => void;
}) {
  const [search, setSearch] = useState('');
  const [formMode, setFormMode] = useState<{ personal: PersonalReminderDto } | 'create' | null>(null);
  const [rescheduling, setRescheduling] = useState<string | null>(null);
  const [rescheduleInput, setRescheduleInput] = useState('');
  const [rescheduleError, setRescheduleError] = useState('');
  const [rescheduleSaving, setRescheduleSaving] = useState(false);
  const [deleting, setDeleting] = useState<DeletingTarget | null>(null);
  const [actionError, setActionError] = useState('');
  const [actingKey, setActingKey] = useState<string | null>(null);

  const panelRef = useRef<HTMLDivElement>(null);

  // نمط الحصر الحواري: `Escape` يغلق، والتركيز يبدأ على اللوحة نفسها (لا على زر
  // الخلفية غير المرئي) ومحصور داخل النافذة، ويعود لموضعه عند الإغلاق.
  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    panelRef.current?.focus({ preventScroll: true });
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
        return;
      }
      if (e.key !== 'Tab' || !panelRef.current) return;
      const focusables = Array.from(
        panelRef.current.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), input, select, textarea, [tabindex]:not([tabindex="-1"])',
        ),
      ).filter((el) => el.offsetParent !== null);
      if (focusables.length === 0) return;
      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement as HTMLElement | null;
      if (e.shiftKey && (active === first || !panelRef.current.contains(active))) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previouslyFocused?.focus?.();
    };
  }, [onClose]);

  const query = search.trim();
  const visible = useMemo(() => filterOccurrences(occurrences, query), [occurrences, query]);

  const startReschedule = (key: string, current: string | undefined) => {
    setRescheduling(key);
    setRescheduleInput(current ?? dayKey);
    setRescheduleError('');
  };

  const saveReschedule = async (o: CalendarOccurrence) => {
    if (o.kind === 'personal' || !o.reminder) return;
    const isAppeal = o.kind === 'appeal';
    const r = o.reminder as ReminderDto & { appealId?: number };
    const parentId = isAppeal ? (r.appealId as number) : r.documentId;
    const base = isAppeal ? `/appeals/${parentId}` : `/documents/${parentId}`;
    setRescheduleSaving(true);
    setRescheduleError('');
    try {
      // المدخل يوم الاستحقاق نفسه — الخلفية تعيد حساب تاريخ الإجراء خلفيًا
      // مع بقاء المدة (لا `GET actions` ولا `PUT` ولا حساب تواريخ هنا).
      await api.post(`${base}/actions/${r.actionId}/reschedule`, {
        newDueDate: normalizeArabicDigits(rescheduleInput).trim(),
      });
      setRescheduling(null);
      onChanged();
    } catch (err) {
      setRescheduleError(getApiErrorMessage(err));
    } finally {
      setRescheduleSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    const key = deleteTargetKey(deleting);
    setActingKey(key);
    setActionError('');
    try {
      if (deleting.kind === 'personal') {
        await api.delete(`/personal-reminders/${deleting.id}`);
      } else if (deleting.kind === 'appeal') {
        await api.delete(`/appeals/${deleting.appealId}/actions/${deleting.actionId}/reminder`);
      } else {
        await api.delete(`/documents/${deleting.documentId}/actions/${deleting.actionId}/reminder`);
      }
      setDeleting(null);
      onChanged();
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setActingKey(null);
    }
  };

  const togglePersonalDone = async (personal: PersonalReminderDto, done: boolean) => {
    const key = personalDoneKey(personal.id);
    setActingKey(key);
    setActionError('');
    try {
      await api.patch(`/personal-reminders/${personal.id}/occurrences`, {
        occurrenceDate: dayKey,
        done,
        version: personal.version,
      });
      onChanged();
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setActingKey(null);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center"
      role="dialog"
      aria-modal="true"
      aria-label={`تذكيرات ${dayLabelOf(dayKey)}`}
    >
      <button
        onClick={onClose}
        aria-label="إغلاق نافذة اليوم"
        tabIndex={-1}
        className="absolute inset-0 bg-black/50 w-full h-full cursor-default"
      />
      <div
        ref={panelRef}
        tabIndex={-1}
        className="relative bg-white w-full sm:max-w-lg max-h-[90dvh] overflow-y-auto overscroll-contain rounded-t-3xl sm:rounded-3xl shadow-xl p-4 sm:p-5 focus-visible:ring-2 focus-visible:ring-emerald-600 focus:outline-none"
      >
        <div className="flex items-center justify-between gap-3 mb-3">
          <div className="flex items-center gap-2 min-w-0">
            <h3 className="font-bold text-gray-900 text-balance">{dayLabelOf(dayKey)}</h3>
            <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium tabular-nums">
              {occurrences.length}
            </span>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            className="shrink-0 min-h-11 min-w-11 rounded-lg text-gray-500 hover:bg-gray-100 hover:text-gray-800 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            ✕
          </button>
        </div>

        <label className="block mb-3">
          <span className="sr-only">بحث في تذكيرات اليوم</span>
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث بالنص أو اسم الملف…"
            name="day-search"
            autoComplete="off"
            className="w-full min-h-11 rounded-xl border border-gray-200 px-3 text-sm focus-visible:ring-2 focus-visible:ring-emerald-600"
          />
        </label>

        {actionError ? (
          <p role="alert" className="text-red-700 text-sm bg-red-50 border border-red-100 rounded-xl px-3 py-2 mb-3">
            {actionError}
          </p>
        ) : null}

        {deleting ? (
          <div className="border border-red-200 bg-red-50 rounded-2xl p-4 mb-3" role="alert">
            <p className="text-sm text-red-900 font-medium break-words">
              حذف «{deleting.title}»{deleting.kind === 'personal' ? ' مع كل تكراراته' : ''}؟ لا يمكن التراجع.
            </p>
            <div className="flex gap-2 mt-3">
              <button
                type="button"
                onClick={confirmDelete}
                disabled={actingKey !== null}
                className="min-h-11 px-4 rounded-lg bg-red-600 hover:bg-red-700 text-white text-sm font-medium disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-red-600"
              >
                {actingKey !== null ? 'جارٍ الحذف…' : 'تأكيد الحذف'}
              </button>
              <button
                type="button"
                onClick={() => setDeleting(null)}
                className="min-h-11 px-4 rounded-lg border border-gray-200 text-sm hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
              >
                تراجع
              </button>
            </div>
          </div>
        ) : null}

        {visible.length === 0 ? (
          <div className="py-8 text-center">
            <p className="text-gray-400 text-sm mb-3">لا توجد تذكيرات لهذا اليوم — أضف تذكيرًا شخصيًا</p>
            <button
              type="button"
              onClick={() => setFormMode('create')}
              className="min-h-11 px-4 rounded-lg bg-emerald-700 hover:bg-emerald-600 text-white text-sm font-medium focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              + تذكير شخصي لهذا اليوم
            </button>
          </div>
        ) : (
          <ul className="divide-y divide-gray-100 -mx-1 px-1">
            {visible.map((o, i) => (
              <DayReminderRow
                key={`${o.kind}-${o.reminder?.actionId ?? o.personal?.id ?? i}-${o.dayKey}`}
                occurrence={o}
                rescheduling={rescheduling}
                rescheduleInput={rescheduleInput}
                rescheduleError={rescheduleError}
                rescheduleSaving={rescheduleSaving}
                actingKey={actingKey}
                onStartReschedule={startReschedule}
                onRescheduleInput={setRescheduleInput}
                onSaveReschedule={() => saveReschedule(o)}
                onCancelReschedule={() => {
                  setRescheduling(null);
                  setRescheduleError('');
                }}
                onDelete={setDeleting}
                onToggleDone={togglePersonalDone}
                onEdit={(p) => setFormMode({ personal: p })}
              />
            ))}
          </ul>
        )}

        {visible.length > 0 && formMode === null ? (
          <button
            type="button"
            onClick={() => setFormMode('create')}
            className="mt-3 w-full min-h-11 rounded-xl border-2 border-dashed border-emerald-300 text-emerald-800 text-sm font-medium hover:bg-emerald-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            + تذكير شخصي لهذا اليوم
          </button>
        ) : null}

        {formMode !== null ? (
          <PersonalForm
            // مفتاح الهدف يمنع تسرب مسودة (إنشاء/تعديل-آخر) إلى هدف جديد.
            key={formMode === 'create' ? 'create' : `edit-${formMode.personal.id}`}
            dayKey={dayKey}
            initial={formMode === 'create' ? null : formMode.personal}
            onClose={() => setFormMode(null)}
            onSaved={() => {
              setFormMode(null);
              onChanged();
            }}
          />
        ) : null}
      </div>
    </div>
  );
}
