import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../../api/client';
import type {
  AppealActionDto,
  AppealReminderDto,
  ExecutionActionDto,
  PersonalReminderDto,
  ReminderDto,
} from '../../types';
import { PERSONAL_COLORS, PERSONAL_RECURRENCES } from '../../types';
import {
  borrowerFullName,
  dueLabel,
} from './dashboardFormat';
import {
  dayLabelOf,
  dotTone,
  parseDayKey,
  toDayKey,
  type CalendarOccurrence,
} from './personalReminders';

type DeletingTarget =
  | { kind: 'document'; actionId: number; documentId: number; title: string }
  | { kind: 'appeal'; actionId: number; appealId: number; title: string }
  | { kind: 'personal'; id: number; title: string };

const END_OPTIONS = [
  { value: '', label: 'بلا نهاية' },
  { value: '7', label: 'بعد أسبوع' },
  { value: '30', label: 'بعد شهر' },
  { value: '90', label: 'بعد 3 أشهر' },
  { value: '365', label: 'بعد سنة' },
];

function endDateFrom(dayKey: string, days: string): string | null {
  if (!days) return null;
  const base = parseDayKey(dayKey);
  if (!base) return null;
  base.setDate(base.getDate() + Number(days));
  return toDayKey(base);
}

/**
 * نافذة تذكيرات اليوم: قائمة المواعيد الثلاثة (ملف/استئناف/شخصي) مع روابطها وشاراتها،
 * ونموذج تذكير شخصي (إنشاء/تعديل)، وتغيير يوم تذكير الملف/الاستئناف (نص حر بنفس عقد
 * `ParseDateTime`)، وإلغاء التذكير بتأكيد صريح — سفلية على الجوال ووسطية على المكتبي.
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
  const visible = useMemo(
    () =>
      occurrences.filter((o) => {
        if (!query) return true;
        const haystack = [
          o.kind === 'personal' ? o.personal?.title : null,
          o.kind === 'personal' ? o.personal?.notes : null,
          o.reminder && 'borrowerName' in o.reminder ? borrowerFullName(o.reminder as ReminderDto) : null,
          o.reminder && 'appealTitle' in o.reminder ? (o.reminder as AppealReminderDto).appealTitle : null,
          o.reminder?.actionText,
        ]
          .filter(Boolean)
          .join(' ');
        return haystack.includes(query);
      }),
    [occurrences, query],
  );

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
      // نوع الإجراء الأصلي يُحفَظ كما هو (لا يُكشف في `ReminderDto`) — يُجلب من القائمة.
      const { data: actions } = await api.get<(ExecutionActionDto | AppealActionDto)[]>(`${base}/actions`);
      const current = (Array.isArray(actions) ? actions : []).find((a) => a.id === r.actionId);
      await api.put(`${base}/actions/${r.actionId}`, {
        type: current?.type ?? 'action',
        text: r.actionText,
        actionDate: rescheduleInput.trim(),
        reminderDuration: r.reminderDuration ?? null,
        reminderColor: r.reminderColor ?? null,
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
    const key = `${deleting.kind}-${deleting.kind === 'personal' ? deleting.id : deleting.actionId}`;
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
    const key = `personal-done-${personal.id}`;
    setActingKey(key);
    setActionError('');
    try {
      await api.patch(`/personal-reminders/${personal.id}/occurrences`, {
        occurrenceDate: dayKey,
        done,
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
              <DayRow
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

function RowBadge({ dueDate }: { dueDate: string }) {
  const { text, tone } = dueLabel(dueDate);
  return (
    <span className={`text-xs rounded-full border px-2 py-0.5 font-medium whitespace-nowrap ${tone}`}>
      {text}
    </span>
  );
}

function DayRow({
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
    const doneKey = `personal-done-${p.id}`;
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
    ? (r.appealTitle ?? `استئناف ${parentId}`)
    : borrowerFullName(r as ReminderDto);
  const rescheduleKey = `${o.kind}-${r.actionId}`;

  return (
    <li className="py-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <Link to={linkTo} className="font-medium text-sky-700 hover:underline break-words">
            {title}
          </Link>
          <p className="text-xs text-gray-500 mt-0.5 break-words line-clamp-2">{r.actionText}</p>
        </div>
        <RowBadge dueDate={r.dueDate} />
      </div>
      {rescheduling === rescheduleKey ? (
        <div className="mt-2 rounded-xl border border-gray-200 p-3">
          <label className="block text-xs text-gray-600 mb-1.5" htmlFor={`reschedule-${rescheduleKey}`}>
            اليوم الجديد (تاريخ حر — المدة نفسها تُحفَظ)
          </label>
          <input
            id={`reschedule-${rescheduleKey}`}
            type="text"
            inputMode="numeric"
            value={rescheduleInput}
            onChange={(e) => onRescheduleInput(e.target.value)}
            placeholder="مثال: 1/8/2026…"
            name={`reschedule-${rescheduleKey}`}
            autoComplete="off"
            className="w-full min-h-11 rounded-xl border border-gray-200 px-3 text-sm tabular-nums focus-visible:ring-2 focus-visible:ring-emerald-600"
          />
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
            onClick={() => onStartReschedule(rescheduleKey, r.actionDate ?? r.dueDate)}
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

export function PersonalForm({
  dayKey,
  initial,
  onClose,
  onSaved,
}: {
  dayKey: string;
  initial: PersonalReminderDto | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [title, setTitle] = useState(initial?.title ?? '');
  const [notes, setNotes] = useState(initial?.notes ?? '');
  // التعديل يحفظ اللون المخزّن (بما فيه الغياب) — لا يُزرَع لون افتراضي دون قصد.
  const [color, setColor] = useState(initial ? (initial.color ?? '') : 'زمردي');
  const [recurrence, setRecurrence] = useState(initial?.recurrence ?? 'مرة واحدة');
  const [endDays, setEndDays] = useState('');
  const [archived, setArchived] = useState(initial?.isArchived ?? false);
  const [titleError, setTitleError] = useState('');
  const [submitError, setSubmitError] = useState('');
  const [saving, setSaving] = useState(false);
  const titleRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    titleRef.current?.focus();
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      setTitleError('عنوان التذكير مطلوب');
      titleRef.current?.focus();
      return;
    }
    setTitleError('');
    setSubmitError('');
    setSaving(true);
    try {
      if (initial) {
        await api.put(`/personal-reminders/${initial.id}`, {
          title: title.trim(),
          // النص الخام كما هو: الفارغ يمسح (الخلفية تقبله)، وغيره يُطبَّع خادميًا.
          notes,
          color,
          recurrence,
          // `__clear__` تُرسل سلسلة فارغة فتمسحها الخلفية؛ `undefined` تُبقي الحالية.
          recurrenceEnd: endDays === '__clear__' ? '' : endDays ? endDateFrom(dayKey, endDays) : undefined,
          isArchived: archived,
        });
      } else {
        await api.post('/personal-reminders', {
          title: title.trim(),
          notes: notes.trim() ? notes.trim() : null,
          dueDate: dayKey,
          color,
          recurrence,
          recurrenceEnd: endDateFrom(dayKey, endDays),
        });
      }
      onSaved();
    } catch (err) {
      setSubmitError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="mt-3 rounded-2xl border border-emerald-200 bg-emerald-50/50 p-4">
      <h4 className="font-bold text-gray-900 text-sm mb-3">
        {initial ? 'تعديل التذكير الشخصي' : 'تذكير شخصي جديد لهذا اليوم'}
      </h4>

      <label className="block mb-2.5" htmlFor="personal-title">
        <span className="block text-xs text-gray-600 mb-1">العنوان *</span>
        <input
          ref={titleRef}
          id="personal-title"
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="مثال: مراجعة دائرة التنفيذ…"
          name="personal-title"
          autoComplete="off"
          maxLength={200}
          className="w-full min-h-11 rounded-xl border border-gray-200 bg-white px-3 text-sm focus-visible:ring-2 focus-visible:ring-emerald-600"
        />
      </label>
      {titleError ? (
        <p role="alert" className="text-red-700 text-xs mb-2">
          {titleError}
        </p>
      ) : null}

      <label className="block mb-2.5" htmlFor="personal-notes">
        <span className="block text-xs text-gray-600 mb-1">ملاحظة</span>
        <textarea
          id="personal-notes"
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          placeholder="تفاصيل اختيارية…"
          name="personal-notes"
          autoComplete="off"
          rows={2}
          maxLength={2000}
          className="w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm focus-visible:ring-2 focus-visible:ring-emerald-600"
        />
      </label>

      <div className="grid grid-cols-2 gap-2.5 mb-2.5">
        <label className="block" htmlFor="personal-color">
          <span className="block text-xs text-gray-600 mb-1">اللون</span>
          <select
            id="personal-color"
            value={color}
            onChange={(e) => setColor(e.target.value)}
            name="personal-color"
            className="w-full min-h-11 rounded-xl border border-gray-200 bg-white px-2 text-sm focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            {initial ? <option value="">بلا لون</option> : null}
            {PERSONAL_COLORS.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </label>
        <label className="block" htmlFor="personal-recurrence">
          <span className="block text-xs text-gray-600 mb-1">التكرار</span>
          <select
            id="personal-recurrence"
            value={recurrence}
            onChange={(e) => setRecurrence(e.target.value)}
            name="personal-recurrence"
            className="w-full min-h-11 rounded-xl border border-gray-200 bg-white px-2 text-sm focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            {PERSONAL_RECURRENCES.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
        </label>
      </div>

      <label className="block mb-2.5" htmlFor="personal-end">
        <span className="block text-xs text-gray-600 mb-1">
          انتهاء التكرار{initial?.recurrenceEnd ? ` (الحالي: ${initial.recurrenceEnd})` : ''}
        </span>
        <select
          id="personal-end"
          value={endDays}
          onChange={(e) => setEndDays(e.target.value)}
          name="personal-end"
          className="w-full min-h-11 rounded-xl border border-gray-200 bg-white px-2 text-sm focus-visible:ring-2 focus-visible:ring-emerald-600"
        >
          {END_OPTIONS.map((o) => (
            <option key={o.label} value={o.value}>
              {o.value === '' && initial?.recurrenceEnd ? 'إبقاء النهاية الحالية' : o.label}
            </option>
          ))}
          {initial?.recurrenceEnd ? <option value="__clear__">إزالة النهاية</option> : null}
        </select>
      </label>

      {initial ? (
        <label className="flex items-center gap-2 mb-3 text-sm text-gray-700 min-h-11" htmlFor="personal-archived">
          <input
            id="personal-archived"
            type="checkbox"
            checked={archived}
            onChange={(e) => setArchived(e.target.checked)}
            name="personal-archived"
            className="w-5 h-5 accent-emerald-700"
          />
          مؤرشف (يُخفى من التقويم)
        </label>
      ) : null}

      {submitError ? (
        <p role="alert" className="text-red-700 text-xs mb-2">
          {submitError}
        </p>
      ) : null}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={saving}
          className="min-h-11 px-4 rounded-lg bg-emerald-700 hover:bg-emerald-600 text-white text-sm font-medium disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
        >
          {saving ? 'جارٍ الحفظ…' : initial ? 'حفظ التعديل' : 'إضافة التذكير'}
        </button>
        <button
          type="button"
          onClick={onClose}
          className="min-h-11 px-4 rounded-lg border border-gray-200 text-sm hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
        >
          إلغاء
        </button>
      </div>
    </form>
  );
}
