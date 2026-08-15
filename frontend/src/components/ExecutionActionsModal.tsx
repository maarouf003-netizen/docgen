import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { sanitizeRichText, richToPlainText } from '../utils/richText';
import { normalizeArabicDigits } from '../utils/arabicDigits';
import RichTextEditor from './RichTextEditor';
import type { ExecutionActionDto } from '../types';

function isNote(a: ExecutionActionDto) {
  return a.type === 'note';
}

const REMINDER_DURATIONS = ['3 أيام', 'أسبوع', 'أسبوعين', 'شهر'] as const;
const REMINDER_COLORS = ['أحمر', 'بنفسجي', 'أصفر'] as const;

const REMINDER_COLOR_STYLES: Record<string, string> = {
  'أحمر': 'bg-red-100 text-red-700 border-red-200',
  'بنفسجي': 'bg-purple-100 text-purple-700 border-purple-200',
  'أصفر': 'bg-amber-100 text-amber-700 border-amber-200',
};

export default function ExecutionActionsModal({
  documentId,
  onClose,
  onChanged,
}: {
  documentId: number;
  onClose: () => void;
  onChanged?: () => void;
}) {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';

  const [actions, setActions] = useState<ExecutionActionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [type, setType] = useState<'action' | 'note'>('action');
  const [text, setText] = useState('');
  const [actionDate, setActionDate] = useState('');
  const [remind, setRemind] = useState(false);
  const [reminderDuration, setReminderDuration] = useState<string>('');
  const [reminderColor, setReminderColor] = useState<string>('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [formSession, setFormSession] = useState(0);

  const load = () => {
    setLoading(true);
    setError('');
    api
      .get<ExecutionActionDto[]>(`/documents/${documentId}/actions`)
      .then((r) => setActions(Array.isArray(r.data) ? r.data : []))
      .catch((err) => setError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [documentId]);

  const resetForm = () => {
    setType('action');
    setText('');
    setActionDate('');
    setRemind(false);
    setReminderDuration('');
    setReminderColor('');
    setShowForm(false);
    setEditingId(null);
    setSaveError('');
    setFormSession((s) => s + 1);
  };

  const submit = async (targetType: 'action' | 'note') => {
    if (!richToPlainText(text)) {
      setSaveError('نص الإجراء أو الملاحظة مطلوب');
      return;
    }
    setSaving(true);
    setSaveError('');
    try {
      const payload = {
        type: targetType,
        text,
        actionDate: normalizeArabicDigits(actionDate) || null,
        reminderDuration: remind ? reminderDuration || null : null,
        reminderColor: remind ? reminderColor || null : null,
      };
      if (editingId !== null) {
        await api.put(`/documents/${documentId}/actions/${editingId}`, payload);
      } else {
        await api.post(`/documents/${documentId}/actions`, payload);
      }
      resetForm();
      load();
      onChanged?.();
    } catch (err) {
      setSaveError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const startEdit = (a: ExecutionActionDto) => {
    setEditingId(a.id);
    setType(isNote(a) ? 'note' : 'action');
    setText(a.text);
    setActionDate(a.actionDate ?? '');
    setRemind(Boolean(a.reminderDuration || a.reminderColor));
    setReminderDuration(a.reminderDuration ?? '');
    setReminderColor(a.reminderColor ?? '');
    setShowForm(true);
    setSaveError('');
    setFormSession((s) => s + 1);
  };

  const remove = async (a: ExecutionActionDto) => {
    if (!window.confirm('هل أنت متأكد من حذف هذا العنصر؟')) return;
    setBusyId(a.id);
    setError('');
    try {
      await api.delete(`/documents/${documentId}/actions/${a.id}`);
      load();
      onChanged?.();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  const dateLabel =
    editingId !== null
      ? type === 'action'
        ? '(مطلوب للإجراء)'
        : '(اختياري للملاحظة)'
      : '(يلزم للإجراء، اختياري للملاحظة)';

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" dir="rtl">
      <div
        role="dialog"
        aria-modal="true"
        aria-label="الإجراءات والملاحظات"
        className="bg-white rounded-xl shadow-xl w-full max-w-4xl max-h-[90vh] flex flex-col overflow-hidden"
      >
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-gray-800">الإجراءات والملاحظات</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-3 border-b border-gray-100 flex items-center justify-between">
          <span className="text-sm text-gray-600">{actions.length} عنصر</span>
          {isLawyer && !showForm && (
            <button
              onClick={() => { setShowForm(true); setSaveError(''); }}
              className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              + إضافة إجراء أو ملاحظة
            </button>
          )}
        </div>

        <div className="flex-1 min-h-0 overflow-y-auto">
          {showForm && (
            <form className="px-5 py-4 border-b border-gray-200 space-y-3" onSubmit={(e) => e.preventDefault()}>
              <div>
                <span className="block text-xs font-medium text-gray-600 mb-1">النص</span>
                <RichTextEditor
                  key={formSession}
                  value={text}
                  onChange={setText}
                  placeholder="أدخل نص الإجراء أو الملاحظة..."
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  التاريخ {dateLabel}
                </label>
                <input
                  value={actionDate}
                  onChange={(e) => setActionDate(e.target.value)}
                  placeholder="مثال: 1/8/2026"
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div className="flex flex-col gap-2">
                <button
                  type="button"
                  onClick={() => setRemind((v) => !v)}
                  className={`min-h-11 inline-flex items-center justify-center gap-2 rounded-lg border px-4 text-sm transition-colors ${
                    remind
                      ? 'bg-amber-50 border-amber-400 text-amber-800'
                      : 'border-gray-300 text-gray-700 hover:bg-gray-50'
                  }`}
                >
                  <span aria-hidden="true">🔔</span>
                  {remind ? 'إلغاء التذكير' : 'ذكرني'}
                </button>
                {remind && (
                  <div className="flex flex-col gap-3 rounded-lg bg-gray-50 p-3">
                    <div>
                      <label htmlFor="reminder-duration" className="block text-xs font-medium text-gray-600 mb-1">مدة التذكير</label>
                      <select
                        id="reminder-duration"
                        value={reminderDuration}
                        onChange={(e) => setReminderDuration(e.target.value)}
                        className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                      >
                        <option value="">اختر المدة...</option>
                        {REMINDER_DURATIONS.map((d) => (
                          <option key={d} value={d}>{d}</option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <span className="block text-xs font-medium text-gray-600 mb-1">الأهمية</span>
                      <div className="flex flex-wrap gap-2">
                        {REMINDER_COLORS.map((c) => (
                          <button
                            key={c}
                            type="button"
                            onClick={() => setReminderColor(c)}
                            className={`min-h-11 rounded-lg border px-4 text-sm transition-colors ${
                              reminderColor === c
                                ? 'ring-2 ring-offset-1 ring-gray-800 ' + REMINDER_COLOR_STYLES[c]
                                : 'bg-white ' + REMINDER_COLOR_STYLES[c]
                            }`}
                          >
                            {c}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}
              </div>
              {saveError && <p className="text-red-600 text-sm">{saveError}</p>}
              <div className="flex flex-wrap gap-2">
                {editingId !== null ? (
                  <button
                    type="button"
                    disabled={saving}
                    onClick={() => submit(type)}
                    className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                  >
                    {saving ? 'جارِ الحفظ...' : 'حفظ التعديل'}
                  </button>
                ) : (
                  <>
                    <button
                      type="button"
                      disabled={saving}
                      onClick={() => submit('note')}
                      className="bg-sky-800 hover:bg-sky-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                    >
                      {saving ? 'جارِ الحفظ...' : 'حفظ كملاحظة'}
                    </button>
                    <button
                      type="button"
                      disabled={saving}
                      onClick={() => submit('action')}
                      className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                    >
                      {saving ? 'جارِ الحفظ...' : 'حفظ كإجراء'}
                    </button>
                  </>
                )}
                <button
                  type="button"
                  onClick={resetForm}
                  className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                >
                  إلغاء
                </button>
              </div>
            </form>
          )}

          <div className="px-5 py-4">
            {error && <p className="text-red-600 text-sm">{error}</p>}
            {loading && <p className="text-gray-500">جارِ التحميل...</p>}
            {!loading && !error && actions.length === 0 && (
              <p className="text-gray-400 text-sm text-center py-8">لا توجد إجراءات أو ملاحظات بعد</p>
            )}
            {actions.map((a) => (
              <div key={a.id} className="py-3 border-b border-gray-100 last:border-0 flex items-start justify-between gap-3">
                <div className="flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span
                      className={`text-[11px] px-2 py-0.5 rounded-full ${
                        isNote(a)
                          ? 'bg-sky-100 text-sky-800'
                          : 'bg-emerald-100 text-emerald-800'
                      }`}
                    >
                      {isNote(a) ? 'ملاحظة' : 'إجراء'}
                    </span>
                    {a.reminderDuration || a.reminderColor ? (
                      <span
                        className={`text-[11px] px-2 py-0.5 rounded-full border ${
                          REMINDER_COLOR_STYLES[a.reminderColor ?? ''] ?? 'bg-gray-100 text-gray-700 border-gray-200'
                        }`}
                      >
                        🔔 تذكير: {a.reminderDuration || '—'}
                      </span>
                    ) : null}
                    <div
                      className="text-gray-800 [&_ul]:list-disc [&_ul]:pr-5 [&_ol]:list-decimal [&_ol]:pr-5"
                      dangerouslySetInnerHTML={{ __html: sanitizeRichText(a.text) }}
                    />
                  </div>
                  <div className="text-emerald-600 text-sm mt-1">
                    {a.actionDate || '—'}
                    {a.createdByName ? <span className="text-gray-400"> · {a.createdByName}</span> : null}
                  </div>
                </div>
                {isLawyer && (
                  <div className="flex gap-1 shrink-0">
                    <button
                      onClick={() => startEdit(a)}
                      disabled={busyId === a.id}
                      className="text-sky-700 hover:bg-sky-50 rounded-lg px-2 py-1 text-xs min-h-11"
                      aria-label="تعديل"
                    >
                      تعديل
                    </button>
                    <button
                      onClick={() => remove(a)}
                      disabled={busyId === a.id}
                      className="text-red-600 hover:bg-red-50 rounded-lg px-2 py-1 text-xs min-h-11"
                      aria-label="حذف"
                    >
                      حذف
                    </button>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
