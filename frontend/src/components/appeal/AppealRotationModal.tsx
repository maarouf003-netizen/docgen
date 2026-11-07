import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useCurrentYear } from '../../hooks/useCurrentYear';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { APPEAL_BASE_NUMBER_MAX_LENGTH } from '../../utils/appealBaseNumber';
import type { AppealBaseNumberHistoryDto, AppealDto } from '../../types';

/**
 * سجل أرقام الأساس الاستئنافي:
 * - عند غياب هوية السنة الحالية (`needsRotation`): نموذج تدوير رقم السنة الحالية
 *   مع خطوة تأكيد تعرض القيمة والسنة قبل الحفظ.
 * - عند وجودها: السجل للقراءة فقط مع «تصحيح» لأي رقم مسجّل مسبقًا (تعديل في مكانه
 *   بلا إلحاق سجل جديد).
 */
export default function AppealRotationModal({
  appeal,
  onClose,
  onSaved,
  onCorrected,
}: {
  appeal: AppealDto;
  onClose: () => void;
  onSaved: () => void;
  /** بعد تصحيح رقم مسجّل: يُبقى السجل مفتوحًا ويُحدَّث ما حوله. */
  onCorrected?: () => void;
}) {
  // سنة التدوير الاستئنافية المعتمدة من الخادم — لا من عداد المتصفح.
  const currentYear = useCurrentYear().currentYear;
  const rotationMode = appeal.needsRotation;
  const [history, setHistory] = useState<AppealBaseNumberHistoryDto[]>([]);
  const [value, setValue] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  // خطوة التأكيد المسبق للتدوير: تعرض القيمة والسنة والسجل قبل الحفظ.
  const [confirming, setConfirming] = useState(false);
  const [confirmedValue, setConfirmedValue] = useState('');
  const confirmTitleRef = useRef<HTMLHeadingElement>(null);

  // تصحيح رقم موجود: سنة السطر قيد التصحيح وقيمته.
  const [correctingYear, setCorrectingYear] = useState<number | null>(null);
  const [correctValue, setCorrectValue] = useState('');
  const [correctionError, setCorrectionError] = useState<string | null>(null);

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

  useEffect(() => {
    if (confirming) confirmTitleRef.current?.focus();
  }, [confirming]);

  const reloadHistory = async () => {
    const r = await api.get<AppealBaseNumberHistoryDto[]>(`/appeals/${appeal.id}/base-numbers`);
    setHistory(Array.isArray(r.data) ? r.data : []);
  };

  const submitRotation = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError(null);
    const trimmed = normalizeArabicDigits(value).trim();
    if (!trimmed) {
      setError('أدخل رقم الأساس الاستئنافي للسنة الحالية');
      return;
    }
    setConfirmedValue(trimmed);
    setConfirming(true);
  };

  const doSaveRotation = async () => {
    setError(null);
    setSaving(true);
    try {
      await api.put(`/appeals/${appeal.id}/base-numbers`, {
        entries: [{ baseNumber: confirmedValue }],
      });
      onSaved();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const startCorrection = (entry: AppealBaseNumberHistoryDto) => {
    setCorrectingYear(entry.year);
    setCorrectValue(entry.baseNumber);
    setCorrectionError(null);
    setError(null);
  };

  const cancelCorrection = () => {
    setCorrectingYear(null);
    setCorrectValue('');
    setCorrectionError(null);
  };

  const doSaveCorrection = async () => {
    if (correctingYear === null) return;
    const trimmed = normalizeArabicDigits(correctValue).trim();
    if (!trimmed) {
      setCorrectionError('أدخل رقم الأساس الاستئنافي');
      return;
    }

    setSaving(true);
    setCorrectionError(null);
    try {
      await api.put(`/appeals/${appeal.id}/base-numbers/${correctingYear}`, {
        baseNumber: trimmed,
      });
    } catch (err) {
      // فشل الحفظ فقط هو خطأ التصحيح؛ لا يُخلط بخطأ تحديث السجل بعده.
      setCorrectionError(getApiErrorMessage(err));
      setSaving(false);
      return;
    }

    setSaving(false);
    setCorrectingYear(null);
    setCorrectValue('');
    if (onCorrected) onCorrected();
    else onSaved();

    try {
      await reloadHistory();
    } catch {
      setError('تم حفظ التصحيح، لكن تعذّر تحديث السجل — أعد فتح النافذة.');
    }
  };

  const submitCorrection = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    void doSaveCorrection();
  };

  const fieldCls =
    'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11';

  const title = rotationMode ? 'تدوير رقم الأساس الاستئنافي' : 'سجل أرقام الأساس الاستئنافي';

  return (
    <div
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[90vh] overflow-y-auto">
        <div className="sticky top-0 bg-white flex justify-between items-center px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-emerald-800">{title}</h3>
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

        <div className="px-5 py-4 space-y-4">
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
                  <li key={h.year} className="px-3 py-2 text-sm">
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-gray-500 tabular-nums">{h.year}</span>
                      <span className="flex items-center gap-2 min-w-0">
                        {h.isCurrent && (
                          <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] text-emerald-800">
                            الحالي
                          </span>
                        )}
                        <span className="tabular-nums font-medium text-gray-800 truncate">{h.baseNumber}</span>
                        {correctingYear !== h.year && (
                          <button
                            type="button"
                            onClick={() => startCorrection(h)}
                            disabled={saving}
                            aria-label={`تصحيح رقم الأساس لسنة ${h.year}`}
                            className="shrink-0 border border-gray-300 rounded-lg px-2 py-1 text-xs text-gray-700 hover:bg-gray-50 min-h-11"
                          >
                            تصحيح
                          </button>
                        )}
                      </span>
                    </div>

                    {correctingYear === h.year && (
                      <form
                        onSubmit={submitCorrection}
                        noValidate
                        className="mt-2 space-y-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2"
                      >
                        <label
                          htmlFor={`correct-base-${h.year}`}
                          className="block text-xs font-medium text-amber-900"
                        >
                          تصحيح رقم سنة {h.year}
                        </label>
                        <input
                          id={`correct-base-${h.year}`}
                          value={correctValue}
                          onChange={(e) => setCorrectValue(e.target.value)}
                          inputMode="numeric"
                          autoComplete="off"
                          maxLength={APPEAL_BASE_NUMBER_MAX_LENGTH}
                          className={fieldCls}
                        />
                        {correctionError && (
                          <p role="alert" className="text-xs text-red-700">{correctionError}</p>
                        )}
                        <div className="flex gap-2 flex-wrap">
                          <button
                            type="submit"
                            disabled={saving}
                            className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-60 text-white rounded-lg px-4 py-2 text-sm font-medium min-h-11"
                          >
                            {saving ? 'جارٍ الحفظ…' : 'حفظ التصحيح'}
                          </button>
                          <button
                            type="button"
                            onClick={cancelCorrection}
                            disabled={saving}
                            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-white min-h-11"
                          >
                            إلغاء
                          </button>
                        </div>
                      </form>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>

          {rotationMode ? (
            confirming ? (
              <section aria-label="تأكيد التدوير" className="space-y-3 rounded-lg border border-gray-200 bg-gray-50 px-4 py-3">
                <h4 ref={confirmTitleRef} tabIndex={-1} className="text-sm font-bold text-gray-800 focus:outline-none">
                  تأكيد حفظ التدوير
                </h4>
                <dl className="space-y-1.5 text-sm">
                  <div className="flex items-center justify-between gap-2">
                    <dt className="text-gray-500">الرقم الجديد</dt>
                    <dd className="font-bold text-gray-900 tabular-nums">{confirmedValue}</dd>
                  </div>
                  <div className="flex items-center justify-between gap-2">
                    <dt className="text-gray-500">سنة التدوير</dt>
                    <dd className="font-medium text-gray-900 tabular-nums">{currentYear}</dd>
                  </div>
                  <div className="flex items-center justify-between gap-2">
                    <dt className="text-gray-500">السجلات المحفوظة سابقًا</dt>
                    <dd className="font-medium text-gray-900 tabular-nums">{loading ? '…' : history.length}</dd>
                  </div>
                </dl>
                <div className="flex gap-2 pt-1 flex-wrap">
                  <button
                    type="button"
                    onClick={doSaveRotation}
                    disabled={saving}
                    className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-60 text-white rounded-lg px-5 py-2 text-sm font-medium min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  >
                    {saving ? 'جارٍ الحفظ…' : 'تأكيد الحفظ'}
                  </button>
                  <button
                    type="button"
                    onClick={() => setConfirming(false)}
                    disabled={saving}
                    className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-white min-h-11"
                  >
                    رجوع للتعديل
                  </button>
                </div>
              </section>
            ) : (
              <form onSubmit={submitRotation} noValidate className="space-y-4">
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
                    maxLength={APPEAL_BASE_NUMBER_MAX_LENGTH}
                    placeholder="مثال: 1450…"
                    className={fieldCls}
                  />
                  <p className="mt-1.5 text-xs text-red-600 font-medium">
                    لا يوجد رقم أساس مسجّل لهذه السنة — التدوير يحفظ الرقم السابق ويُثبت رقم السنة الحالية.
                  </p>
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
            )
          ) : (
            <div className="flex gap-2 pt-1">
              <button
                type="button"
                onClick={onClose}
                disabled={saving}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إغلاق
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
