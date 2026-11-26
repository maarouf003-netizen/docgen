import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../../api/client';
import type { PersonalReminderDto } from '../../../types';
import { PERSONAL_COLORS, PERSONAL_RECURRENCES } from '../../../types';
import { END_OPTIONS, endDateFrom } from './dayRemindersModel';

/**
 * نموذج التذكير الشخصي (إنشاء/تعديل) داخل نافذة اليوم أو التقويم.
 * التعديل يحفظ اللون المخزّن (بما فيه الغياب) ولا يزرع لونًا افتراضيًا دون قصد.
 */
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
          version: initial.version,
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
