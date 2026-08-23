import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import type { AppealBaseNumberHistoryDto, AppealDto } from '../../types';

/**
 * تدوير رقم الأساس الاستئنافي: نافذة تاريخ الأرقام لكل السنوات السابقة
 * مع حقل إدخال رقم سنة التدوير الحالية — على نمط صفحة تدوير أرقام الملفات.
 */
export default function AppealRotationModal({
  appeal,
  onClose,
  onSaved,
}: {
  appeal: AppealDto;
  onClose: () => void;
  onSaved: () => void;
}) {
  const currentYear = new Date().getFullYear();
  const [history, setHistory] = useState<AppealBaseNumberHistoryDto[]>([]);
  const [value, setValue] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let alive = true;
    setLoading(true);
    api
      .get<AppealBaseNumberHistoryDto[]>(`/appeals/${appeal.id}/base-numbers`)
      .then((r) => {
        if (!alive) return;
        setHistory(Array.isArray(r.data) ? r.data : []);
      })
      .catch((err) => {
        if (alive) setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (alive) setLoading(false);
      });
    return () => {
      alive = false;
    };
  }, [appeal.id]);

  const submit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    const trimmed = normalizeArabicDigits(value).trim();
    if (!trimmed) {
      setError('أدخل رقم الأساس الاستئنافي للسنة الحالية');
      return;
    }
    setSaving(true);
    try {
      await api.put(`/appeals/${appeal.id}/base-numbers`, {
        entries: [{ baseNumber: trimmed }],
      });
      onSaved();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const fieldCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11';

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="تدوير رقم الأساس الاستئنافي"
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[90vh] overflow-y-auto">
        <div className="sticky top-0 bg-white flex justify-between items-center px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-emerald-800">تدوير رقم الأساس الاستئنافي</h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="إغلاق"
            disabled={saving}
            className="text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg w-11 h-11 inline-flex items-center justify-center text-xl focus:outline-none focus:ring-2 focus:ring-emerald-500"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} noValidate className="px-5 py-4 space-y-4">
          {error && (
            <div role="alert" className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
              {error}
            </div>
          )}

          <section aria-label="أرقام الأساس السابقة" className="space-y-1.5">
            <h4 className="text-sm font-semibold text-gray-600">أرقام الأساس السابقة</h4>
            {loading ? (
              <p className="text-gray-500 text-sm">جارِ التحميل...</p>
            ) : history.length === 0 ? (
              <p className="text-gray-400 text-sm">لا توجد أرقام مسجلة بعد.</p>
            ) : (
              <ul className="rounded-lg border border-gray-200 divide-y divide-gray-100">
                {history.map((h) => (
                  <li key={h.year} className="flex items-center justify-between px-3 py-2 text-sm">
                    <span className="text-gray-500 tabular-nums">{h.year}</span>
                    <span
                      className={`tabular-nums font-medium ${
                        h.year < currentYear && !history.some((x) => x.year === currentYear)
                          ? 'text-red-600'
                          : 'text-gray-800'
                      }`}
                    >
                      {h.baseNumber}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <div>
            <label htmlFor="rotate-year-number" className="block text-sm font-medium text-gray-700 mb-1">
              رقم الأساس الاستئنافي لسنة {currentYear}
            </label>
            <input
              id="rotate-year-number"
              value={value}
              onChange={(e) => setValue(e.target.value)}
              inputMode="numeric"
              autoComplete="off"
              placeholder="مثال: 1450…"
              className={fieldCls}
            />
            {appeal.needsRotation && (
              <p className="mt-1.5 text-xs text-red-600 font-medium">
                الرقم الحالي لسنة سابقة ولم يُدوَّر بعد.
              </p>
            )}
          </div>

          <div className="flex gap-2 pt-1">
            <button
              type="submit"
              disabled={saving || loading}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              {saving ? 'جارٍ الحفظ…' : 'حفظ التدوير'}
            </button>
            <button
              type="button"
              onClick={onClose}
              disabled={saving}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إلغاء
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
