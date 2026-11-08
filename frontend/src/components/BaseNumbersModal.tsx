import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { getNumberReuseWarning, type NumberReuseWarning } from '../api/numberWarning';
import { useCurrentYear } from '../hooks/useCurrentYear';
import NumberReuseWarningModal from './NumberReuseWarningModal';
import type { BaseNumberHistoryDto } from '../types';

const SOURCE_LABELS: Record<string, string> = {
  registration: 'قيد',
  rotation: 'تدوير',
  renewal: 'تجديد',
};

const VOID_LABELS: Record<string, string> = {
  'document-deleted': 'ملغى — حذف ملف',
  'undo-rotation': 'ملغى — تراجع عن تدوير',
  'registration-superseded': 'ملغى — استُبدل القيد',
  'registration-cleared': 'ملغى — أُفرغ رقم القيد',
};

function statusLabel(entry: BaseNumberHistoryDto): string {
  if (entry.isVoided) return VOID_LABELS[entry.voidReason ?? ''] ?? 'ملغى';
  if (entry.isEffective) return 'فعّال';
  return 'سابق';
}

/**
 * سجل أرقام الأساس الكامل: يعرض كل الصفوف (بما فيها الملغاة) مع الدائرة والمصدر،
 * ويتيح تصحيح صف فعّال موضعيًا والتراجع عن تدوير السنة الحالية. لا يُحذف صف.
 * في بوابة المندوب (fetchUrl) يكون السجل للقراءة فقط.
 */
export default function BaseNumbersModal({
  documentId,
  documentTitle,
  fileType,
  fetchUrl,
  onClose,
}: {
  documentId: number;
  documentTitle?: string;
  fileType?: string;
  onClose: () => void;
  /** مسار جلب التاريخ (افتراضيًا المسار الداخلي للمحامي) — بوابة المندوب تمرّر النقطة المسموح لها. */
  fetchUrl?: string;
}) {
  const { currentYear } = useCurrentYear();
  const readOnly = Boolean(fetchUrl);
  const [entries, setEntries] = useState<BaseNumberHistoryDto[] | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [correcting, setCorrecting] = useState<BaseNumberHistoryDto | null>(null);
  const [correctValue, setCorrectValue] = useState('');
  const [correctError, setCorrectError] = useState('');
  const [undoTarget, setUndoTarget] = useState<BaseNumberHistoryDto | null>(null);
  const [warning, setWarning] = useState<NumberReuseWarning | null>(null);

  const load = useCallback(async () => {
    const response = await api.get<BaseNumberHistoryDto[]>(
      fetchUrl ?? `/documents/${documentId}/base-numbers`,
    );
    setEntries(Array.isArray(response.data) ? response.data : []);
  }, [documentId, fetchUrl]);

  useEffect(() => {
    let cancelled = false;
    setEntries(null);
    setError('');
    api
      .get<BaseNumberHistoryDto[]>(fetchUrl ?? `/documents/${documentId}/base-numbers`)
      .then((r) => {
        if (!cancelled) setEntries(Array.isArray(r.data) ? r.data : []);
      })
      .catch((err) => {
        if (!cancelled) setError(getApiErrorMessage(err));
      });
    return () => {
      cancelled = true;
    };
  }, [documentId, fetchUrl]);

  const startCorrection = (entry: BaseNumberHistoryDto) => {
    if (entry.isVoided) return;
    setCorrecting(entry);
    setCorrectValue(entry.baseNumber);
    setCorrectError('');
    setError('');
  };

  const saveCorrection = async (warningToken?: string) => {
    if (!correcting) return;
    const value = correctValue.trim();
    if (!value) {
      setCorrectError('أدخل رقم الأساس');
      return;
    }
    setBusy(true);
    setCorrectError('');
    try {
      await api.put(`/documents/${documentId}/base-numbers/${correcting.year}`, {
        baseNumber: value,
        rowId: correcting.id ?? null,
        ...(warningToken ? { warningToken } : {}),
      });
      setCorrecting(null);
      setCorrectValue('');
      setWarning(null);
      await load();
    } catch (err) {
      const reuse = getNumberReuseWarning(err);
      if (reuse) setWarning(reuse);
      else setCorrectError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const undoRotation = async () => {
    if (!undoTarget) return;
    setBusy(true);
    setError('');
    try {
      await api.delete(`/documents/${documentId}/base-numbers/${undoTarget.year}`);
      setUndoTarget(null);
      await load();
    } catch (err) {
      setError(getApiErrorMessage(err));
      setUndoTarget(null);
    } finally {
      setBusy(false);
    }
  };

  const fieldCls =
    'w-full min-h-11 rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500';

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 overscroll-contain"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="سجل أرقام الأساس"
    >
      <div className="w-full max-w-2xl overflow-y-auto rounded-xl bg-white shadow-xl max-h-[90vh]">
        <div className="sticky top-0 flex items-center justify-between border-b border-gray-200 bg-white px-5 py-4">
          <h3 className="text-lg font-bold text-gray-800">سجل أرقام الأساس</h3>
          <button
            onClick={onClose}
            className="min-h-11 px-2 text-xl leading-none text-gray-400 hover:text-gray-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            aria-label="إغلاق"
            type="button"
          >
            ×
          </button>
        </div>

        <div className="px-4 py-4 sm:px-5">
          <div className="mb-4 rounded-lg border border-gray-200 bg-gray-50 px-3 py-2">
            <p className="text-xs text-gray-500">الملف</p>
            <p className="font-medium text-gray-800">
              {documentTitle || `ملف رقم ${documentId}`}
            </p>
          </div>

          {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
          {entries === null && !error && <p className="text-gray-500">جارِ التحميل…</p>}
          {entries !== null && entries.length === 0 && !error && (
            <p className="text-sm text-gray-400">لا توجد أرقام أساس مسجلة لهذا الملف</p>
          )}

          {entries !== null && entries.length > 0 && (
            <>
              {/* الجوال: بطاقات؛ المكتبي: جدول */}
              <div className="flex flex-col gap-3 sm:hidden">
                {entries.map((entry) => (
                  <article
                    key={entry.id ?? `${entry.year}-${entry.baseNumber}`}
                    className={`rounded-xl border p-3 ${entry.isVoided ? 'border-gray-200 bg-gray-50' : 'border-emerald-100 bg-white'}`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-lg font-bold text-gray-900 tabular-nums">
                          {entry.baseNumber}
                        </p>
                        <p className="text-xs text-gray-500">
                          سنة {entry.year} · {SOURCE_LABELS[entry.source ?? ''] ?? '—'}
                          {entry.circuitName ? ` · ${entry.circuitName}` : ''}
                        </p>
                      </div>
                      <span
                        className={`shrink-0 rounded-full px-2 py-1 text-xs font-medium ${entry.isVoided ? 'bg-gray-200 text-gray-600' : entry.isEffective ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'}`}
                      >
                        {statusLabel(entry)}
                      </span>
                    </div>
                    {!readOnly && !entry.isVoided && (
                      <div className="mt-3 flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => startCorrection(entry)}
                          className="min-h-11 rounded-lg border border-emerald-300 px-3 py-1 text-sm text-emerald-800 hover:bg-emerald-50"
                        >
                          تصحيح
                        </button>
                        {entry.source === 'rotation' && entry.year === currentYear && (
                          <button
                            type="button"
                            onClick={() => setUndoTarget(entry)}
                            className="min-h-11 rounded-lg border border-amber-300 px-3 py-1 text-sm text-amber-800 hover:bg-amber-50"
                          >
                            تراجع
                          </button>
                        )}
                      </div>
                    )}
                  </article>
                ))}
              </div>

              <div className="hidden overflow-x-auto sm:block">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-right text-xs text-gray-500">
                      <th className="border-b border-gray-200 py-2 pl-2 font-medium">السنة</th>
                      <th className="border-b border-gray-200 py-2 pl-2 font-medium">رقم الأساس</th>
                      <th className="border-b border-gray-200 py-2 pl-2 font-medium">الدائرة</th>
                      <th className="border-b border-gray-200 py-2 pl-2 font-medium">المصدر</th>
                      <th className="border-b border-gray-200 py-2 pl-2 font-medium">الحالة</th>
                      {!readOnly && <th className="border-b border-gray-200 py-2 font-medium">إجراء</th>}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {entries.map((entry) => (
                      <tr
                        key={entry.id ?? `${entry.year}-${entry.baseNumber}`}
                        className={entry.isVoided ? 'text-gray-400' : ''}
                      >
                        <td className="py-2 pl-2 tabular-nums">{entry.year}</td>
                        <td className="py-2 pl-2 font-medium tabular-nums">{entry.baseNumber}</td>
                        <td className="max-w-40 truncate py-2 pl-2">{entry.circuitName || '—'}</td>
                        <td className="py-2 pl-2">
                          {SOURCE_LABELS[entry.source ?? ''] ?? '—'}
                        </td>
                        <td className="py-2 pl-2">{statusLabel(entry)}</td>
                        {!readOnly && (
                          <td className="py-2">
                            <div className="flex flex-wrap gap-1">
                              {!entry.isVoided && (
                                <button
                                  type="button"
                                  onClick={() => startCorrection(entry)}
                                  className="rounded-md border border-emerald-300 px-2 py-1 text-xs text-emerald-800 hover:bg-emerald-50"
                                >
                                  تصحيح
                                </button>
                              )}
                              {entry.source === 'rotation' && entry.year === currentYear && (
                                <button
                                  type="button"
                                  onClick={() => setUndoTarget(entry)}
                                  className="rounded-md border border-amber-300 px-2 py-1 text-xs text-amber-800 hover:bg-amber-50"
                                >
                                  تراجع
                                </button>
                              )}
                            </div>
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {fileType && (
                <p className="mt-2 text-xs text-gray-400">نوع الملف الحالي: {fileType}</p>
              )}
            </>
          )}

          {correcting && (
            <form
              className="mt-4 rounded-xl border border-emerald-200 bg-emerald-50/50 p-3"
              onSubmit={(e) => {
                e.preventDefault();
                void saveCorrection();
              }}
            >
              <h4 className="mb-2 font-bold text-emerald-900">
                تصحيح رقم سنة {correcting.year}
              </h4>
              <p className="mb-2 text-xs text-gray-600">
                القيمة السابقة: {correcting.baseNumber} — التصحيح يعدّل الصف نفسه ويُحفظ في التدقيق.
              </p>
              <label htmlFor="correct-base-number" className="mb-1 block text-xs font-bold text-gray-600">
                رقم الأساس الجديد
              </label>
              <input
                id="correct-base-number"
                type="text"
                value={correctValue}
                onChange={(e) => setCorrectValue(e.target.value)}
                maxLength={50}
                className={fieldCls}
                autoComplete="off"
                placeholder="مثال: 1234…"
              />
              {correctError && (
                <p className="mt-2 text-sm text-red-600" role="alert">
                  {correctError}
                </p>
              )}
              <div className="mt-3 flex flex-wrap justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setCorrecting(null)}
                  className="min-h-11 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                >
                  إلغاء
                </button>
                <button
                  type="submit"
                  disabled={busy}
                  className="min-h-11 rounded-lg bg-emerald-800 px-5 py-2 text-sm text-white hover:bg-emerald-700 disabled:opacity-50"
                >
                  {busy ? 'جارِ الحفظ…' : 'حفظ التصحيح'}
                </button>
              </div>
            </form>
          )}

          <div className="mt-5 flex justify-end">
            <button
              onClick={onClose}
              type="button"
              className="min-h-11 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
            >
              إغلاق
            </button>
          </div>
        </div>
      </div>

      {undoTarget && (
        <div
          className="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 p-4"
          role="alertdialog"
          aria-modal="true"
          aria-label="تأكيد التراجع عن التدوير"
        >
          <div className="w-full max-w-sm rounded-xl bg-white p-5 shadow-xl">
            <h4 className="mb-2 text-lg font-bold text-gray-900">تأكيد التراجع</h4>
            <p className="text-sm leading-7 text-gray-700">
              سيُلغى رقم الأساس {undoTarget.baseNumber} لسنة {undoTarget.year} ويبقى ظاهرًا
              في السجل بوسم «ملغى — تراجع». هل تريد المتابعة؟
            </p>
            <div className="mt-4 flex flex-wrap justify-end gap-2">
              <button
                type="button"
                onClick={() => setUndoTarget(null)}
                className="min-h-11 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
              >
                إلغاء
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void undoRotation()}
                className="min-h-11 rounded-lg bg-amber-700 px-5 py-2 text-sm text-white hover:bg-amber-800 disabled:opacity-50"
              >
                {busy ? 'جارِ التراجع…' : 'تأكيد التراجع'}
              </button>
            </div>
          </div>
        </div>
      )}

      {warning && (
        <NumberReuseWarningModal
          message={warning.message}
          busy={busy}
          onCancel={() => setWarning(null)}
          onConfirm={() => void saveCorrection(warning.warningToken)}
        />
      )}
    </div>
  );
}
